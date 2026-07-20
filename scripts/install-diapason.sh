#!/usr/bin/env bash
# Installs the Diapason reference content on a running instance.
#
# The Configuration service already seeds the Diapason experience document
# (categories, journeys, economy, intro) on first initialisation — see
# ConfigurationService.CreateDefault. That seeder cannot create scenarios,
# because scenarios live in Authoring and are authored documents, not
# configuration. This script performs that second half over the public API:
# import, validate, analyze, publish, then attach each published scenario to
# its category.
#
# Idempotent: each scenario carries a `slug` (its natural key) in the manifest,
# and the script passes it to POST /scenarios/import?slug=<slug>. The Authoring
# service upserts by slug — a slug already present updates the existing draft
# instead of creating a new one — so re-running the script never duplicates the
# ten drafts. The second pass updates and republishes; the catalog stays at ten.
#
# See specs/domain/diapason/seeding.md and scripts/reset-diapason.sh.
set -euo pipefail

AUTHORING_URL="${AUTHORING_URL:-http://localhost:5201}"
IDENTITY_URL="${IDENTITY_URL:-http://localhost:5203}"
CONFIGURATION_URL="${CONFIGURATION_URL:-http://localhost:5204}"
FRONT_ID="${FRONT_ID:-default}"
CONTENT_DIR="${CONTENT_DIR:-content/diapason}"
MANIFEST="$CONTENT_DIR/manifest.json"
USER_NAME="${GENENGINE_ADMIN_USER_NAME:?GENENGINE_ADMIN_USER_NAME must be set}"
PASSWORD="${GENENGINE_ADMIN_PASSWORD:?GENENGINE_ADMIN_PASSWORD must be set}"

for command in curl jq; do
  command -v "$command" >/dev/null || { echo "Missing required command: $command" >&2; exit 1; }
done

[[ -f "$MANIFEST" ]] || { echo "Manifest not found: $MANIFEST" >&2; exit 1; }

for endpoint in "$IDENTITY_URL/health/ready" "$AUTHORING_URL/health/ready" "$CONFIGURATION_URL/health/ready"; do
  curl --fail --silent --show-error "$endpoint" >/dev/null
done

echo "[1/4] Authenticate"
token=$(curl --fail --silent --show-error \
  -H 'Content-Type: application/json' \
  -d "$(jq -n --arg u "$USER_NAME" --arg p "$PASSWORD" '{userName:$u,password:$p}')" \
  "$IDENTITY_URL/auth/login" | jq -er '.token')

echo "[2/4] Verify the seeded Diapason configuration"
experience=$(curl --fail --silent --show-error "$CONFIGURATION_URL/experience/$FRONT_ID")
jq -e '
  .document.game.name == "Le Diapason"
  and (.document.categories | length) == 6
  and (.document.journeys | length) == 3
  and ([.document.journeys[] | select((.prerequisiteJourneyIds | length) > 0)] | length) == 2
' <<<"$experience" >/dev/null \
  || { echo "The front '$FRONT_ID' does not carry the Diapason configuration. Seed it first." >&2; exit 1; }

# Accumulated as JSON rather than a bash associative array: macOS still ships
# bash 3.2, which has no `declare -A`.
published_by_category='{}'

echo "[3/4] Import, validate, analyze and publish each scenario"
total=$(jq -r '.scenarios | length' "$MANIFEST")
index=0
while IFS=$'\t' read -r slug title category_key; do
  index=$((index + 1))
  file="$CONTENT_DIR/scenarios/$slug.json"
  [[ -f "$file" ]] || { echo "Missing scenario file: $file" >&2; exit 1; }
  printf '  (%d/%d) %s\n' "$index" "$total" "$slug"

  # The slug is the natural key: import upserts by it, so this stays idempotent.
  slug_encoded=$(jq -rn --arg s "$slug" '$s | @uri')
  imported=$(curl --fail-with-body --silent --show-error \
    -H "Authorization: Bearer $token" \
    -H 'Content-Type: application/json' \
    --data-binary "@$file" \
    "$AUTHORING_URL/scenarios/import?slug=$slug_encoded")
  scenario_id=$(jq -er '.id' <<<"$imported")
  revision=$(jq -er '.revision' <<<"$imported")

  validation=$(curl --fail-with-body --silent --show-error -X POST \
    -H "Authorization: Bearer $token" \
    "$AUTHORING_URL/scenarios/$scenario_id/validate")
  jq -e '.isValid == true' <<<"$validation" >/dev/null \
    || { echo "$slug failed validation:" >&2; jq '.issues' <<<"$validation" >&2; exit 1; }

  analysis=$(curl --fail-with-body --silent --show-error -X POST \
    -H "Authorization: Bearer $token" \
    "$AUTHORING_URL/scenarios/$scenario_id/analyze")
  jq -e '.unreachableEndingNodeIds == [] and .nodesWithoutEndingPath == []' <<<"$analysis" >/dev/null \
    || { echo "$slug has unreachable endings:" >&2; echo "$analysis" >&2; exit 1; }

  published=$(curl --fail-with-body --silent --show-error \
    -H "Authorization: Bearer $token" \
    -H 'Content-Type: application/json' \
    -d "{\"expectedRevision\":$revision}" \
    "$AUTHORING_URL/scenarios/$scenario_id/publish")
  jq -er '.id' <<<"$published" >/dev/null

  published_by_category=$(jq --arg k "$category_key" --arg id "$scenario_id" \
    '.[$k] = ((.[$k] // []) + [$id])' <<<"$published_by_category")
  echo "      $title"
done < <(jq -r '.scenarios | sort_by(.order)[] | [.slug, .title, .categoryKey] | @tsv' "$MANIFEST")

echo "[4/4] Attach the published scenarios to their categories"
admin_document=$(curl --fail --silent --show-error \
  -H "Authorization: Bearer $token" \
  "$CONFIGURATION_URL/admin/configuration/$FRONT_ID")
revision=$(jq -er '.revision' <<<"$admin_document")

# Map manifest category keys to their configuration ids, then set scenarioIds.
document=$(jq \
  --slurpfile manifest "$MANIFEST" \
  --argjson published "$published_by_category" \
  '
    ($manifest[0].categories | map({(.key): .id}) | add) as $ids
    | ($published | to_entries | map({(($ids[.key]) | ascii_downcase): .value}) | add // {}) as $byId
    | .document
    | .categories = [.categories[] | .scenarioIds = ($byId[(.id | ascii_downcase)] // .scenarioIds)]
  ' <<<"$admin_document")

updated=$(curl --fail-with-body --silent --show-error \
  -X PUT \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  -d "$(jq -n --argjson d "$document" --argjson r "$revision" '{expectedRevision:$r,document:$d}')" \
  "$CONFIGURATION_URL/admin/configuration/$FRONT_ID")
new_revision=$(jq -er '.revision' <<<"$updated")

curl --fail-with-body --silent --show-error \
  -X POST \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  -d "{\"expectedRevision\":$new_revision}" \
  "$CONFIGURATION_URL/admin/configuration/$FRONT_ID/publish" >/dev/null

echo "Diapason installed on front '$FRONT_ID': 10 scenarios published across 6 categories and 3 journeys."
