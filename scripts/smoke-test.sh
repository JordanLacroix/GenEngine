#!/usr/bin/env bash
set -euo pipefail

IDENTITY_URL="${IDENTITY_URL:-http://localhost:5203}"
AUTHORING_URL="${AUTHORING_URL:-http://localhost:5201}"
PLAY_URL="${PLAY_URL:-http://localhost:5202}"
CONFIGURATION_URL="${CONFIGURATION_URL:-http://localhost:5204}"
PLAYER_EXPERIENCE_URL="${PLAYER_EXPERIENCE_URL:-http://localhost:5205}"
BOOTSTRAP_KEY="${GENENGINE_BOOTSTRAP_KEY:?GENENGINE_BOOTSTRAP_KEY must be set for the administrative smoke flow}"
TOKEN_FILE="${GENENGINE_SMOKE_TOKEN_FILE:-/tmp/genengine-smoke-token}"
SCENARIO_FILE="${SCENARIO_FILE:-specs/domain/examples/forest-choice.json}"
USER_NAME="smoke-$(date +%s)"
PASSWORD="LocalSmokePassword!2026"

for command in curl jq uuidgen; do
  command -v "$command" >/dev/null || { echo "Missing required command: $command" >&2; exit 1; }
done

for endpoint in "$IDENTITY_URL/health/ready" "$AUTHORING_URL/health/ready" "$PLAY_URL/health/ready" "$CONFIGURATION_URL/health/ready" "$PLAYER_EXPERIENCE_URL/health/ready"; do
  curl --fail --silent --show-error "$endpoint" >/dev/null
done

experience=$(curl --fail --silent --show-error "$CONFIGURATION_URL/experience/default")
jq -e '.version == 1 and .document.game.name != "" and (.document.categories | length) > 0 and (.document.familiars | length) > 0 and .document.economy.currencyCode == "BRAISE" and (.document.aiProviders[] | select(.type == "AzureAiFoundry") | .secretReference) == null' <<<"$experience" >/dev/null

credentials=$(jq -n --arg userName "$USER_NAME" --arg password "$PASSWORD" '{userName:$userName,password:$password}')
echo "[1/11] Register"
curl --fail --silent --show-error \
  -H 'Content-Type: application/json' \
  -d "$credentials" \
  "$IDENTITY_URL/auth/register" >/dev/null

echo "[2/11] Login"
token=$(curl --fail --silent --show-error \
  -H 'Content-Type: application/json' \
  -d "$credentials" \
  "$IDENTITY_URL/auth/login" | jq -er '.token')

curl --fail --silent --show-error \
  -X POST \
  -H "Authorization: Bearer $token" \
  -H "X-Bootstrap-Key: $BOOTSTRAP_KEY" \
  "$IDENTITY_URL/admin/access/bootstrap" >/dev/null
token=$(curl --fail --silent --show-error \
  -H 'Content-Type: application/json' \
  -d "$credentials" \
  "$IDENTITY_URL/auth/login" | jq -er '.token')
umask 077
printf '%s' "$token" > "$TOKEN_FILE"
me=$(curl --fail --silent --show-error -H "Authorization: Bearer $token" "$IDENTITY_URL/me")
jq -e '(.permissions | index("scenario.author")) != null and (.permissions | index("config.read")) != null' <<<"$me" >/dev/null
wallet=$(curl --fail --silent --show-error -H "Authorization: Bearer $token" "$PLAYER_EXPERIENCE_URL/me/experience?frontId=default")
jq -e '.currencyCode == "BRAISE" and .balance >= 0' <<<"$wallet" >/dev/null

echo "[3/11] Import scenario"
scenario=$(curl --fail-with-body --silent --show-error \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  --data-binary "@$SCENARIO_FILE" \
  "$AUTHORING_URL/scenarios/import")
scenario_id=$(jq -er '.id' <<<"$scenario")
revision=$(jq -er '.revision' <<<"$scenario")

echo "[4/11] Validate draft"
if ! validation=$(curl --fail-with-body --silent --show-error \
  -X POST \
  -H "Authorization: Bearer $token" \
  "$AUTHORING_URL/scenarios/$scenario_id/validate"); then
  echo "$validation" >&2
  exit 1
fi
jq -e '.isValid == true' <<<"$validation" >/dev/null

echo "[5/11] Analyze structure"
analysis=$(curl --fail-with-body --silent --show-error \
  -X POST \
  -H "Authorization: Bearer $token" \
  "$AUTHORING_URL/scenarios/$scenario_id/analyze")
jq -e '.loops == [] and .conditionalDeadEnds == [] and .unreachableEndingNodeIds == [] and .nodesWithoutEndingPath == []' <<<"$analysis" >/dev/null

echo "[6/11] Preview injected state"
preview=$(curl --fail-with-body --silent --show-error \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  -d '{"nodeId":"safe-end","turn":4,"logicalDay":7,"inventory":["author-map"],"characteristics":{"insight":2}}' \
  "$AUTHORING_URL/scenarios/$scenario_id/preview")
jq -e '.state.currentNodeId == "safe-end" and .state.turn == 4 and .state.world.logicalDay == 7 and .state.status == "Completed" and (.state.world.inventory | index("author-map")) != null and .currentStep.kind == "Completed"' <<<"$preview" >/dev/null

echo "[7/11] Publish snapshot"
if ! published=$(curl --fail-with-body --silent --show-error \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  -d "{\"expectedRevision\":$revision}" \
  "$AUTHORING_URL/scenarios/$scenario_id/publish"); then
  echo "$published" >&2
  exit 1
fi
version_id=$(jq -er '.id' <<<"$published")

echo "[8/11] Start session"
session=$(curl --fail-with-body --silent --show-error \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  -d "{\"scenarioVersionId\":\"$version_id\",\"seed\":42}" \
  "$PLAY_URL/sessions")
session_id=$(jq -er '.id' <<<"$session")

echo "[9/11] Pause and resume"
paused=$(curl --fail-with-body --silent --show-error \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  -d '{"expectedRevision":1}' \
  "$PLAY_URL/sessions/$session_id/pause")
jq -e '.status == "Paused" and .revision == 2' <<<"$paused" >/dev/null

resumed=$(curl --fail --silent --show-error \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  -d '{"expectedRevision":2}' \
  "$PLAY_URL/sessions/$session_id/resume")
jq -e '.status == "AwaitingInput" and .revision == 3' <<<"$resumed" >/dev/null

step=$(curl --fail --silent --show-error \
  -H "Authorization: Bearer $token" \
  "$PLAY_URL/sessions/$session_id/current-step")
choice_id=$(jq -er '.choices[0].id' <<<"$step")
command_id=$(uuidgen | tr '[:upper:]' '[:lower:]')
input=$(jq -n \
  --arg commandId "$command_id" \
  --arg choiceId "$choice_id" \
  '{commandId:$commandId,expectedRevision:3,choiceId:$choiceId}')

echo "[10/11] Submit choice"
result=$(curl --fail-with-body --silent --show-error \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  -d "$input" \
  "$PLAY_URL/sessions/$session_id/inputs")
jq -e '.session.status == "Completed" and .replayed == false' <<<"$result" >/dev/null

echo "[11/11] Replay command"
replayed=$(curl --fail-with-body --silent --show-error \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  -d "$input" \
  "$PLAY_URL/sessions/$session_id/inputs")
jq -e '.replayed == true' <<<"$replayed" >/dev/null

echo "Smoke test passed: register → login → import → validate → analyze → preview → publish → start → pause → resume → complete → replay"
