# Graph Report - optcg-manifest  (2026-10-04)

## Corpus Check
- 76 files · ~253,679 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1062 nodes · 2098 edges · 64 communities (57 shown, 7 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 123 edges (avg confidence: 0.81)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `35eb041c`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Manifest
- Manifest.Models
- esc (HTML escape)
- CardRepository
- .Open
- AccessTests
- CatalogScraper
- loadFacets
- cornerVariants
- UserRepository
- ISessionRepository
- send (scan submit)
- make_cert.sh
- CardId
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
- .Ready
- setDeckCardQty
- wire (row +/- handlers)
- SqliteToPostgres
- api (fetch wrapper)
- AccessRepository
- pickLeader
- Manifest Scaling Plan
- .Scripts
- RedisLoginThrottle
- SqlDialect
- AGENTS.md
- .Build
- .Write
- MigrateSqliteTests
- Manifest.Web
- DeckDetail
- .MapPublic
- ApiScanner
- Database
- ImageCache
- DeckAnalysis
- AccessRequest
- User
- Mailer
- TlsSetupTests
- .MapAuth
- Manifest.Data
- .CreateTestDatabase
- .Create
- Access.cs
- ServerFixture.cs
- DatabaseUrlTests
- PriceRefresh
- Manifest.Tests
- .UsernamesAreCaseInsensitive

## God Nodes (most connected - your core abstractions)
1. `AccessRepository` - 37 edges
2. `UserRepository` - 30 edges
3. `CatalogScraper` - 30 edges
4. `ServerFixture` - 28 edges
5. `Database` - 26 edges
6. `Manifest.Data` - 23 edges
7. `ServingTests` - 21 edges
8. `AccessRequest` - 21 edges
9. `Manifest.Models` - 20 edges
10. `CatalogAndLookupTests` - 19 edges

## Surprising Connections (you probably didn't know these)
- `AccessRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/AccessRepository.cs → Manifest/Data/Database.cs
- `AccessRepository` --implements--> `IAccessRepository`  [EXTRACTED]
  Manifest/Data/AccessRepository.cs → Manifest/Data/RepositoryInterfaces.cs
- `CardRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/Database.cs
- `CardRepository` --implements--> `IScanRepository`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/RepositoryInterfaces.cs
- `ApiScanner` --references--> `CardRepository`  [EXTRACTED]
  Manifest/Services/ApiScanner.cs → Manifest/Data/CardRepository.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Client-side OCR capture and preprocessing pipeline** — ui_shrink, ui_cornervariants, ui_send, ui_concept_object_fit_cover_crop, ui_concept_min_vs_chroma_channel, ui_concept_plane_fit_flatfield, ui_concept_multi_region_multi_config_ocr [EXTRACTED 1.00]
- **Deck editor re-render pipeline** — ui_renderdeckeditor, ui_renderdeckhead, ui_renderdecklegal, ui_renderdeckguide, ui_renderdeckcards, ui_renderbuylist, ui_setdeckcardqty [EXTRACTED 1.00]
- **Abort-latest-wins async fetch family** — ui_dosearch, ui_doleadersearch, ui_dodecksearch, ui_loadpreview, ui_concept_abort_latest_wins [INFERRED 0.95]

## Communities (64 total, 7 thin omitted)

### Community 0 - "Manifest"
Cohesion: 0.09
Nodes (22): A note on the network, Card art, Checking which version you are running, Files, Getting your data out, Logging cards, Manifest, More than one app container: Redis (+14 more)

### Community 1 - "Manifest.Models"
Cohesion: 0.14
Nodes (8): Manifest.Services, Manifest.Models, Exception, AccountError, VisionReading, SessionInfo, RuleViolation, MigrationError

### Community 2 - "esc (HTML escape)"
Cohesion: 0.19
Nodes (18): cardModalHTML, Deferred effect-text fetch, Leader cost column holds Life, Preview panel as single quantity source of truth, Scheduled price snapshots, not live pricing, GET /api/card/{id}, /img/{card_id} image proxy, esc (HTML escape) (+10 more)

### Community 3 - "CardRepository"
Cohesion: 0.07
Nodes (19): IReadOnlyList, List, string, CardRepository, IReadOnlyList, List, ICardRepository, List (+11 more)

### Community 4 - ".Open"
Cohesion: 0.16
Nodes (10): DbConnection, DbDataReader, DbConnection, DbConnection, IReadOnlyList, List, DeckRepository, CatalogRow (+2 more)

### Community 5 - "AccessTests"
Cohesion: 0.15
Nodes (12): DataReceivedEventArgs, Fact, HttpClient, HttpResponseMessage, int, JsonElement, Process, string (+4 more)

### Community 6 - "CatalogScraper"
Cohesion: 0.10
Nodes (19): Label, Fact, string, ScraperTests, AppPaths, GeneratedRegex, HttpClient, List (+11 more)

### Community 7 - "loadFacets"
Cohesion: 0.22
Nodes (9): Facet-driven filter dropdowns, Rarity abbreviation vintage tolerance, Log-anything tolerance for uncatalogued cards, GET /api/facets, fillSelect, loadFacets, orderBy, RARITY_LABEL lookup table (+1 more)

### Community 8 - "cornerVariants"
Cohesion: 0.29
Nodes (8): camConstraints (portrait video), Client-side OCR preprocessing, Freeze-frame capture on shoot, min-channel vs chroma ink separation, Multi-region, multi-config OCR shotgun, object-fit:cover crop compensation, Least-squares plane-fit flat-fielding, cornerVariants

### Community 9 - "UserRepository"
Cohesion: 0.12
Nodes (10): DateTime, DbConnection, DbDataReader, IClock, List, string, TimeSpan, UserRepository (+2 more)

### Community 10 - "ISessionRepository"
Cohesion: 0.13
Nodes (9): TimeSpan, ISessionRepository, HashSet, HttpContext, RequestDelegate, string, Task, Auth (+1 more)

### Community 11 - "send (scan submit)"
Cohesion: 0.16
Nodes (14): startup health bootstrap IIFE, Stream-restart autofocus fallback, Photo-first scanning fallback, OCR-vs-API scan engine selection, Secure-context requirement for live camera, GET /api/health, POST /api/scan, GET /api/stats (+6 more)

### Community 13 - "CardId"
Cohesion: 0.10
Nodes (15): Dictionary, GeneratedRegex, List, Regex, CardId, AppPaths, IReadOnlyList, string (+7 more)

### Community 14 - "ServerFixture"
Cohesion: 0.20
Nodes (8): Body, HttpClient, JsonElement, Process, Status, string, Task, ServerFixture

### Community 15 - "AppConfig"
Cohesion: 0.06
Nodes (23): Manifest, HashSet, int, IReadOnlyList, long, string, AppConfig, AppPaths (+15 more)

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
Cohesion: 0.13
Nodes (13): net8.0, net8.0, Anthropic (12.40.0), MailKit (4.17.0), Microsoft.Data.Sqlite (8.0.11), Microsoft.NET.Test.Sdk (17.11.1), Npgsql (8.0.6), StackExchange.Redis (2.8.16) (+5 more)

### Community 23 - "Responses"
Cohesion: 0.39
Nodes (5): Key, HttpContext, Task, Responses, Value

### Community 24 - "ReaderExtensions"
Cohesion: 0.33
Nodes (3): DbDataReader, string, ReaderExtensions

### Community 25 - "Manifest — server"
Cohesion: 0.33
Nodes (5): Layout, Manifest — server, Notes, Running it, Tests

### Community 27 - ".Ready"
Cohesion: 0.20
Nodes (12): CancellationToken, Component, IReadOnlyDictionary, AppConfig, Body, IConnectionMultiplexer, Status, Task (+4 more)

### Community 28 - "setDeckCardQty"
Cohesion: 0.21
Nodes (15): Guideline bands are advisory, not rules, Leader colour lock for the add-cards browser, Server-computed legality and guideline bands, deckQtyOf, guidePill, hoverPreview, loadPreview, renderDeckBrowse (+7 more)

### Community 29 - "wire (row +/- handlers)"
Cohesion: 0.27
Nodes (11): cardIndex (client card cache), Duplicated filter controls per pane, Stable sort with card_id tiebreak, /api/collection, indexCards, loadOwned, paintOwned, readFilters (+3 more)

### Community 31 - "SqliteToPostgres"
Cohesion: 0.24
Nodes (8): AppPaths, DateTime, DbConnection, Dictionary, HashSet, SqliteConnection, string, SqliteToPostgres

### Community 32 - "api (fetch wrapper)"
Cohesion: 0.67
Nodes (7): api (fetch wrapper), Abort-and-latest-wins search race guard, doDeckSearch, doLeaderSearch, doSearch, GET /api/search, quickAdd

### Community 33 - "AccessRepository"
Cohesion: 0.14
Nodes (12): DateTime, DbConnection, DbDataReader, IClock, int, InviteToken, Request, TimeSpan (+4 more)

### Community 34 - "pickLeader"
Cohesion: 0.28
Nodes (9): Conditional wide desktop workspace, /api/decks, openDeck, openLeaderPicker, pickLeader, renderDeckList, showDeckList, showDeckSub (+1 more)

### Community 35 - "Manifest Scaling Plan"
Cohesion: 0.14
Nodes (13): Acceptance Criteria, Manifest Scaling Plan, Phase 1: Stabilize Current App, Phase 2: Database Migration to PostgreSQL, Phase 3: Storage and Image Pipeline, Phase 4: Background Jobs, Phase 5: Public Auth, Abuse Protection, and Sessions, Phase 6: API Pagination and Query Scaling (+5 more)

### Community 36 - ".Scripts"
Cohesion: 0.15
Nodes (10): Assembly, DbConnection, IReadOnlyList, List, string, PostgresMigrations, SkippableFact, Task (+2 more)

### Community 37 - "RedisLoginThrottle"
Cohesion: 0.07
Nodes (27): ConcurrentDictionary, ConfigurationOptions, IClock, ILogger, LuaScript, DateTime, Fact, HttpClient (+19 more)

### Community 38 - "SqlDialect"
Cohesion: 0.14
Nodes (7): DbCommand, DbParameter, DateTime, DbConnection, Exception, CommandExtensions, SqlDialect

### Community 40 - ".Build"
Cohesion: 0.20
Nodes (6): AppConfig, AppPaths, WebApplication, SetupHelper, string, SetupPage

### Community 41 - ".Write"
Cohesion: 0.33
Nodes (3): IEnumerable, StringBuilder, CsvExport

### Community 42 - "MigrateSqliteTests"
Cohesion: 0.24
Nodes (7): IDisposable, AppPaths, Fact, List, SkippableFact, string, MigrateSqliteTests

### Community 43 - "Manifest.Web"
Cohesion: 0.20
Nodes (5): Manifest.Web, string, AccessPages, string, LoginPage

### Community 44 - "DeckDetail"
Cohesion: 0.20
Nodes (11): IDeckRepository, Dictionary, List, BuyListItem, DeckCardRow, DeckDetail, DeckListItem, Guideline (+3 more)

### Community 45 - ".MapPublic"
Cohesion: 0.18
Nodes (9): Acted, AppConfig, HttpContext, IClock, string, Task, WebApplication, AccessEndpoints (+1 more)

### Community 46 - "ApiScanner"
Cohesion: 0.33
Nodes (3): string, ApiScanner, MediaType

### Community 47 - "Database"
Cohesion: 0.23
Nodes (7): AppPaths, long, SqliteConnection, string, Database, NpgsqlConnectionStringBuilder, NpgsqlDataSource

### Community 48 - "ImageCache"
Cohesion: 0.25
Nodes (7): byte, AppPaths, Database, HttpClient, ImageCache, Result, SemaphoreSlim

### Community 49 - "DeckAnalysis"
Cohesion: 0.43
Nodes (4): Band, HashSet, Band, DeckAnalysis

### Community 50 - "AccessRequest"
Cohesion: 0.14
Nodes (6): List, InviteToken, Request, IAccessRepository, DateTime, AccessRequest

### Community 52 - "Mailer"
Cohesion: 0.20
Nodes (8): AccessMail, Message, Task, TimeSpan, Mailer, MailOutcome, Submission, Message

### Community 53 - "TlsSetupTests"
Cohesion: 0.20
Nodes (8): bool, IAsyncLifetime, int, Process, SkippableFact, string, Task, TlsSetupTests

### Community 54 - ".MapAuth"
Cohesion: 0.12
Nodes (12): Dictionary, GeneratedRegex, IEnumerable, List, Regex, DeckListParser, AppConfig, HttpContext (+4 more)

### Community 55 - "Manifest.Data"
Cohesion: 0.24
Nodes (3): Manifest.Tools, Manifest.Data, IScanRepository

### Community 58 - "Access.cs"
Cohesion: 0.50
Nodes (3): string, AccessRequestView, AccessStatus

### Community 59 - "ServerFixture.cs"
Cohesion: 0.30
Nodes (3): ICollectionFixture, StringContent, ServerCollection

### Community 60 - "DatabaseUrlTests"
Cohesion: 0.25
Nodes (5): AppPaths, Fact, InlineData, Theory, DatabaseUrlTests

### Community 61 - "PriceRefresh"
Cohesion: 0.22
Nodes (8): AppPaths, Dictionary, JsonSerializerOptions, string, Task, FxResponse, PricedCard, PriceRefresh

### Community 63 - ".UsernamesAreCaseInsensitive"
Cohesion: 0.33
Nodes (4): Fact, StringContent, Task, AccountTests

## Ambiguous Edges - Review These
- `Leader colour lock for the add-cards browser` → `Server-computed legality and guideline bands`  [AMBIGUOUS]
  ui.html · relation: conceptually_related_to

## Knowledge Gaps
- **80 isolated node(s):** `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)`, `xunit.runner.visualstudio (2.8.2)`, `Xunit.SkippableFact (1.5.61)` (+75 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **7 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `Leader colour lock for the add-cards browser` and `Server-computed legality and guideline bands`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `Manifest.Tests` connect `Manifest.Tests` to `AccessTests`, `CardId`, `TlsSetupTests`, `Manifest.Data`, `ServerFixture.cs`, `DatabaseUrlTests`, `.UsernamesAreCaseInsensitive`?**
  _High betweenness centrality (0.214) - this node is a cross-community bridge._
- **Why does `Manifest.Data` connect `Manifest.Data` to `Manifest.Models`, `.Scripts`, `SqlDialect`, `ISessionRepository`, `Manifest.Web`, `CardId`, `ApiScanner`, `AppConfig`, `ImageCache`, `.Ready`, `DatabaseUrlTests`?**
  _High betweenness centrality (0.151) - this node is a cross-community bridge._
- **Why does `Manifest.Web` connect `Manifest.Web` to `RedisLoginThrottle`, `.Build`, `ISessionRepository`, `AppConfig`, `Requests.cs`, `.Ready`, `Manifest.Tests`?**
  _High betweenness centrality (0.128) - this node is a cross-community bridge._
- **What connects `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)` to the rest of the system?**
  _80 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Manifest` be split into smaller, more focused modules?**
  _Cohesion score 0.08695652173913043 - nodes in this community are weakly interconnected._
- **Should `Manifest.Models` be split into smaller, more focused modules?**
  _Cohesion score 0.14035087719298245 - nodes in this community are weakly interconnected._