#!/bin/sh
# Proves a dump can be restored: loads it into a scratch database beside the
# live one, compares row counts table by table, and drops the scratch copy. Run
# it weekly from cron, after the nightly backup:
#
#   45 3 * * 0  cd /srv/manifest && scripts/verify-backup.sh >> backups/backup.log 2>&1
#
#   scripts/verify-backup.sh                      # the newest dump in backups/
#   scripts/verify-backup.sh backups/<file>.dump  # a particular one
#
# The live database is only read. A restored table may hold fewer rows than the
# live one - it is a snapshot from earlier - but one that comes back empty while
# the live table has rows, or does not come back at all, fails the check.
set -eu

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
DIR="${BACKUP_DIR:-backups}"
SCRATCH=manifest_restore_check

dump="${1:-$(ls -1t "$DIR"/manifest-*.dump 2>/dev/null | head -n 1)}"
if [ -z "$dump" ] || [ ! -s "$dump" ]; then
  echo "verify-backup: no dump to check (looked in $DIR)" >&2
  exit 1
fi

psql() {
  docker compose -f "$COMPOSE_FILE" exec -T -e PGOPTIONS="-c client_min_messages=warning" postgres \
    psql -U manifest -v ON_ERROR_STOP=1 -qAt "$@"
}

psql -d postgres -c "DROP DATABASE IF EXISTS $SCRATCH WITH (FORCE)"
psql -d postgres -c "CREATE DATABASE $SCRATCH"
trap 'psql -d postgres -c "DROP DATABASE IF EXISTS $SCRATCH WITH (FORCE)" >/dev/null 2>&1 || true' EXIT

started=$(date +%s)
docker compose -f "$COMPOSE_FILE" exec -T postgres \
  pg_restore -U manifest -d "$SCRATCH" --no-owner --exit-on-error < "$dump"
took=$(( $(date +%s) - started ))

tables="catalog users sessions access_requests collection decks deck_cards scan_log prices card_images jobs schema_migrations"
failed=0
printf '%-18s %10s %10s\n' table live restored
for t in $tables; do
  live=$(psql -d manifest -c "SELECT count(*) FROM $t" 2>/dev/null || echo missing)
  restored=$(psql -d "$SCRATCH" -c "SELECT count(*) FROM $t" 2>/dev/null || echo missing)
  printf '%-18s %10s %10s\n' "$t" "$live" "$restored"
  if [ "$live" = missing ]; then continue; fi
  if [ "$restored" = missing ]; then failed=1; continue; fi
  # jobs and sessions churn on their own; an empty restored copy of either is fine.
  case "$t" in jobs|sessions) continue ;; esac
  if [ "$live" -gt 0 ] && [ "$restored" -eq 0 ]; then failed=1; fi
done

if [ "$failed" -ne 0 ]; then
  echo "$(date -u +%FT%TZ) verify-backup: FAILED for $dump" >&2
  exit 1
fi

echo "$(date -u +%FT%TZ) verify-backup: $dump restores cleanly (${took}s)"
cat > "$DIR/manifest_backup_verify.prom.part" <<PROM
# HELP manifest_backup_last_verified_timestamp_seconds When a dump last test-restored cleanly.
# TYPE manifest_backup_last_verified_timestamp_seconds gauge
manifest_backup_last_verified_timestamp_seconds $(date -u +%s)
# HELP manifest_backup_restore_seconds How long that test restore took.
# TYPE manifest_backup_restore_seconds gauge
manifest_backup_restore_seconds $took
PROM
mv "$DIR/manifest_backup_verify.prom.part" "$DIR/manifest_backup_verify.prom"
