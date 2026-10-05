# Launch checklist

Go through this before the site is opened to people you don't know. Each item
has a way to check it. Details are in [OPERATIONS.md](OPERATIONS.md) and the
README's "Deploying to a VPS".

## Before the day

### Server and network

- [ ] DNS A (and AAAA) records point at the VPS. `dig +short <domain>` shows its address.
- [ ] Only 22, 80 and 443 are open from outside. `ss -ltnp` on the server shows
      nothing but Caddy and sshd on public addresses, and `nmap <domain>` from
      elsewhere agrees. 5432, 6379, 8333, 8420, 9090, 9093 and 9464 must not
      be reachable.
- [ ] SSH accepts keys only (`PasswordAuthentication no`), and unattended
      security updates are on.
- [ ] The disk has room for 30 days of growth plus 14 nightly dumps. Check with
      `df -h /`.

### Configuration

- [ ] `.env` is filled from `.env.example`. `POSTGRES_PASSWORD` and
      `MANIFEST_OBJECT_STORAGE_SECRET_KEY` are long random values
      (`openssl rand -hex 24`), and `.env` is saved in a password manager.
- [ ] How people get accounts is decided. Either an invite code of 16 or more
      characters, or the request-and-approve flow with `MANIFEST_ADMIN_EMAIL`
      and working SMTP. The app refuses to start in Production with a short
      code or with a missing SMTP setup.
- [ ] Mail actually arrives. Submit an access request from the sign-in page and
      check that the admin mail and the approval link both land. Check that they
      aren't in spam: SPF and DKIM must be set up for the From domain.
- [ ] `docker compose -f docker-compose.prod.yml up -d --build` comes up
      clean, and `curl -s https://<domain>/api/health/ready` shows every check
      as `ok` and `"environment":"Production"`.
- [ ] The owner account exists, and an existing collection has been migrated
      with `migrate-sqlite` if there is one. Sign in and spot-check a few counts.

### Security

- [ ] https works and http redirects to it. `curl -sI http://<domain>` returns
      308 to https, and the certificate is from Let's Encrypt.
- [ ] `curl -sI https://<domain>/` shows `Strict-Transport-Security`,
      `Content-Security-Policy`, `X-Frame-Options: DENY` and
      `X-Content-Type-Options: nosniff`.
- [ ] The session cookie is `Secure; HttpOnly; SameSite=Lax`. Check in the
      browser's dev tools after signing in.
- [ ] The sign-in throttle is shared. With two app replicas, nine wrong
      passwords in a row get a 429 whichever replica answers. The
      `/api/health/ready` Redis check is `ok`.
- [ ] `/metrics` isn't public. `curl -s https://<domain>/metrics` returns the
      app's 404, not metric text.
- [ ] The repository on the server has no `manifest.db`, `*.pem` or `.env`
      committed into it. `git status --ignored` should show them only as
      ignored files.

### Backups

- [ ] Both cron lines are installed (`crontab -l`): the nightly
      `backup-postgres.sh` and the weekly `verify-backup.sh`.
- [ ] One backup has been run by hand, and `backups/manifest-*.dump` exists and
      isn't tiny.
- [ ] `scripts/verify-backup.sh` passes, and you've noted how long the restore
      took: ____ s.
- [ ] Off-site copies are going somewhere the server can't delete, and one has
      been downloaded and checked.
- [ ] A full restore has been practised once onto a scratch VPS or a local
      `docker compose` stack, following "Restoring" in OPERATIONS.md.

### Monitoring

- [ ] `docker compose -f docker-compose.prod.yml --profile monitoring up -d` is
      running. In Prometheus (over the SSH tunnel), Status → Targets shows
      every app replica, the worker and node as UP.
- [ ] `MANIFEST_ALERT_EMAIL` is set and a test alert has arrived. Stopping the
      worker for 6 minutes fires ManifestNoWorkerRunning. Start it again and
      the resolved mail follows.
- [ ] `manifest_backup_last_success_timestamp_seconds` shows up in
      Prometheus, which means node-exporter is reading `backups/`.
- [ ] There's an external uptime check from outside the VPS, for example
      UptimeRobot or Healthchecks, polling
      `https://<domain>/api/health/ready`. Prometheus can't report that the
      whole server is down.

### Capacity

- [ ] The load test passes against a staging copy of the production stack
      with the same VPS size, compose file and replicas:
      `BASE_URL=https://<staging> INVITE=... loadtest/run.sh`. Every
      threshold must show ✓: reads p95 < 300 ms, writes p95 < 500 ms, no
      lost increments, scans p95 < 5 s.
- [ ] During that run, no alert fired apart from the expected ones, and
      memory stayed below 70% of the VPS.
- [ ] The load-test accounts are on staging only, never on the live database.

## On the day

- [ ] Take a fresh backup right before opening up: `scripts/backup-postgres.sh`.
- [ ] Tag what is deployed with `git tag launch-$(date +%F)` so a rollback has
      a target.
- [ ] Open registration or announce the access-request form.
- [ ] Keep Prometheus open for the first hour. Watch request rate, p95
      latency, 5xx, `manifest_jobs_oldest_due_seconds` and refused sign-ins.

## Rolling back

- Code: `git checkout <previous tag> && docker compose -f docker-compose.prod.yml up -d --build`.
  An older build skips migrations it doesn't know, so it starts on a newer
  schema. That is safe while migrations only add, as 0001 and 0002 do. If a
  release's migration changed or dropped something, restore the data as well.
- Data: "Restoring" in OPERATIONS.md, from the backup taken before launch.

## The first week

- [ ] Read the alert mail every day, and tune any threshold that fires without
      a real problem behind it.
- [ ] Check that `verify-backup.sh` ran on Sunday (ManifestBackupNotVerified
      would say if it didn't).
- [ ] Look at the slowest routes:
      `topk(5, histogram_quantile(0.95, sum by (route, le) (rate(manifest_http_request_duration_seconds_bucket[1d]))))`.
- [ ] Check disk growth against the estimate made before launch.
