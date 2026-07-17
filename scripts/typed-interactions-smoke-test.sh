#!/usr/bin/env bash
set -euo pipefail

IDENTITY_URL="${IDENTITY_URL:-http://localhost:5203}"
AUTHORING_URL="${AUTHORING_URL:-http://localhost:5201}"
PLAY_URL="${PLAY_URL:-http://localhost:5202}"
SCENARIO_FILE="${SCENARIO_FILE:-specs/domain/examples/critical-reading-interactions.json}"
USER_NAME="typed-smoke-$(date +%s)"
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
jq -e '.kind == "Narration" and .interactionId == "intro"' <<<"$step" >/dev/null

continue_id=$(uuidgen | tr '[:upper:]' '[:lower:]')
continued=$(curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"commandId\":\"$continue_id\",\"expectedRevision\":1}" "$PLAY_URL/sessions/$session_id/continue")
jq -e '.session.revision == 2 and .currentStep.kind == "Quiz"' <<<"$continued" >/dev/null

answer_id=$(uuidgen | tr '[:upper:]' '[:lower:]')
answered=$(curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"commandId\":\"$answer_id\",\"expectedRevision\":2,\"answerId\":\"fact\"}" "$PLAY_URL/sessions/$session_id/answers")
jq -e '.session.revision == 3 and .currentStep.kind == "ChoiceSet"' <<<"$answered" >/dev/null

choice_id=$(uuidgen | tr '[:upper:]' '[:lower:]')
chosen=$(curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"commandId\":\"$choice_id\",\"expectedRevision\":3,\"choiceId\":\"conclude\"}" "$PLAY_URL/sessions/$session_id/inputs")
jq -e '.session.revision == 4 and .currentStep.kind == "Narration" and .currentStep.interactionId == "outro"' <<<"$chosen" >/dev/null

ending_id=$(uuidgen | tr '[:upper:]' '[:lower:]')
completed=$(curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"commandId\":\"$ending_id\",\"expectedRevision\":4}" "$PLAY_URL/sessions/$session_id/continue")
jq -e '.session.revision == 5 and .session.status == "Completed" and .currentStep.kind == "Completed"' <<<"$completed" >/dev/null

replayed=$(curl --fail-with-body --silent --show-error -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d "{\"commandId\":\"$ending_id\",\"expectedRevision\":4}" "$PLAY_URL/sessions/$session_id/continue")
jq -e '.replayed == true and .session.revision == 5' <<<"$replayed" >/dev/null

echo "Typed interaction smoke test passed: narration → quiz → choice → ending → replay"

