#!/usr/bin/env bash
set -euo pipefail

IDENTITY_URL="${IDENTITY_URL:-http://localhost:5203}"
AUTHORING_URL="${AUTHORING_URL:-http://localhost:5201}"
PLAY_URL="${PLAY_URL:-http://localhost:5202}"
SCENARIO_FILE="${SCENARIO_FILE:-specs/domain/examples/critical-reflection-free-text.json}"
USER_NAME="free-text-smoke-$(date +%s)"
PASSWORD="LocalSmokePassword!2026"

credentials=$(jq -n --arg userName "$USER_NAME" --arg password "$PASSWORD" '{userName:$userName,password:$password}')
curl --fail --silent --show-error -H 'Content-Type: application/json' -d "$credentials" "$IDENTITY_URL/auth/register" >/dev/null
token=$(curl --fail --silent --show-error -H 'Content-Type: application/json' -d "$credentials" "$IDENTITY_URL/auth/login" | jq -er '.token')

scenario=$(curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' --data-binary "@$SCENARIO_FILE" "$AUTHORING_URL/scenarios/import")
scenario_id=$(jq -er '.id' <<<"$scenario")
revision=$(jq -er '.revision' <<<"$scenario")
published=$(curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"expectedRevision\":$revision}" "$AUTHORING_URL/scenarios/$scenario_id/publish")
version_id=$(jq -er '.id' <<<"$published")

session=$(curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"scenarioVersionId\":\"$version_id\",\"seed\":42}" "$PLAY_URL/sessions")
session_id=$(jq -er '.id' <<<"$session")
step=$(curl --fail --silent --show-error -H "Authorization: Bearer $token" "$PLAY_URL/sessions/$session_id/current-step")
jq -e '.status == "AwaitingExternalInput" and .kind == "FreeText" and .interactionId == "critical-method"' <<<"$step" >/dev/null

text_id=$(uuidgen | tr '[:upper:]' '[:lower:]')
analyzed=$(curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"commandId\":\"$text_id\",\"expectedRevision\":1,\"text\":\"Je compare la source originale.\"}" "$PLAY_URL/sessions/$session_id/text-inputs")
jq -e '.session.revision == 2 and .session.turn == 0 and .session.status == "AwaitingValidation" and .currentStep.pendingTextAnalysis.isAccepted == true' <<<"$analyzed" >/dev/null

replayed=$(curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"commandId\":\"$text_id\",\"expectedRevision\":1,\"text\":\"Je compare la source originale.\"}" "$PLAY_URL/sessions/$session_id/text-inputs")
jq -e '.replayed == true and .session.revision == 2' <<<"$replayed" >/dev/null

retry_id=$(uuidgen | tr '[:upper:]' '[:lower:]')
retry=$(curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"commandId\":\"$retry_id\",\"expectedRevision\":2,\"confirmed\":false}" "$PLAY_URL/sessions/$session_id/text-inputs/confirm")
jq -e '.session.revision == 3 and .session.turn == 0 and .session.status == "AwaitingExternalInput" and .currentStep.pendingTextAnalysis == null' <<<"$retry" >/dev/null

second_text_id=$(uuidgen | tr '[:upper:]' '[:lower:]')
curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"commandId\":\"$second_text_id\",\"expectedRevision\":3,\"text\":\"Je cherche une preuve et compare plusieurs sources.\"}" "$PLAY_URL/sessions/$session_id/text-inputs" >/dev/null

confirm_id=$(uuidgen | tr '[:upper:]' '[:lower:]')
completed=$(curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"commandId\":\"$confirm_id\",\"expectedRevision\":4,\"confirmed\":true}" "$PLAY_URL/sessions/$session_id/text-inputs/confirm")
jq -e '.session.revision == 5 and .session.turn == 1 and .session.status == "Completed" and .currentStep.kind == "Completed"' <<<"$completed" >/dev/null

echo "Free-text smoke test passed: input → analysis → retry → confirmation → effects → completion"
