# Manifest — server

The server, on .NET 8 / ASP.NET Core. `ui.html`, `manifest.db`, `catalog.json`,
`img-cache/` and `make_cert.sh` live in the repository root and are shared with it.

This started as a port of a Python implementation; the database schema and every
JSON response were kept identical to it, which is why a few notes below explain why
something is shaped the way it is.

## Running it

```sh
cd Manifest
dotnet run                      # http://localhost:8420, this machine only
dotnet run -- --lan             # reachable from your phone
dotnet run -- --https           # TLS, needed for the live camera (run ../make_cert.sh first)
dotnet run -- --help
```

Built output is a single binary:

```sh
dotnet publish -c Release
./bin/Release/net8.0/publish/manifest --lan
```

The catalogue and price jobs are subcommands of the same binary:

| Command | What it does |
|---|---|
| `manifest refresh-catalog` | rebuild `catalog.json` from the vegapull-records dataset |
| `manifest refresh-prices` | pull market prices into the database, converted to GBP |
| `manifest scrape --list` | list the sets on the official card site |
| `manifest scrape` | rebuild `catalog.json` from the official card site |
| `manifest --reseed` | reload the database's catalogue from `catalog.json` |

`--root PATH` points the app at the directory holding `manifest.db`, `catalog.json`
and `ui.html`. Without it the root is found by walking up from the working directory,
so running from the repository root just works.

## Tests

```sh
dotnet test Manifest.Tests
```

85 tests. Most boot the real binary against a scratch database seeded from
`catalog.json` and drive it over HTTP, so they exercise routing, the guards and
serialisation rather than calling the repositories directly. The TLS tests shell out
to `make_cert.sh` and skip themselves if `openssl` is missing.

## Layout

| File | What it holds |
|---|---|
| `Program.cs` | argument dispatch, Kestrel setup, TLS, the startup banner |
| `Cli.cs`, `Net.cs`, `AppConfig.cs`, `AppPaths.cs` | options, host discovery, paths |
| `Web/Guard.cs` | Host / Origin / content-type / body-size refusals |
| `Web/Endpoints.cs` | every route |
| `Web/SetupHelper.cs` | the plain-http certificate-install listener |
| `Data/` | connection handling, schema, card and deck repositories |
| `Services/` | card-id parsing, deck analysis, image cache, the two scanners |
| `Tools/` | the catalogue scraper and the two refresh jobs |

## Notes

- **Whole floats render as `2.0`, not `2`** (see `DoubleConverter.cs`). That matched
  the original implementation's output, so responses captured either side of the port
  diff cleanly. Both parse to the same JavaScript number.
- **The guards are hand-written** rather than delegated to `HostFiltering`/CORS
  middleware, because the exact 421/403/415/413 codes and the `{error, ref}` body are
  part of what the UI and the tests expect.
- **`cert.pem` coverage is checked in-process** using the certificate's
  subjectAltName instead of shelling out to `openssl`, so there is no external tool
  to be missing.
- **Kestrel handles the connection load**, so the old worry about a phone firing a
  burst of increments overflowing a small listen backlog no longer applies.
- **The scan model is `claude-sonnet-4-6`.** Changing it changes what a scan costs,
  so that stays a deliberate edit.
- **A Leader's Life** lives in the catalogue's `cost` column. That quirk is now stated
  once, on `LeaderSummary.Life`, instead of at each call site.
