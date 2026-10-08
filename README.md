# Manifest

A self-hosted counter for a One Piece TCG collection. Runs on one machine on your
network; you use it from your phone. No accounts, no scan limits, no subscription.
Your counts live in a SQLite file you own.

## Run it

```
dotnet run --project Manifest
```

.NET 8 or newer. `dotnet restore` pulls the two dependencies (SQLite and the
Anthropic client) the first time.

To build it once and run the binary instead:

```
dotnet publish Manifest -c Release -o dist
./dist/manifest
```

It prints two addresses. Use the second one on your phone:

```
This machine : http://localhost:8420
Your phone   : http://192.168.1.42:8420
```

Both devices need to be on the same network. First start seeds the card catalogue
into `manifest.db`, which takes a couple of seconds; later starts are instant.

## Logging cards

Three ways, fastest first.

**Type the number.** Read the number off the bottom-right corner, type it, press
Enter. That logs one copy and clears the box for the next card. For a shoebox of
bulk this beats scanning by a wide margin — no focusing, no waiting, no misreads.

**Search by name.** Type "Nami" and every printing comes back with its art. Tap `+`
on the right one. Use this when you can't read the number or aren't sure which
version you're holding.

**Scan a photo.** Optional, and needs setup — see below.

Alternate arts are tracked as separate entries (`OP01-016` and `OP01-016_p1` are
different rows with different images), because a parallel can be worth many times
the base print and lumping them together loses exactly the information worth having.

## Turning on scanning

Scanning is free and runs entirely on your own machine. Install Tesseract, an
open-source OCR engine:

```
sudo apt install tesseract-ocr                    # Debian, Ubuntu, Raspberry Pi
sudo dnf install tesseract tesseract-langpack-eng # Fedora
sudo pacman -S tesseract tesseract-data-eng       # Arch
brew install tesseract                            # macOS
winget install UB-Mangoldt.TesseractOCR           # Windows
```

On Fedora the package is called `tesseract`, not `tesseract-ocr`, and the English
training data is a separate package — without it the engine installs but reads
nothing. Check with `tesseract --list-langs`; you want `eng` in the list.

Restart the server and it says `Scanning: on, local OCR`. Nothing is uploaded
anywhere, there is no key, and there is no per-scan cost.

**How it works.** Your phone crops the bottom-right corner of the photo — the dashed
box in the viewfinder shows exactly where — blows it up, flattens it to grey and
stretches the contrast, then sends a few processed versions of that small crop to
the server. Tesseract reads each until one produces a real card number. Because
letters and digits are in fixed positions, common OCR slips get corrected
automatically: `0P11-004` becomes `OP11-004`, `EBO1-006` becomes `EB01-006`.

**Line the number up inside the dashed box.** That box is the crop region. If the
number is outside it, there is nothing to read.

**What it struggles with:** low-contrast numbers, mostly pale text over pale
artwork. On the test set it reads clean, blurred, tilted, small and noisy corners
correctly and misses low-contrast ones. When it misses it tells you why rather than
guessing, and typing the number is right there.

**The paid alternative.** If you set `ANTHROPIC_API_KEY`, unreadable photos fall
through to Claude, which handles the awkward cases. Purely optional — with Tesseract
installed the API is never called, so it costs nothing unless you ask for it.

### The live viewfinder needs a secure page

Browsers only allow camera access on a secure page, and a plain `http://` address on
your own network does not count. This is a browser rule; the app cannot opt out.

**You may not need to bother.** Without HTTPS the **Photo** button scans exactly the
same — it opens your camera app and uploads the picture. Only the live viewfinder is
affected. And typing a card number beats both for bulk.

If you do want the viewfinder:

```
./make_cert.sh
dotnet run --project Manifest -- --https
```

Then open the `/setup` address the server prints, **on the phone**, and follow it.

That address is deliberately plain `http://`, on the port one above the app. It has
to be: the setup page is what gets the certificate onto your phone, so serving it
over `https://` would mean the phone had to trust the certificate before it could
reach the page that installs it. The helper serves nothing but the certificate and
the instructions.

```
Your phone   : https://192.168.1.42:8420
Camera setup : http://192.168.1.42:8421/setup    <- start here
```

A bare self-signed certificate is no longer enough for phones, so `make_cert.sh`
creates a small certificate authority of your own and signs a server certificate
with it. You install the authority on the phone once. Two details matter and are
easy to get wrong:

- The server certificate is valid for **397 days**. Safari refuses anything over
  398, with no way to proceed past the warning.
- It carries an `extendedKeyUsage` of `serverAuth` and lists every address of this
  machine in `subjectAltName`. iOS 13 and later reject certificates without both.

On iPhone, installing the profile is only half the job — you must then switch the
authority on under **Settings → General → About → Certificate Trust Settings**. The
`/setup` page walks through it.

**Android has a shortcut with nothing to install:** open
`chrome://flags/#unsafely-treat-insecure-origin-as-secure`, paste in the `http://`
address, set it to Enabled and relaunch Chrome.

## Card art

Card pictures are fetched by the server and kept in `img-cache/` (or a bucket -
see below), then served from your own machine. They are not hotlinked: the
official card site sends a `Cross-Origin-Resource-Policy` header that makes
browsers refuse to render its images inside another page.

The first time you view a set the pictures are fetched in the background, so it
fills in over a few seconds; the page asks again for each one until it arrives.
After that they load from disk instantly and work offline. Delete `img-cache/` to
reclaim the space; it will refill on demand. A picture the card site fails to send
is not asked for again straight away, but after a minute, then two, doubling to a
day.

If a picture never appears, the machine running the server could not reach
`en.onepiece-cardgame.com`. Everything else keeps working — art is decoration,
the counts are the point.

## Binders: sharing a collection

Cards live in binders. Everyone has their own, **Mine**. To keep one collection
with someone else, open **Manage binders**, make a shared binder (tick "move
everything in Mine into it" to bring your existing cards along) and add them by
username. Everyone in a shared binder can see and change it, and each card shows who
first logged it.

The **Binder** picker at the top decides where typed, searched and scanned cards go,
and what My cards, the totals and the CSV export show. **All I can use** adds every
binder you are in together, read-only. Decks count cards from all of them. A card
can be moved between binders from its dossier, and you can let the people you share
a binder with look at your own Mine, read-only, with the switch in Manage binders.

The API takes the same choice as `?binder=<id>` or `?binder=all` on the collection,
search, card, stats and export routes; without it, they mean your own binder.

## Getting your data out

`Export CSV` in the My cards tab exports the binder you are looking at, or
`http://<host>:8420/api/export.csv?binder=<id>`. Columns:
card number, name, set, variant, rarity, colours, category, quantity, note, last
updated. Opens in Excel, Sheets, or anything else.

The database is a single file, `manifest.db`. Copy it to back it up.

## What the catalogue covers

Every English set on the official card list at the time `catalog.json` was last
scraped — OP-01 onward, the EB and PRB sets, the starter and ultra decks, promos and
other-product cards. Japanese-only cards are not included.

A set released since then is still loggable — the cards count correctly, they just
show up without a name or picture until the catalogue catches up, which it does on
its own.

## Keeping cards and prices current

Nothing to run. The server does both by itself, once a day:

- **New sets.** It reads the set list on `en.onepiece-cardgame.com`, fetches any set
  the catalogue does not have yet, plus the promo lists, which grow between sets, and
  adds them. When nothing is new that is a handful of requests. Cards logged before their
  set arrived pick up their name and picture.
- **What the official site leaves out.** Promos only the Japanese site lists, the
  English prints of anniversary sets, event and championship packs, stamped release-event
  and pre-release cards, and sets too new for the official site, from
  [Limitless](https://onepiece.limitlesstcg.com) and TCGplayer (through
  [tcgcsv.com](https://tcgcsv.com)). They are kept in `catalog-extra.json`, beside the
  official catalogue; a print TCGplayer alone knows is numbered `_t1`, `_t2`... under
  its card. About 150 page requests, a few minutes.
- **Prices.** Cardmarket (EUR, as Limitless shows it), TCGplayer (USD, from tcgcsv.com)
  and [optcgapi.com](https://optcgapi.com), each kept and shown on the card, converted to
  GBP at the day's rate. A card's headline price is Cardmarket's, else TCGplayer's, else
  optcgapi.com's. A new set's prices are fetched straight after it arrives.
- **What no source lists.** A print none of these has - a promo that came with a book,
  say - can be added by hand: open the card and press **Add a print that isn't listed**,
  with its name, set, rarity, a photo and, if you like, a price. It is filed as `_c1`,
  `_c2`... under its card, reads as that card everywhere, and survives every refresh.

To do either now, sign in as the owner (the first account made on the server),
press **Admin** at the top, and use **Check for new sets** or **Refresh prices**
under *Card data*. The same panel shows when each last ran and what happened.

To change how often they run, set a number of hours, or `off`:

```
MANIFEST_SCRAPE_CATALOG_HOURS=168   # new sets weekly
MANIFEST_REFRESH_PRICES_HOURS=off   # never fetch prices
```

From a console, the same work is available as subcommands:

```
dotnet run --project Manifest -- scrape --new     # add sets it does not have yet
dotnet run --project Manifest -- scrape --limitless  # cards and prints from Limitless/TCGplayer
dotnet run --project Manifest -- --reseed         # load catalog.json into the database
dotnet run --project Manifest -- refresh-prices
```

`scrape` on its own re-reads every set (a couple of minutes, with a pause between
requests, because it is someone else's website); `scrape --list` shows the sets
the site lists and `--only OP-11 OP-12` limits it to some.

`refresh-catalog` replaces `catalog.json` with the
[vegapull-records](https://github.com/coko7/vegapull-records) dataset, which stops at
April 2025. It is only a fallback for when the official site's markup changes, and
it drops every set newer than that until the next new-set check.

## Checking which version you are running

```
curl localhost:8420/api/health
```

That reports the version, how many printings are in the catalogue, and whether
scanning is on. Any unknown path also returns the list of routes this copy
supports, so a missing `/setup` means the file is older than the documentation.

## Options

```
dotnet run --project Manifest -- --port 9000   # different port
dotnet run --project Manifest -- --lan        # reachable from your phone
dotnet run --project Manifest -- --https      # TLS, needed for the live camera
dotnet run --project Manifest -- --reseed     # rebuild the catalogue from catalog.json
dotnet run --project Manifest -- --verbose    # log every request
```

The catalogue and price jobs are subcommands of the same binary, though the server
runs them itself (see *Keeping cards and prices current*):

```
dotnet run --project Manifest -- scrape --new      # add new sets from the official site
dotnet run --project Manifest -- refresh-prices    # market prices, in GBP
dotnet run --project Manifest -- refresh-catalog   # rebuild from the GitHub dataset
dotnet run --project Manifest -- print add EB02-003 --name "Book promo" --photo card.jpg
```

### PostgreSQL instead of SQLite

For a deployment where more than one copy of the app shares the data, point it at
PostgreSQL (14 or newer, with the `citext` and `pg_trgm` extensions available —
both ship with PostgreSQL and every managed provider allows them):

```
MANIFEST_DATABASE_URL=postgres://user:pass@host:5432/manifest?sslmode=require \
  dotnet run --project Manifest
```

The schema is created and kept current by the numbered scripts in
`Manifest/Data/Migrations/Postgres/`, applied on startup under a lock so several
containers can start at once. Unset, it stays `sqlite://manifest.db` - but only
in Development: with `MANIFEST_ENV=Production` (the container image's default) the
server and worker refuse to start on anything but PostgreSQL.

To move an existing collection across, stop the server, keep a copy of
`manifest.db`, and point the copy at an empty PostgreSQL database:

```
dotnet run --project Manifest -- migrate-sqlite --postgres "postgres://..."
```

Accounts, passwords, sessions, decks, requests and scan history all come with their
ids, so nobody is signed out and invite links already sent keep working. It is one
transaction that checks its counts before committing, and it refuses a target that
already has accounts or collections in it. If the file holds rows from before
accounts existed and also has accounts, it asks whose they are: `--owner <name>`.

### More than one app container: Redis

Sessions live in the database, so any container can serve any signed-in user. The
sign-in throttle (8 wrong passwords or invite codes from one address in 15 minutes,
then a 15-minute wait) is kept in memory unless Redis is configured, and memory is
per process: behind a load balancer, three containers would allow three times the
guesses. Point them all at one Redis and they share one count:

```
MANIFEST_REDIS_URL=redis://:password@redis-host:6379   # rediss:// for TLS
```

If Redis goes away, sign-ins keep working without the throttle rather than being
refused, each miss is logged as an error, and `/api/health/ready` reports Redis as
failing.

### Background jobs and the worker

Fetching card art, sending mail, reading scans, refreshing prices and the
housekeeping (expired sessions, links and old jobs) all run as jobs from a queue in
the database. By default the server runs them itself (`MANIFEST_WORKER_MODE=inline`).
To run them separately, so the site never waits on them and they can be restarted
on their own:

```
MANIFEST_WORKER_MODE=external dotnet run --project Manifest          # the site
MANIFEST_WORKER_MODE=external dotnet run --project Manifest -- worker  # the jobs
```

Run as many workers as you like against one database; they share the queue, and a
job whose worker dies is picked up by another after five minutes. Urgent work goes
first - a scan someone is waiting on, then mail, then card art, then housekeeping -
and one slot in every worker is kept for the urgent kinds. With
`MANIFEST_SCAN_MODE=async` (the Production default) the phone sends a scan and
waits for the worker's answer instead of holding the request open.

New sets and prices are checked daily (see *Keeping cards and prices current*);
`manifest enqueue scrape-catalog` (or `refresh-prices`, `refresh-catalog`, `purge`)
queues one now, which is what the buttons on the admin page do. With several
workers, the new-set check keeps its `catalog.json` on whichever worker ran it, so
keep the worker's `/data` on a volume, as `docker-compose.prod.yml` does.

### Card art in object storage

With several containers, keep the pictures in an S3-compatible bucket - AWS,
Cloudflare R2, Backblaze B2, MinIO, SeaweedFS - instead of each container's disk:

```
MANIFEST_OBJECT_STORAGE_ENDPOINT=https://<account>.r2.cloudflarestorage.com
MANIFEST_OBJECT_STORAGE_BUCKET=manifest-art
MANIFEST_OBJECT_STORAGE_ACCESS_KEY=...
MANIFEST_OBJECT_STORAGE_SECRET_KEY=...
```

Pictures are stored as `cards/<card-id>.png` and still served through the app.

## Deploying to a VPS

Everything is in `docker-compose.prod.yml`: Caddy (HTTPS, with a certificate from
Let's Encrypt it fetches and renews itself) in front of two app containers, a
worker, PostgreSQL, Redis, and a SeaweedFS store for card art. Any VPS with Docker
and a couple of GB of memory will do.

1. **DNS.** Point an A (and AAAA, if it has IPv6) record for your domain at the
   server, and open ports 80 and 443. Caddy needs both to get the certificate.
2. **Settings.** On the server:

   ```
   git clone <this repo> manifest && cd manifest
   cp .env.example .env
   ```

   Fill in `MANIFEST_DOMAIN`, and give `POSTGRES_PASSWORD` and
   `MANIFEST_OBJECT_STORAGE_SECRET_KEY` long random values (`openssl rand -hex 24`).
   Decide how people get accounts: a `MANIFEST_INVITE_CODE` (16+ characters), the
   request-and-approve flow (`MANIFEST_ADMIN_EMAIL` plus SMTP), or neither.
3. **Your existing collection** (skip for a fresh start). Copy `manifest.db` to the
   server, then, before anyone signs in on the new site:

   ```
   docker compose -f docker-compose.prod.yml up -d postgres
   docker compose -f docker-compose.prod.yml run --rm \
     -v ./manifest.db:/import/manifest.db:ro app migrate-sqlite --sqlite /import/manifest.db
   ```

   Your account, password, collection, decks and history come across as they are,
   and the file itself is not changed.
4. **Start it.**

   ```
   docker compose -f docker-compose.prod.yml up -d --build
   ```

   `https://<your domain>/api/health/ready` should show every check as `ok`. With
   neither invite code nor migration, make the first account on the server:
   `docker compose -f docker-compose.prod.yml exec app manifest-entrypoint user add <name>`.
5. **Backups.** `scripts/backup-postgres.sh` dumps the database to `backups/` and
   keeps two weeks of them; `scripts/verify-backup.sh` test-restores the newest
   one. Cron lines, the off-site copy and the restore steps are in
   [docs/OPERATIONS.md](docs/OPERATIONS.md).
6. **Monitoring and launch.** `--profile monitoring` adds Prometheus, Alertmanager
   and a host exporter with alert rules for errors, latency, the job queue, disk
   and backups. Before opening the site up, go through
   [docs/LAUNCH_CHECKLIST.md](docs/LAUNCH_CHECKLIST.md), including a load test
   (`loadtest/run.sh`) against a staging copy.

To update: `git pull && docker compose -f docker-compose.prod.yml up -d --build`.
Migrations run as the new containers start. `APP_REPLICAS` and `WORKER_REPLICAS`
in `.env` set how many of each run.

`docker compose up --build` (the plain `docker-compose.yml`) runs the same stack on
your own machine at http://localhost:8420, without Caddy or a domain.

## Tests

```
dotnet test Manifest.Tests
```

To run the same suite against PostgreSQL, give it a server it may create scratch
databases on (each run makes its own and drops it afterwards):

```
docker run -d --name manifest-pg -e POSTGRES_USER=manifest -e POSTGRES_PASSWORD=manifest \
  -p 127.0.0.1:55432:5432 postgres:16-alpine
MANIFEST_TEST_POSTGRES=postgres://manifest:manifest@127.0.0.1:55432/manifest \
  dotnet test Manifest.Tests
```

Likewise `MANIFEST_TEST_REDIS=redis://127.0.0.1:6379` runs the throttle tests
against Redis, including two servers sharing one allowance, and
`MANIFEST_TEST_S3=http://127.0.0.1:8333` the object-storage tests against an
S3-compatible server (any with a `test`/`test` key pair). All three can be set
together.

Most tests boot a real server on a scratch database and drive it over HTTP:
catalogue seeding, lookup by number and name, alt-art separation, logging and
clamping, 40 simultaneous writes landing correctly, stats, CSV export, deck
building and its legality rules, error handling, survival across a restart,
keep-alive connection reuse, and the art proxy refusing path traversal. The
Host/Origin/content-type/body-size refusals each have a test. Others parse
reconstructed card-list markup to check the scraper reads names, rarities, costs,
multi-colour cards, type lines and alt-art printings correctly, and check the OCR
character-confusion repairs. The TLS tests run `make_cert.sh` and skip themselves
if OpenSSL is missing. Cleans up after itself.

## Files

| | |
|---|---|
| `Manifest/` | the server: HTTP, SQLite/PostgreSQL, API, scraper and price jobs |
| `Manifest/wwwroot/` | the interface: page, stylesheet and scripts, built into the binary |
| `catalog.json` | card data, seeded into the database on first run |
| `make_cert.sh` | certificate authority and server certificate for the camera |
| `ca.pem`, `cert.pem`, `key.pem` | created by `make_cert.sh`; keep the keys private |
| `Manifest.Tests/` | test suite |
| `Dockerfile`, `docker-compose*.yml`, `docker/`, `.env.example` | the container image and the two stacks |
| `scripts/backup-postgres.sh`, `scripts/verify-backup.sh` | nightly database dumps, and a test restore of one |
| `docker/prometheus*.yml`, `docker/alerts.yml`, `docker/alertmanager.sh` | the monitoring profile and its alert rules |
| `loadtest/` | the k6 load test for the launch target, and `run.sh` to run it from Docker |
| `docs/` | `OPERATIONS.md` (metrics, alerts, backups, load tests) and `LAUNCH_CHECKLIST.md` |
| `manifest.db` | your collection — created on first run |
| `img-cache/` | card pictures, downloaded as you view them |

`Manifest/README.md` has the layout of the server source itself.

## A note on the network

The server binds to `127.0.0.1` — this machine only — unless you pass `--lan`, which
binds every interface so your phone can reach it. With `--lan` anyone on your network
can reach it and change your counts; on a home network that is usually what you want.
There is no login. Do not expose the port to the internet.
# OPTCGManifest
# OPTCGManifest
# OPTCGManifest
# OPTCGManifest
# OPTCGManifest
# OPTCGManifest
