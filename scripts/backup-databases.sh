#!/usr/bin/env bash
#
# backup-databases.sh -- encrypted logical backups of the three GenEngine
# PostgreSQL databases (authoring, identity, play).
#
# For each database it runs `pg_dump -Fc` (custom format) *inside* the running
# postgres container and pipes the dump through `age` on the host, writing a
# timestamped, encrypted file to the backups directory. Plaintext dumps never
# touch the host disk.
#
# Encryption mode is chosen from the environment (see scripts/lib/age-crypto.sh):
#   * BACKUP_AGE_PASSPHRASE          -> symmetric passphrase mode (local dev)
#   * BACKUP_AGE_RECIPIENTS[_FILE]   -> asymmetric recipients mode (production)
# No key or passphrase is ever read from the command line or committed.
#
# Usage:
#   BACKUP_AGE_PASSPHRASE='dev-pass' scripts/backup-databases.sh
#
# Environment overrides:
#   BACKUP_DIR      output directory        (default: <repo>/backups)
#   COMPOSE_FILE    compose file to use     (default: <repo>/compose.yaml)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
# shellcheck source=scripts/lib/age-crypto.sh
. "$SCRIPT_DIR/lib/age-crypto.sh"

BACKUP_DIR="${BACKUP_DIR:-$REPO_ROOT/backups}"
COMPOSE_FILE="${COMPOSE_FILE:-$REPO_ROOT/compose.yaml}"

# service:database:user for each of the three autonomous services.
DATABASES=(
  "authoring-db:genengine_authoring:authoring"
  "identity-db:genengine_identity:identity"
  "play-db:genengine_play:play"
)

age_require_tool
mode="$(age_mode)"

for tool in docker python3; do
  command -v "$tool" >/dev/null 2>&1 || { echo "error: required tool '$tool' not found on PATH." >&2; exit 127; }
done

mkdir -p "$BACKUP_DIR"
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"

echo "GenEngine encrypted database backup"
echo "  mode        : $mode"
echo "  output dir  : $BACKUP_DIR"
echo "  timestamp   : $timestamp (UTC)"
echo

written=()
for entry in "${DATABASES[@]}"; do
  IFS=':' read -r service database user <<<"$entry"
  out="$BACKUP_DIR/${service}-${timestamp}.dump.age"

  echo "-> $service ($database)"
  # pg_dump (custom format) inside the container -> age on the host -> file.
  # set -o pipefail (from the lib) makes a failing pg_dump abort the pipeline.
  if ! docker compose -f "$COMPOSE_FILE" exec -T "$service" \
        pg_dump -U "$user" -d "$database" -Fc \
      | age_encrypt "$out"; then
    echo "error: backup failed for $service" >&2
    rm -f "$out"
    exit 1
  fi

  size="$(wc -c <"$out" | tr -d ' ')"
  if [ "$size" -le 0 ]; then
    echo "error: produced an empty backup for $service" >&2
    rm -f "$out"
    exit 1
  fi
  echo "   wrote $(basename "$out") (${size} bytes, encrypted)"
  written+=("$out")
done

echo
echo "Done. ${#written[@]} encrypted backups written to $BACKUP_DIR:"
for f in "${written[@]}"; do
  echo "  $(basename "$f")"
done
