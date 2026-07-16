#!/usr/bin/env bash
set -euo pipefail

IDENTITY_URL="${IDENTITY_URL:-http://localhost:5203}"
AUTHORING_URL="${AUTHORING_URL:-http://localhost:5201}"
PLAY_URL="${PLAY_URL:-http://localhost:5202}"
SCENARIO_FILE="${SCENARIO_FILE:-specs/domain/examples/forest-choice.json}"
USER_NAME="smoke-$(date +%s)"
PASSWORD="LocalSmokePassword!2026"

for command in curl jq uuidgen; do
  command -v "$command" >/dev/null || { echo "Missing required command: $command" >&2; exit 1; }
done

for endpoint in "$IDENTITY_URL/health/ready" "$AUTHORING_URL/health/ready" "$PLAY_URL/health/ready"; do
  curl --fail --silent --show-error "$endpoint" >/dev/null
done

credentials=$(jq -n --arg userName "$USER_NAME" --arg password "$PASSWORD" '{userName:$userName,password:$password}')
echo "[1/9] Register"
curl --fail --silent --show-error \
  -H 'Content-Type: application/json' \
  -d "$credentials" \
  "$IDENTITY_URL/auth/register" >/dev/null

echo "[2/9] Login"
token=$(curl --fail --silent --show-error \
  -H 'Content-Type: application/json' \
  -d "$credentials" \
  "$IDENTITY_URL/auth/login" | jq -er '.token')

echo "[3/9] Import scenario"
scenario=$(curl --fail-with-body --silent --show-error \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  --data-binary "@$SCENARIO_FILE" \
  "$AUTHORING_URL/scenarios/import")
scenario_id=$(jq -er '.id' <<<"$scenario")
revision=$(jq -er '.revision' <<<"$scenario")

echo "[4/9] Validate draft"
if ! validation=$(curl --fail-with-body --silent --show-error \
  -X POST \
  -H "Authorization: Bearer $token" \
  "$AUTHORING_URL/scenarios/$scenario_id/validate"); then
  echo "$validation" >&2
  exit 1
fi
jq -e '.isValid == true' <<<"$validation" >/dev/null

echo "[5/9] Publish snapshot"
if ! published=$(curl --fail-with-body --silent --show-error \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  -d "{\"expectedRevision\":$revision}" \
  "$AUTHORING_URL/scenarios/$scenario_id/publish"); then
  echo "$published" >&2
  exit 1
fi
version_id=$(jq -er '.id' <<<"$published")

echo "[6/9] Start session"
session=$(curl --fail-with-body --silent --show-error \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  -d "{\"scenarioVersionId\":\"$version_id\",\"seed\":42}" \
  "$PLAY_URL/sessions")
session_id=$(jq -er '.id' <<<"$session")

echo "[7/9] Pause and resume"
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

echo "[8/9] Submit choice"
result=$(curl --fail-with-body --silent --show-error \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  -d "$input" \
  "$PLAY_URL/sessions/$session_id/inputs")
jq -e '.session.status == "Completed" and .replayed == false' <<<"$result" >/dev/null

echo "[9/9] Replay command"
replayed=$(curl --fail-with-body --silent --show-error \
  -H "Authorization: Bearer $token" \
  -H 'Content-Type: application/json' \
  -d "$input" \
  "$PLAY_URL/sessions/$session_id/inputs")
jq -e '.replayed == true' <<<"$replayed" >/dev/null

echo "Smoke test passed: register → login → import → validate → publish → start → pause → resume → complete → replay"
