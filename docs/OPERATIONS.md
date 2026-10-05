# Running Manifest in production

This covers what to watch, what each alert means, how the data is backed up and
restored, and how to check the site still meets its load target. The deployment
itself is in the README under "Deploying to a VPS". Work through
[LAUNCH_CHECKLIST.md](LAUNCH_CHECKLIST.md) before inviting the public in.

## Metrics

Every app and worker process can serve Prometheus metrics on a port of its own:

```
MANIFEST_METRICS_LISTEN=9464          # this machine only
MANIFEST_METRICS_LISTEN=0.0.0.0:9464  # every interface, as the compose files set it
```

That port is a separate listener answering `GET /metrics` and nothing else.
In the compose stacks it is reachable on the internal network only, because
Caddy forwards 8420 and no other port. So the numbers are never public, and
there's no token to manage. Don't publish 9464 on a host port.

The monitoring stack is opt-in:

```
docker compose -f docker-compose.prod.yml --profile monitoring up -d
ssh -L 9090:127.0.0.1:9090 -L 9093:127.0.0.1:9093 <server>   # then open localhost:9090
```

That starts Prometheus (30 days of history), Alertmanager and node-exporter.
Prometheus finds every app and worker replica through Docker's DNS, so
`APP_REPLICAS` and `WORKER_REPLICAS` can change without editing anything. To get
alerts by mail, set `MANIFEST_ALERT_EMAIL` in `.env`. Alertmanager reuses the
app's `MANIFEST_SMTP_*` settings. Without it, alerts appear only at
`localhost:9093`.

Locally, `docker compose --profile monitoring up` adds a Prometheus at
http://localhost:9090 with the same rules. It's useful during a load test.

### What is reported

| Metric | Labels | What it is |
|---|---|---|
| `manifest_http_requests_total` | method, route, status | Requests answered. `route` is the route template (`/api/decks/{id:long}`), or `unmatched` for unknown paths, so a scanner can't create new series |
| `manifest_http_request_duration_seconds` | method, route | Histogram. Buckets include 0.3 s and 0.5 s, the launch targets |
| `manifest_rate_limited_total` | route | Requests refused with 429 |
| `manifest_auth_attempts_total` | kind (login, register), outcome (ok, refused, throttled) | Sign-in and sign-up attempts |
| `manifest_jobs_total` | type, outcome (done, retry, failed) | Jobs finished. `failed` means out of attempts |
| `manifest_job_duration_seconds` | type | How long a job ran |
| `manifest_jobs_queued`, `_running`, `_failed` | | The queue, read from the jobs table. `queued` counts only jobs that are due |
| `manifest_jobs_oldest_due_seconds` | | How long the oldest due job has waited. This is the backlog signal |
| `manifest_scan_duration_seconds` | mode (sync, async), outcome | Time to read a scan. Async includes only the read, not the queue wait |
| `manifest_image_requests_total` | state (found, pending, missing) | Card art served, queued or unavailable |
| `manifest_image_fetches_total` | outcome (stored, already, failed, missing) | Fetches from the official card site |
| `manifest_database_up` | | 1 if the last queue query reached the database |
| `manifest_db_command_duration_seconds` | | Every PostgreSQL command, from Npgsql's own instrumentation |
| `manifest_db_command_failures_total`, `manifest_db_connections{state}`, `manifest_db_connection_waiters`, `manifest_db_connection_timeouts_total` | | Npgsql's failure counts and connection-pool state |
| `manifest_process_*`, `manifest_dotnet_*` | | Memory, CPU, GC heap and thread pool |
| `manifest_backup_*` | | Written by the backup scripts into `backups/*.prom` and read by node-exporter |

All of these are per process, and Prometheus adds `job` and `instance`. Every
process reports the same queue gauges because they read the same table, so
aggregate them with `max()`, not `sum()`. On SQLite there are no `manifest_db_*`
series, because only Npgsql publishes them.

Some useful queries:

```promql
# p95 read latency, all routes
histogram_quantile(0.95, sum by (le) (rate(manifest_http_request_duration_seconds_bucket{method="GET"}[5m])))
# slowest routes at p95
topk(5, histogram_quantile(0.95, sum by (route, le) (rate(manifest_http_request_duration_seconds_bucket[5m]))))
# requests per second by status
sum by (status) (rate(manifest_http_requests_total[5m]))
# jobs by outcome over the last hour
sum by (type, outcome) (increase(manifest_jobs_total[1h]))
```

## Alerts

The rules are in `docker/alerts.yml`. After editing them, check with
`docker run --rm -v ./docker:/d:ro --entrypoint promtool prom/prometheus check rules /d/alerts.yml`.

| Alert | Fires when | First thing to do |
|---|---|---|
| ManifestTargetDown | a replica's `/metrics` hasn't answered for 2 min | `docker compose -f docker-compose.prod.yml ps`, then that container's logs |
| ManifestNoAppRunning / NoWorkerRunning | no replica of one kind is up | `docker compose ... up -d`. With no worker, scans, mail and art all stop |
| ManifestDatabaseUnreachable | the queue query has been failing for 1 min | check the postgres container, its disk, `pg_isready` |
| ManifestDatabaseErrors | more than 10 failed commands in 5 min | app log: look for `Npgsql` exceptions |
| ManifestDatabasePoolExhausted / ConnectionTimeouts | requests are waiting for one of the 100 pooled connections per process | find the slow query (`pg_stat_activity`) before raising the pool size |
| ManifestServerErrors | more than 2% of requests return 5xx for 5 min | app log. Every 500 is logged with its `request_id` |
| ManifestSlowReads / SlowWrites | p95 over 300 ms (reads) or 500 ms (writes) for 10 min | the "slowest routes" query above, then `ManifestSlowDatabase` |
| ManifestSlowDatabase | p95 command time over 100 ms | `pg_stat_statements`, missing index, disk I/O |
| ManifestThreadPoolStarved | over 50 work items waiting for threads | something is blocking threads: a sync call to a slow upstream |
| ManifestQueueBacklog / QueueStalled | the oldest due job has waited over 2 min (warning) or 15 min (critical) | worker logs, `SELECT type, count(*) FROM jobs WHERE status='running' GROUP BY 1`. Raise `WORKER_REPLICAS` |
| ManifestJobsFailing | more than 5 jobs of one type ran out of attempts in an hour | the query in the alert shows each `last_error` |
| ManifestMailFailing | SendEmail retries or failures | check the SMTP credentials. Access requests and invite links are stuck |
| ManifestSlowScans | p95 OCR read over 10 s | worker CPU. Tesseract is CPU-bound |
| ManifestCardArtFetchesFailing | over 50 failures an hour, outnumbering successes | is en.onepiece-cardgame.com up, and are we blocked? Art degrades gracefully, nothing else breaks |
| ManifestSignInFailureSpike / RateLimitingHeavy | over 100 refused sign-ins, or over 200 rate-limited requests, in 15 min | probably password guessing. The throttle is working, so watch whether it is one address or many |
| HostDiskFilling / AlmostFull / FullWithin24h | under 15% (warning) or 5% (critical) free, or full within a day at the current rate | `docker system df`, `docker image prune`, old `backups/`. PostgreSQL stops writing when the disk is full |
| HostMemoryLow | under 10% available for 10 min | `docker stats`. Reduce replicas or resize the VPS |
| ManifestBackupMissing / Stale | no successful dump recorded, or the last one is over 26 h old | run `scripts/backup-postgres.sh` by hand and read its output, then check the cron line |
| ManifestBackupNotVerified | no test restore in 8 days | `scripts/verify-backup.sh` |

## Backups and restore

### What needs backing up

| Data | Where | Backed up? |
|---|---|---|
| Accounts, collections, decks, scan history, access requests | PostgreSQL (`pg-data` volume) | **Yes.** `scripts/backup-postgres.sh`, nightly |
| Settings and secrets | `.env` on the server | **Yes, by hand**, into a password manager. It isn't in git |
| Card art | object storage (`s3-data` volume, or a hosted bucket) | Optional. See below |
| Sign-in throttle counters | Redis | No. They're short-lived on purpose and a restart only resets them |
| TLS certificates | Caddy's `caddy-data` volume | No. Caddy fetches new ones automatically |
| Queued jobs | PostgreSQL `jobs` table | Included in the database dump |

### Nightly dumps, a weekly test restore, and an off-site copy

On the VPS (`crontab -e`, with the repository at `/srv/manifest`):

```
15 3 * * *  cd /srv/manifest && scripts/backup-postgres.sh >> backups/backup.log 2>&1
45 3 * * 0  cd /srv/manifest && scripts/verify-backup.sh  >> backups/backup.log 2>&1
30 3 * * *  rclone sync /srv/manifest/backups offsite:manifest-backups --include 'manifest-*.dump'
```

- `backup-postgres.sh` writes `backups/manifest-<UTC time>.dump` (`pg_dump -Fc`),
  keeps 14 days of dumps (`KEEP_DAYS`), and records its success in
  `backups/manifest_backup.prom` for the staleness alert.
- `verify-backup.sh` restores the newest dump into a scratch database next to the
  live one, compares row counts table by table, and drops the copy. It records
  success in `backups/manifest_backup_verify.prom`. It only reads the live
  database. A failure exits non-zero and lists the table that didn't come back.
- **Off-site:** a dump that only lives on the VPS disappears with the VPS. Any
  `rclone` remote works, such as a B2 or R2 bucket or another server over SFTP. Use
  a bucket with object versioning or a retention lock if you can, so a
  compromised server can't delete its own history.

### Restoring

Into the same stack, for example after a bad migration or a mistaken bulk delete:

```
docker compose -f docker-compose.prod.yml stop app worker
docker compose -f docker-compose.prod.yml exec -T postgres \
  pg_restore -U manifest -d manifest --clean --if-exists < backups/<file>.dump
docker compose -f docker-compose.prod.yml start app worker
```

Onto a new server: follow the README deploy steps up to and including
`docker compose ... up -d postgres`, restore with the same `pg_restore` line,
then start the rest. Copy `.env` across first, because the app needs the same
`MANIFEST_DOMAIN`. Sessions are in the dump, so people stay signed in.
Anything written after the dump was taken is lost. Plan on losing up to a day
with nightly backups.

Practise a restore once before launch, and time it. `verify-backup.sh` prints
how long its restore took.

### Object storage

The bucket holds card art only, as `cards/<card-id>.png`. All of it is a copy
of images on the official card site, and the app re-fetches anything missing
the first time someone looks at it. **Losing the bucket loses nothing that
can't be rebuilt.** The pictures just fill in again over the next few page
views. So the database backup above is the one that matters.

You might still copy it to skip the slow re-fetch after a move:

- **Bundled SeaweedFS:** `docker run --rm -v manifest_s3-data:/data:ro -v "$PWD":/out alpine tar czf /out/s3-data.tgz -C /data .`
  (check the volume name with `docker volume ls`). Restore by extracting into the new
  volume before starting the `s3` service.
- **Hosted bucket (R2, B2, S3):** nothing to do day to day. To move providers,
  `rclone sync old:manifest-art new:manifest-art`.

If anything that can't be regenerated ever goes into the bucket, such as
user uploads or kept debug scans, this changes: turn on bucket versioning and
add an `rclone sync` to the cron jobs above.

## Load testing

`loadtest/manifest.js` is a k6 script for the public-launch target in
SCALING_PLAN.md:

- 1,000 registered users, 100 of them active at once;
- p95 under 300 ms for reads (search, collection, decks, card art);
- p95 under 500 ms for writes (logging, deck edits, scan submission);
- no lost increments: 20 clients add +1 to the same card of one account 2,000
  times, and the count must come out exact;
- scans complete, submission through to the worker's answer, in under 5 s at
  p95.

The 100 active users are split roughly as people use the app. Half browse and
search, a quarter log cards, and the rest build decks, load card art and scan.
Each waits one to three seconds between actions. Card art uses a fixed set of
24 cards, fetched once from the card site in `setup()`, so the test measures
this server rather than theirs.

```
docker compose --profile monitoring up -d --build      # with MANIFEST_INVITE_CODE in .env
INVITE=<that code> loadtest/run.sh                     # k6 from Docker; nothing to install
DURATION=1m USERS=200 ACTIVE=40 INVITE=... loadtest/run.sh   # a quick one
```

`BASE_URL` points it at a staging server. **Never point it at the live site.**
It creates `USERS` accounts called `load-0000` onwards, plus `load-shared`, and
adds cards to them. k6 exits non-zero when any target is missed, and the
summary is saved in `loadtest/results/`. Watch Prometheus during the run to see
which route or job gives first.

### Latest result

Run on 2026-10-05 with the local `docker-compose.yml` stack on one workstation
(Ryzen 5 5500, 12 threads, 32 GB). That's **one** app container and one worker
on PostgreSQL 16, Redis and SeaweedFS, with k6 on the same machine. The stack
was the defaults: 1,000 users, 100 active, a 1-minute ramp, 5 minutes held and
2,000 contended increments. Every threshold passed.

| | p95 | p99 | Target |
|---|---|---|---|
| Reads (search, collection, stats, decks) | 7.6 ms | 10.2 ms | < 300 ms |
| Writes (logging, bulk, deck edits, scan submit) | 12.6 ms | 64.6 ms | < 500 ms |
| Card art | 13.9 ms | 19.4 ms | < 300 ms |
| Scan, submit to answer | 1.01 s | 1.26 s | < 5 s |
| Lost increments | 0 of 2,000 | | 0 |

That was 29,861 requests at about 76/s, with no failed read, write or image
request. The app container peaked at about 25% of one core and 140 MB. The
headroom is large. The real VPS will have fewer, slower cores than this
workstation, so repeat the run on staging at the production size before launch.

Scan time is mostly the worker's one-second poll of the queue: the OCR itself
took about 130 ms at p95 (`manifest_scan_duration_seconds`). A web process
queuing a scan for an external worker has no way to wake it sooner. A
PostgreSQL `LISTEN`/`NOTIFY` on enqueue would cut that second if it ever
matters.
