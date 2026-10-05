#!/bin/sh
# Dumps the Manifest database from the running compose stack into ./backups and
# keeps the most recent KEEP_DAYS days of them. Run it from cron on the VPS, e.g.
#
#   15 3 * * *  cd /srv/manifest && scripts/backup-postgres.sh >> backups/backup.log 2>&1
#
# Restore one into a fresh stack (the app stopped, the database empty):
#
#   docker compose -f docker-compose.prod.yml stop app worker
#   docker compose -f docker-compose.prod.yml exec -T postgres \
#     pg_restore -U manifest -d manifest --clean --if-exists < backups/<file>.dump
#   docker compose -f docker-compose.prod.yml start app worker
#
# scripts/verify-backup.sh test-restores the newest dump into a scratch database;
# run it weekly. Card art is not included: it is a cache of the official site's
# images and refills itself. docs/OPERATIONS.md has the off-site copy and the
# object storage notes.
#
# Each good run also writes backups/manifest_backup.prom, which the monitoring
# profile's node-exporter reads, so a backup that stops happening raises an alert.
set -eu

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
KEEP_DAYS="${KEEP_DAYS:-14}"
DIR="${BACKUP_DIR:-backups}"

mkdir -p "$DIR"
out="$DIR/manifest-$(date -u +%Y%m%d-%H%M%S).dump"

# -Fc: compressed, and restorable table by table with pg_restore. Written to a
# temporary name first so a failed dump never looks like a good one.
docker compose -f "$COMPOSE_FILE" exec -T postgres pg_dump -U manifest -Fc manifest > "$out.part"
mv "$out.part" "$out"
echo "$(date -u +%FT%TZ) wrote $out ($(wc -c < "$out") bytes)"

find "$DIR" -name 'manifest-*.dump' -mtime +"$KEEP_DAYS" -print -delete

# For the ManifestBackupStale alert. Written beside and renamed into place, so the
# exporter never reads half a file.
cat > "$DIR/manifest_backup.prom.part" <<PROM
# HELP manifest_backup_last_success_timestamp_seconds When the last database dump finished.
# TYPE manifest_backup_last_success_timestamp_seconds gauge
manifest_backup_last_success_timestamp_seconds $(date -u +%s)
# HELP manifest_backup_last_size_bytes Size of the last database dump.
# TYPE manifest_backup_last_size_bytes gauge
manifest_backup_last_size_bytes $(wc -c < "$out")
PROM
mv "$DIR/manifest_backup.prom.part" "$DIR/manifest_backup.prom"
