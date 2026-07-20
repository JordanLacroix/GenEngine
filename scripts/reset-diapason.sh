#!/usr/bin/env bash
# Resets a local instance to a clean Diapason install.
#
# Why a total wipe rather than an API purge:
#   The Authoring API has no hard-delete — DELETE /scenarios/{id} only archives,
#   and archived rows (plus their published versions and snapshots) stay in the
#   database. It also spans no service boundary, so it cannot undo pollution in
#   Configuration, Play or the others. The only guaranteed-clean state is an
#   empty volume. `install-diapason.sh` is now idempotent (upsert by slug), so a
#   re-run against a *populated but healthy* instance needs no reset at all; this
#   script exists for the other case: an instance already polluted by the old,
#   non-idempotent flow, where orphan drafts must actually disappear.
#
# This DESTROYS ALL DATA in every GenEngine service (Identity included, so the
# administrator account is recreated here via bootstrap). It is a local
# developer tool, never a production procedure.
#
# See specs/domain/diapason/seeding.md.
set -euo pipefail

cd "$(dirname "$0")/.."

COMPOSE_FILES=(-f compose.yaml)
IDENTITY_URL="${IDENTITY_URL:-http://localhost:5203}"
CONFIGURATION_URL="${CONFIGURATION_URL:-http://localhost:5204}"
USER_NAME="${GENENGINE_ADMIN_USER_NAME:?GENENGINE_ADMIN_USER_NAME must be set}"
PASSWORD="${GENENGINE_ADMIN_PASSWORD:?GENENGINE_ADMIN_PASSWORD must be set}"
BOOTSTRAP_KEY="${GENENGINE_BOOTSTRAP_KEY:?GENENGINE_BOOTSTRAP_KEY must be set}"

for command in curl jq docker; do
  command -v "$command" >/dev/null || { echo "Missing required command: $command" >&2; exit 1; }
done

if [[ "${1:-}" != "--yes" ]]; then
  cat >&2 <<EOF
This wipes every GenEngine database volume (Authoring, Play, Identity,
Configuration, PlayerExperience, Organization) and reinstalls Diapason from
scratch. All local data is lost.

Re-run with --yes to proceed:
  $0 --yes
EOF
  exit 1
fi

echo "[1/5] Tear down the stack and delete its volumes"
docker compose "${COMPOSE_FILES[@]}" down --volumes

echo "[2/5] Bring the stack back up on empty databases"
docker compose "${COMPOSE_FILES[@]}" up --build --detach --wait

credentials=$(jq -n --arg u "$USER_NAME" --arg p "$PASSWORD" '{userName:$u,password:$p}')

echo "[3/5] Register the administrator account"
curl --fail --silent --show-error \
  -H 'Content-Type: application/json' \
  -d "$credentials" \
  "$IDENTITY_URL/auth/register" >/dev/null

echo "[4/5] Promote it to administrator (one-shot bootstrap)"
token=$(curl --fail --silent --show-error \
  -H 'Content-Type: application/json' \
  -d "$credentials" \
  "$IDENTITY_URL/auth/login" | jq -er '.token')
curl --fail --silent --show-error \
  -X POST \
  -H "Authorization: Bearer $token" \
  -H "X-Bootstrap-Key: $BOOTSTRAP_KEY" \
  "$IDENTITY_URL/admin/access/bootstrap" >/dev/null

echo "[5/5] Install the Diapason playable content"
exec ./scripts/install-diapason.sh
