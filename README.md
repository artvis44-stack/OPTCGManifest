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

Card pictures are fetched by the server and cached in `img-cache/`, then served
from your own machine. They are not hotlinked: the official card site sends a
`Cross-Origin-Resource-Policy` header that makes browsers refuse to render its
images inside another page.

The first time you view a set the pictures are pulled one by one, so it fills in
gradually. After that they load from disk instantly and work offline. Delete
`img-cache/` to reclaim the space; it will refill on demand.

If a picture never appears, the machine running the server could not reach
`en.onepiece-cardgame.com`. Everything else keeps working — art is decoration,
the counts are the point.

## Getting your data out

`Export CSV` in the My cards tab, or `http://<host>:8420/api/export.csv`. Columns:
card number, name, set, variant, rarity, colours, category, quantity, note, last
updated. Opens in Excel, Sheets, or anything else.

The database is a single file, `manifest.db`. Copy it to back it up.

## What the catalogue covers

2,627 printings: OP-01 through OP-10, EB-01, EB-02, PRB-01, ST-01 through ST-21,
promos and other-product cards. Data comes from the
[vegapull-records](https://github.com/coko7/vegapull-records) dataset and is current
to April 2025.

**Sets released after that are not in it** — OP-11 onward is missing. You can still
log those cards and they count correctly; they just show up without a name or
picture.

To bring it up to date, scrape the official card list:

```
dotnet run --project Manifest -- scrape
dotnet run --project Manifest -- --reseed
```

That reads `en.onepiece-cardgame.com` directly, so it is current whenever you run
it, with no third-party dataset in the way. It takes a couple of minutes — one
request per set with a deliberate pause between, because it is someone else's
website.

Useful variations:

```
dotnet run --project Manifest -- scrape --list              # see what sets exist first
dotnet run --project Manifest -- scrape --only OP-11 OP-12  # just the missing ones
dotnet run --project Manifest -- scrape --merge --only OP-11 OP-12   # add to what you have
```

`refresh-catalog` is the fallback, pulling the same April 2025 dataset from GitHub.
Use it when the official site is unreachable or its markup changes.

Japanese-only cards are not included. The upstream dataset has a Japanese file with
known formatting problems, so it is deliberately left out.

## What this does not do

**Prices.** TCGplayer and Cardmarket both gate their pricing APIs behind approval,
and scraping them breaks the moment they change a page. This tool counts what you
own and hands you a CSV; if you want values, paste that CSV into a service that has
proper price data.

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

The catalogue and price jobs are subcommands of the same binary:

```
dotnet run --project Manifest -- refresh-prices    # market prices, in GBP
dotnet run --project Manifest -- refresh-catalog   # rebuild from the GitHub dataset
dotnet run --project Manifest -- scrape            # rebuild from the official site
```

## Tests

```
dotnet test Manifest.Tests
```

85 tests. Most boot a real server on a scratch database and drive it over HTTP:
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
| `Manifest/` | the server: HTTP, SQLite, API, scraper and price jobs |
| `ui.html` | the interface |
| `catalog.json` | card data, seeded into the database on first run |
| `make_cert.sh` | certificate authority and server certificate for the camera |
| `ca.pem`, `cert.pem`, `key.pem` | created by `make_cert.sh`; keep the keys private |
| `Manifest.Tests/` | test suite |
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
