# Graph Report - optcg-manifest  (2026-10-04)

## Corpus Check
- 72 files · ~25,421,420 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 985 nodes · 1923 edges · 53 communities (46 shown, 7 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 99 edges (avg confidence: 0.81)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `c4a3032c`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Manifest
- Manifest.Data
- esc (HTML escape)
- ICardRepository
- .Detail
- AccessTests
- CatalogScraper
- loadFacets
- cornerVariants
- .Open
- ISessionRepository
- send (scan submit)
- make_cert.sh
- .Normalise
- ServerFixture
- AppConfig
- CatalogRefresh
- ServingTests
- Requests.cs
- CatalogAndLookupTests
- DeckTests
- CollectionTests
- Manifest.csproj
- Responses
- ReaderExtensions
- Manifest — server
- CLAUDE.md
- Database
- setDeckCardQty
- wire (row +/- handlers)
- DeckListParser
- api (fetch wrapper)
- AccessRepository
- pickLeader
- Manifest Scaling Plan
- DatabaseUrlTests
- LoginThrottle
- Manifest
- AGENTS.md
- .Build
- .Write
- Net
- AccessPages.cs
- DeckDetail
- CardRepository
- .Read
- .Search
- ImageCache
- DeckAnalysis
- CardQuantity
- SystemClock
- LoginPage.cs

## God Nodes (most connected - your core abstractions)
1. `AccessRepository` - 37 edges
2. `UserRepository` - 30 edges
3. `CatalogScraper` - 30 edges
4. `ServerFixture` - 28 edges
5. `Database` - 23 edges
6. `ServingTests` - 21 edges
7. `Manifest.Data` - 21 edges
8. `AccessRequest` - 21 edges
9. `Manifest.Models` - 20 edges
10. `CatalogAndLookupTests` - 19 edges

## Surprising Connections (you probably didn't know these)
- `AccessRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/AccessRepository.cs → Manifest/Data/Database.cs
- `CardRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/Database.cs
- `CardRepository` --implements--> `ICardRepository`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/RepositoryInterfaces.cs
- `ApiScanner` --references--> `CardRepository`  [EXTRACTED]
  Manifest/Services/ApiScanner.cs → Manifest/Data/CardRepository.cs
- `DeckRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/DeckRepository.cs → Manifest/Data/Database.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Client-side OCR capture and preprocessing pipeline** — ui_shrink, ui_cornervariants, ui_send, ui_concept_object_fit_cover_crop, ui_concept_min_vs_chroma_channel, ui_concept_plane_fit_flatfield, ui_concept_multi_region_multi_config_ocr [EXTRACTED 1.00]
- **Deck editor re-render pipeline** — ui_renderdeckeditor, ui_renderdeckhead, ui_renderdecklegal, ui_renderdeckguide, ui_renderdeckcards, ui_renderbuylist, ui_setdeckcardqty [EXTRACTED 1.00]
- **Abort-latest-wins async fetch family** — ui_dosearch, ui_doleadersearch, ui_dodecksearch, ui_loadpreview, ui_concept_abort_latest_wins [INFERRED 0.95]

## Communities (53 total, 7 thin omitted)

### Community 0 - "Manifest"
Cohesion: 0.09
Nodes (21): A note on the network, Card art, Checking which version you are running, Files, Getting your data out, Logging cards, Manifest, OPTCGManifest (+13 more)

### Community 1 - "Manifest.Data"
Cohesion: 0.14
Nodes (9): Manifest.Tools, Manifest.Web, Manifest.Services, Manifest.Data, Manifest.Models, Exception, AccountError, SessionInfo (+1 more)

### Community 2 - "esc (HTML escape)"
Cohesion: 0.23
Nodes (16): cardModalHTML, Deferred effect-text fetch, Leader cost column holds Life, Scheduled price snapshots, not live pricing, /img/{card_id} image proxy, esc (HTML escape), flash, fmtGBP (+8 more)

### Community 3 - "ICardRepository"
Cohesion: 0.15
Nodes (8): ICardRepository, List, AdjustResult, BulkLogResult, CardDetailRow, Facets, SetStat, Stats

### Community 4 - ".Detail"
Cohesion: 0.24
Nodes (6): DbDataReader, DbConnection, IReadOnlyList, List, DeckRepository, CatalogRow

### Community 5 - "AccessTests"
Cohesion: 0.06
Nodes (25): bool, Manifest.Tests, DataReceivedEventArgs, IAsyncLifetime, Fact, HttpClient, HttpResponseMessage, int (+17 more)

### Community 6 - "CatalogScraper"
Cohesion: 0.10
Nodes (19): Label, Fact, string, ScraperTests, AppPaths, GeneratedRegex, HttpClient, List (+11 more)

### Community 7 - "loadFacets"
Cohesion: 0.22
Nodes (9): Facet-driven filter dropdowns, Rarity abbreviation vintage tolerance, Log-anything tolerance for uncatalogued cards, GET /api/facets, fillSelect, loadFacets, orderBy, RARITY_LABEL lookup table (+1 more)

### Community 8 - "cornerVariants"
Cohesion: 0.29
Nodes (8): camConstraints (portrait video), Client-side OCR preprocessing, Freeze-frame capture on shoot, min-channel vs chroma ink separation, Multi-region, multi-config OCR shotgun, object-fit:cover crop compensation, Least-squares plane-fit flat-fielding, cornerVariants

### Community 9 - ".Open"
Cohesion: 0.06
Nodes (21): DbConnection, IUserRepository, DateTime, DbConnection, DbDataReader, IClock, List, string (+13 more)

### Community 10 - "ISessionRepository"
Cohesion: 0.13
Nodes (9): TimeSpan, ISessionRepository, HashSet, HttpContext, RequestDelegate, string, Task, Auth (+1 more)

### Community 11 - "send (scan submit)"
Cohesion: 0.16
Nodes (14): startup health bootstrap IIFE, Stream-restart autofocus fallback, Photo-first scanning fallback, OCR-vs-API scan engine selection, Secure-context requirement for live camera, GET /api/health, POST /api/scan, GET /api/stats (+6 more)

### Community 13 - ".Normalise"
Cohesion: 0.09
Nodes (17): Dictionary, GeneratedRegex, List, Regex, CardId, Task, AppPaths, IReadOnlyList (+9 more)

### Community 14 - "ServerFixture"
Cohesion: 0.11
Nodes (13): ICollectionFixture, Body, HttpClient, JsonElement, Process, Status, string, StringContent (+5 more)

### Community 15 - "AppConfig"
Cohesion: 0.10
Nodes (14): HashSet, int, IReadOnlyList, long, string, AppConfig, string, Cli (+6 more)

### Community 16 - "CatalogRefresh"
Cohesion: 0.17
Nodes (10): AppPaths, JsonSerializerOptions, List, string, Task, CatalogRefresh, TitleParts, VegapullCard (+2 more)

### Community 17 - "ServingTests"
Cohesion: 0.19
Nodes (7): Action, HttpMethod, HttpRequestMessage, Fact, HttpResponseMessage, Task, ServingTests

### Community 18 - "Requests.cs"
Cohesion: 0.09
Nodes (22): JsonConverter, JsonSerializerOptions, Type, Utf8JsonReader, Utf8JsonWriter, PythonStyleDoubleConverter, PythonStyleNullableDoubleConverter, JsonSerializerOptions (+14 more)

### Community 19 - "CatalogAndLookupTests"
Cohesion: 0.28
Nodes (4): Fact, JsonElement, Task, CatalogAndLookupTests

### Community 20 - "DeckTests"
Cohesion: 0.29
Nodes (7): Deck, Id, Fact, JsonElement, string, Task, DeckTests

### Community 21 - "CollectionTests"
Cohesion: 0.31
Nodes (3): Fact, Task, CollectionTests

### Community 22 - "Manifest.csproj"
Cohesion: 0.14
Nodes (12): net8.0, net8.0, Anthropic (12.40.0), MailKit (4.17.0), Microsoft.Data.Sqlite (8.0.11), Microsoft.NET.Test.Sdk (17.11.1), Npgsql (8.0.6), xunit (2.9.2) (+4 more)

### Community 23 - "Responses"
Cohesion: 0.39
Nodes (5): Key, HttpContext, Task, Responses, Value

### Community 24 - "ReaderExtensions"
Cohesion: 0.29
Nodes (3): DbDataReader, string, ReaderExtensions

### Community 25 - "Manifest — server"
Cohesion: 0.33
Nodes (5): Layout, Manifest — server, Notes, Running it, Tests

### Community 27 - "Database"
Cohesion: 0.06
Nodes (33): CancellationToken, Component, DbCommand, DbParameter, IReadOnlyDictionary, AppPaths, long, string (+25 more)

### Community 28 - "setDeckCardQty"
Cohesion: 0.22
Nodes (14): Guideline bands are advisory, not rules, Leader colour lock for the add-cards browser, Preview panel as single quantity source of truth, Server-computed legality and guideline bands, deckQtyOf, guidePill, renderDeckBrowse, renderDeckCards (+6 more)

### Community 29 - "wire (row +/- handlers)"
Cohesion: 0.27
Nodes (11): cardIndex (client card cache), Duplicated filter controls per pane, Stable sort with card_id tiebreak, /api/collection, indexCards, loadOwned, paintOwned, readFilters (+3 more)

### Community 31 - "DeckListParser"
Cohesion: 0.28
Nodes (6): Dictionary, GeneratedRegex, IEnumerable, List, Regex, DeckListParser

### Community 32 - "api (fetch wrapper)"
Cohesion: 0.40
Nodes (10): api (fetch wrapper), Abort-and-latest-wins search race guard, doDeckSearch, doLeaderSearch, doSearch, GET /api/card/{id}, GET /api/search, hoverPreview (+2 more)

### Community 33 - "AccessRepository"
Cohesion: 0.05
Nodes (35): Acted, DateTime, DbConnection, DbDataReader, IClock, int, InviteToken, List (+27 more)

### Community 34 - "pickLeader"
Cohesion: 0.28
Nodes (9): Conditional wide desktop workspace, /api/decks, openDeck, openLeaderPicker, pickLeader, renderDeckList, showDeckList, showDeckSub (+1 more)

### Community 35 - "Manifest Scaling Plan"
Cohesion: 0.14
Nodes (13): Acceptance Criteria, Manifest Scaling Plan, Phase 1: Stabilize Current App, Phase 2: Database Migration to PostgreSQL, Phase 3: Storage and Image Pipeline, Phase 4: Background Jobs, Phase 5: Public Auth, Abuse Protection, and Sessions, Phase 6: API Pagination and Query Scaling (+5 more)

### Community 36 - "DatabaseUrlTests"
Cohesion: 0.09
Nodes (16): Assembly, DbConnection, IReadOnlyList, List, string, PostgresMigrations, AppPaths, Fact (+8 more)

### Community 37 - "LoginThrottle"
Cohesion: 0.19
Nodes (7): ConcurrentDictionary, DateTime, IClock, int, TimeSpan, LoginThrottle, Record

### Community 38 - "Manifest"
Cohesion: 0.15
Nodes (7): Manifest, AppPaths, JsonSerializerOptions, Json, string, AccessRequestView, AccessStatus

### Community 40 - ".Build"
Cohesion: 0.20
Nodes (6): AppConfig, AppPaths, WebApplication, SetupHelper, string, SetupPage

### Community 41 - ".Write"
Cohesion: 0.33
Nodes (3): IEnumerable, StringBuilder, CsvExport

### Community 44 - "DeckDetail"
Cohesion: 0.19
Nodes (11): IDeckRepository, Dictionary, List, BuyListItem, DeckCardRow, DeckDetail, DeckListItem, Guideline (+3 more)

### Community 45 - "CardRepository"
Cohesion: 0.18
Nodes (5): DbConnection, IReadOnlyList, string, CardRepository, IScanRepository

### Community 46 - ".Read"
Cohesion: 0.22
Nodes (6): ScanResponse, VisionReading, string, Task, ApiScanner, MediaType

### Community 47 - ".Search"
Cohesion: 0.25
Nodes (4): List, List, CollectionRow, SearchRow

### Community 48 - "ImageCache"
Cohesion: 0.25
Nodes (7): byte, AppPaths, Database, HttpClient, ImageCache, Result, SemaphoreSlim

### Community 49 - "DeckAnalysis"
Cohesion: 0.43
Nodes (4): Band, HashSet, Band, DeckAnalysis

### Community 51 - "SystemClock"
Cohesion: 1.00
Nodes (3): DateTime, IClock, SystemClock

## Ambiguous Edges - Review These
- `Leader colour lock for the add-cards browser` → `Server-computed legality and guideline bands`  [AMBIGUOUS]
  ui.html · relation: conceptually_related_to

## Knowledge Gaps
- **78 isolated node(s):** `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)`, `xunit.runner.visualstudio (2.8.2)`, `Xunit.SkippableFact (1.5.61)` (+73 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **7 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `Leader colour lock for the add-cards browser` and `Server-computed legality and guideline bands`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `Manifest.Tests` connect `AccessTests` to `DatabaseUrlTests`, `.Normalise`, `ServerFixture`?**
  _High betweenness centrality (0.223) - this node is a cross-community bridge._
- **Why does `Manifest.Data` connect `Manifest.Data` to `DatabaseUrlTests`, `CardRepository`, `.Normalise`, `ImageCache`, `ReaderExtensions`, `Database`?**
  _High betweenness centrality (0.170) - this node is a cross-community bridge._
- **Why does `Manifest.Services` connect `Manifest.Data` to `AccessRepository`, `Manifest`, `.Write`, `.Open`, `CardRepository`, `.Normalise`, `ImageCache`, `DeckListParser`?**
  _High betweenness centrality (0.113) - this node is a cross-community bridge._
- **What connects `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)` to the rest of the system?**
  _78 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Manifest` be split into smaller, more focused modules?**
  _Cohesion score 0.09090909090909091 - nodes in this community are weakly interconnected._
- **Should `Manifest.Data` be split into smaller, more focused modules?**
  _Cohesion score 0.14153846153846153 - nodes in this community are weakly interconnected._