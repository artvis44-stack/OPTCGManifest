#!/bin/sh
# Runs the k6 load test from Docker, so nothing needs installing. Against the
# local stack (docker compose up, with MANIFEST_INVITE_CODE in .env):
#
#   INVITE=<the invite code> loadtest/run.sh
#
# Against a staging server - never the live one; it makes 1,000 accounts:
#
#   BASE_URL=https://staging.example.com INVITE=<code> loadtest/run.sh
#
# Shorter or smaller runs: DURATION=1m USERS=200 ACTIVE=40 loadtest/run.sh.
# The summary lands in loadtest/results/. k6 exits non-zero when a target is
# missed, so this can gate a deploy.
set -eu
cd "$(dirname "$0")"
mkdir -p results
stamp=$(date -u +%Y%m%d-%H%M%S)

# --network host so localhost means this machine; :z for SELinux hosts.
exec docker run --rm --network host \
  -u "$(id -u):$(id -g)" \
  -v "$PWD:/scripts:z" -w /scripts \
  -e BASE_URL="${BASE_URL:-http://localhost:8420}" \
  -e INVITE="${INVITE:-}" \
  -e USERS="${USERS:-1000}" \
  -e ACTIVE="${ACTIVE:-100}" \
  -e DURATION="${DURATION:-5m}" \
  -e RAMP="${RAMP:-1m}" \
  -e CONTENTION="${CONTENTION:-2000}" \
  -e IMAGE_CARDS="${IMAGE_CARDS:-24}" \
  -e USER_PREFIX="${USER_PREFIX:-load}" \
  grafana/k6:1.3.0 run --summary-export "results/summary-$stamp.json" manifest.js "$@"
