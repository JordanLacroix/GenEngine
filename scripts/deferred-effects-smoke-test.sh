#!/usr/bin/env bash
set -euo pipefail

IDENTITY_URL="${IDENTITY_URL:-http://localhost:5203}"
AUTHORING_URL="${AUTHORING_URL:-http://localhost:5201}"
PLAY_URL="${PLAY_URL:-http://localhost:5202}"
SCENARIO_FILE="${SCENARIO_FILE:-specs/domain/examples/deferred-signal.json}"
USER_NAME="deferred-smoke-$(date +%s)"
PASSWORD="LocalSmokePassword!2026"
TOKEN_FILE="${GENENGINE_SMOKE_TOKEN_FILE:-/tmp/genengine-smoke-token}"

if [[ -s "$TOKEN_FILE" ]]; then
  token=$(<"$TOKEN_FILE")
else
  credentials=$(jq -n --arg userName "$USER_NAME" --arg password "$PASSWORD" '{userName:$userName,password:$password}')
  curl --fail --silent --show-error -H 'Content-Type: application/json' -d "$credentials" "$IDENTITY_URL/auth/register" >/dev/null
  token=$(curl --fail --silent --show-error -H 'Content-Type: application/json' -d "$credentials" "$IDENTITY_URL/auth/login" | jq -er '.token')
fi

scenario=$(curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' --data-binary "@$SCENARIO_FILE" "$AUTHORING_URL/scenarios/import")
scenario_id=$(jq -er '.id' <<<"$scenario")
revision=$(jq -er '.revision' <<<"$scenario")
published=$(curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"expectedRevision\":$revision}" "$AUTHORING_URL/scenarios/$scenario_id/publish")
version_id=$(jq -er '.id' <<<"$published")

session=$(curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"scenarioVersionId\":\"$version_id\",\"seed\":42}" "$PLAY_URL/sessions")
session_id=$(jq -er '.id' <<<"$session")

first_id=$(uuidgen | tr '[:upper:]' '[:lower:]')
curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"commandId\":\"$first_id\",\"expectedRevision\":1,\"choiceId\":\"investigate\"}" "$PLAY_URL/sessions/$session_id/inputs" >/dev/null
waiting=$(curl --fail --silent --show-error -H "Authorization: Bearer $token" "$PLAY_URL/sessions/$session_id/player")
jq -e '.summary.turn == 1 and .summary.logicalDay == 2 and .summary.pendingDeferredEffectCount == 2 and (.collection.rewards | index("decoded-signal")) == null' <<<"$waiting" >/dev/null

second_id=$(uuidgen | tr '[:upper:]' '[:lower:]')
curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"commandId\":\"$second_id\",\"expectedRevision\":2,\"choiceId\":\"decode\"}" "$PLAY_URL/sessions/$session_id/inputs" >/dev/null
completed=$(curl --fail --silent --show-error -H "Authorization: Bearer $token" "$PLAY_URL/sessions/$session_id/player")
jq -e '.summary.status == "Completed" and .summary.turn == 2 and .summary.logicalDay == 2 and .summary.pendingDeferredEffectCount == 0 and (.collection.evidence | index("signal-fragment")) != null and (.collection.rewards | index("decoded-signal")) != null and (.journal[0].label == "Le rendez-vous est arrivé.")' <<<"$completed" >/dev/null

echo "Deferred effect smoke test passed: logical day → pending condition → evidence → ordered triggers → player projection"
