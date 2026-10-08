# Graph Report - optcg-manifest  (2026-10-07)

## Corpus Check
- 137 files · ~412,538 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1953 nodes · 4154 edges · 113 communities (93 shown, 20 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 206 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `541316c5`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Manifest
- Manifest.Services
- cards.js
- BinderScope
- BinderRepository
- AccessTests
- CatalogScraper
- ExternalWorkerTests
- decks.js
- manifest.js
- ISessionRepository
- JobQueue
- make_cert.sh
- CardId
- AccessPages.cs
- Responses
- .MapPublic
- ServingTests
- Requests.cs
- CatalogAndLookupTests
- DeckTests
- CollectionTests
- Manifest.csproj
- entrypoint.sh
- ReaderExtensions
- Manifest — server
- CLAUDE.md
- seaweedfs.sh
- PaginationTests
- JobQueueTests
- ServerFixture
- Job
- AccessRepository
- CardRepository
- Manifest Scaling Plan
- .EnsureSchema
- RedisLoginThrottle
- LimitlessScraper
- AGENTS.md
- backup-postgres.sh
- AccessRequest
- .MapAuth
- PrometheusExporter
- .Open
- ContentSecurityTests
- SqlDialect
- DeckDetail
- ImageCache
- BinderTests
- Keyset
- UserRepository
- Telemetry
- TcgCsv
- AdminEndpoints
- binders.js
- Database
- .Start
- Manifest
- .Initialise
- Backups and restore
- PriceRefresh
- Manifest.Tests
- .UsernamesAreCaseInsensitive
- WebAssets
- User
- UserAdmin
- README.md
- RepositoryInterfaces.cs
- collection.js
- .Create
- scan.js
- .Build
- MetricsTests.cs
- Before the day
- ICardRepository
- Manifest.Data
- AdminDataTests
- LimitlessPrints
- MailSettings
- api.js
- ExtraPrintsTests
- Options
- Launch checklist
- Series
- verify-backup.sh
- alertmanager.sh
- run.sh
- Net
- .Read
- DatabaseUrlTests
- AccessMail
- Task
- .Of
- TlsSetupTests
- MetricsTests
- LoginPage.cs
- Manifest.Tools
- DeckListParser
- JobWorker
- AppConfig
- CatalogRefresh
- JobSchedule
- HttpContext
- DeckAnalysis
- Guard
- .ReadLinks
- .Write
- .Depth
- .TheNewNumbersSurviveNormalising
- .TheListenSettingIsAPortOrAnAddress

## God Nodes (most connected - your core abstractions)
1. `Manifest.Data` - 42 edges
2. `CatalogScraper` - 40 edges
3. `AccessRepository` - 37 edges
4. `Manifest.Services` - 34 edges
5. `Database` - 33 edges
6. `CardRepository` - 31 edges
7. `UserRepository` - 30 edges
8. `ServerFixture` - 29 edges
9. `LimitlessScraper` - 29 edges
10. `Manifest.Tests` - 27 edges

## Surprising Connections (you probably didn't know these)
- `JapanesePrintsTests` --references--> `CatalogRow`  [EXTRACTED]
  Manifest.Tests/JapanesePrintsTests.cs → Manifest/Models/Cards.cs
- `Guard` --references--> `AppConfig`  [EXTRACTED]
  Manifest/Web/Guard.cs → Manifest/AppConfig.cs
- `AccessRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/AccessRepository.cs → Manifest/Data/Database.cs
- `AccessRepository` --implements--> `IAccessRepository`  [EXTRACTED]
  Manifest/Data/AccessRepository.cs → Manifest/Data/RepositoryInterfaces.cs
- `BinderRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/BinderRepository.cs → Manifest/Data/Database.cs

## Import Cycles
- None detected.

## Communities (113 total, 20 thin omitted)

### Community 0 - "Manifest"
Cohesion: 0.13
Nodes (15): A note on the network, Binders: sharing a collection, Card art, Checking which version you are running, Deploying to a VPS, Files, Getting your data out, Keeping cards and prices current (+7 more)

### Community 1 - "Manifest.Services"
Cohesion: 0.14
Nodes (8): Manifest.Services, Manifest.Models, Exception, BinderError, BinderRights, AccountError, RuleViolation, MigrationError

### Community 2 - "cards.js"
Cohesion: 0.08
Nodes (50): addedBy(), cardModalHTML(), cardViewHTML(), CATEGORY_ORDER, collectionActs(), COLOR_CLASS, COLOR_ORDER, CURRENCY_SIGN (+42 more)

### Community 3 - "BinderScope"
Cohesion: 0.16
Nodes (9): DbCommand, BinderScope, DbCommand, List, List, IReadOnlyList, CardFilter, CollectionRow (+1 more)

### Community 4 - "BinderRepository"
Cohesion: 0.06
Nodes (28): DbConnection, DbDataReader, From, int, List, To, BinderRepository, List (+20 more)

### Community 5 - "AccessTests"
Cohesion: 0.15
Nodes (12): DataReceivedEventArgs, Fact, HttpClient, HttpResponseMessage, int, JsonElement, Process, string (+4 more)

### Community 6 - "CatalogScraper"
Cohesion: 0.05
Nodes (35): IReadOnlySet, Label, Dictionary, IReadOnlyList, List, string, JapanesePrints, ScrapedCard (+27 more)

### Community 7 - "ExternalWorkerTests"
Cohesion: 0.12
Nodes (16): HttpListener, IDisposable, byte, Fact, HttpClient, HttpResponseMessage, int, JsonElement (+8 more)

### Community 8 - "decks.js"
Cohesion: 0.19
Nodes (27): deckBrowsePager, deckListPager, deckPrintFor(), deckQtyOf(), deckQtyOfBase(), doDeckSearch(), doLeaderSearch(), fillPreviewPrints() (+19 more)

### Community 9 - "manifest.js"
Cohesion: 0.10
Nodes (34): ACTIVE, asMe(), BASE, browse(), COLORS, contention, contentionSent, deck (+26 more)

### Community 10 - "ISessionRepository"
Cohesion: 0.18
Nodes (7): TimeSpan, ISessionRepository, HashSet, RequestDelegate, string, Task, Auth

### Community 11 - "JobQueue"
Cohesion: 0.14
Nodes (9): DateTime, DbConnection, IClock, TimeSpan, Backlog, JobQueue, Recent, List (+1 more)

### Community 13 - "CardId"
Cohesion: 0.10
Nodes (15): Dictionary, GeneratedRegex, List, Regex, CardId, AppPaths, IReadOnlyList, string (+7 more)

### Community 15 - "Responses"
Cohesion: 0.10
Nodes (14): HttpContext, Key, Task, Responses, HttpContext, string, SecurityHeaders, AppConfig (+6 more)

### Community 16 - ".MapPublic"
Cohesion: 0.16
Nodes (12): Acted, Task, Outbox, AppConfig, HttpContext, IClock, string, Submission (+4 more)

### Community 17 - "ServingTests"
Cohesion: 0.19
Nodes (7): HttpMethod, HttpRequestMessage, Action, Fact, HttpResponseMessage, Task, ServingTests

### Community 18 - "Requests.cs"
Cohesion: 0.06
Nodes (33): JsonConverter, JsonSerializerOptions, Type, Utf8JsonReader, Utf8JsonWriter, PythonStyleDoubleConverter, PythonStyleNullableDoubleConverter, ScanResponse (+25 more)

### Community 19 - "CatalogAndLookupTests"
Cohesion: 0.24
Nodes (4): Fact, JsonElement, Task, CatalogAndLookupTests

### Community 20 - "DeckTests"
Cohesion: 0.28
Nodes (7): Deck, Id, Fact, JsonElement, string, Task, DeckTests

### Community 21 - "CollectionTests"
Cohesion: 0.28
Nodes (3): Fact, Task, CollectionTests

### Community 22 - "Manifest.csproj"
Cohesion: 0.12
Nodes (14): net8.0, net8.0, Anthropic (12.40.0), AWSSDK.S3 (3.7.410.4), MailKit (4.17.0), Microsoft.Data.Sqlite (8.0.11), Microsoft.NET.Test.Sdk (17.11.1), Npgsql (8.0.6) (+6 more)

### Community 24 - "ReaderExtensions"
Cohesion: 0.29
Nodes (3): DbDataReader, string, ReaderExtensions

### Community 25 - "Manifest — server"
Cohesion: 0.33
Nodes (5): Layout, Manifest — server, Notes, Running it, Tests

### Community 28 - "PaginationTests"
Cohesion: 0.27
Nodes (9): Action, Fact, IEnumerable, InlineData, JsonElement, List, Task, Theory (+1 more)

### Community 29 - "JobQueueTests"
Cohesion: 0.25
Nodes (8): Clock, IEnumerable, string, Task, JobQueueTests, MemberData, Queue, SkippableTheory

### Community 31 - "ServerFixture"
Cohesion: 0.16
Nodes (8): ICollectionFixture, Process, string, StringContent, ServerCollection, ServerFixture, Name, Url

### Community 32 - "Job"
Cohesion: 0.17
Nodes (12): DbDataReader, Job, CancellationToken, Task, PurgeAccessTokensHandler, PurgeJobsHandler, PurgeSessionsHandler, RefreshCatalogHandler (+4 more)

### Community 33 - "AccessRepository"
Cohesion: 0.14
Nodes (11): DateTime, DbConnection, DbDataReader, IClock, int, InviteToken, Request, TimeSpan (+3 more)

### Community 34 - "CardRepository"
Cohesion: 0.22
Nodes (7): Dictionary, From, IReadOnlyCollection, Key, string, To, CardRepository

### Community 35 - "Manifest Scaling Plan"
Cohesion: 0.14
Nodes (13): Acceptance Criteria, Manifest Scaling Plan, Phase 1: Stabilize Current App, Phase 2: Database Migration to PostgreSQL, Phase 3: Storage and Image Pipeline, Phase 4: Background Jobs, Phase 5: Public Auth, Abuse Protection, and Sessions, Phase 6: API Pagination and Query Scaling (+5 more)

### Community 36 - ".EnsureSchema"
Cohesion: 0.15
Nodes (10): Assembly, DbConnection, IReadOnlyList, List, string, PostgresMigrations, SkippableFact, Task (+2 more)

### Community 37 - "RedisLoginThrottle"
Cohesion: 0.06
Nodes (33): ConfigurationOptions, IClock, IServiceCollection, LuaScript, AppConfig, AppPaths, ServiceSetup, DateTime (+25 more)

### Community 38 - "LimitlessScraper"
Cohesion: 0.09
Nodes (19): Found, Fact, ScrapedCard, string, LimitlessScraperTests, AppPaths, GeneratedRegex, IEnumerable (+11 more)

### Community 41 - "AccessRequest"
Cohesion: 0.13
Nodes (6): List, InviteToken, Request, Submission, IAccessRepository, AccessRequest

### Community 42 - ".MapAuth"
Cohesion: 0.20
Nodes (7): IQueryCollection, AppConfig, HttpContext, string, Task, WebApplication, Endpoints

### Community 43 - "PrometheusExporter"
Cohesion: 0.16
Nodes (9): KeyValuePair, ConcurrentDictionary, Dictionary, object, Kind, PrometheusExporter, Spec, MeterListener (+1 more)

### Community 44 - ".Open"
Cohesion: 0.20
Nodes (8): DbConnection, DbDataReader, DbConnection, DbConnection, IReadOnlyList, List, DeckRepository, CatalogRow

### Community 45 - "ContentSecurityTests"
Cohesion: 0.32
Nodes (6): Fact, HttpResponseMessage, InlineData, Task, Theory, ContentSecurityTests

### Community 46 - "SqlDialect"
Cohesion: 0.15
Nodes (7): DbParameter, DateTime, DbCommand, DbConnection, Exception, CommandExtensions, SqlDialect

### Community 47 - "DeckDetail"
Cohesion: 0.19
Nodes (11): IDeckRepository, Dictionary, List, BuyListItem, DeckCardRow, DeckDetail, DeckListItem, Guideline (+3 more)

### Community 48 - "ImageCache"
Cohesion: 0.05
Nodes (39): Component, Detail, IAmazonS3, byte, CancellationToken, DateTime, DbConnection, HttpClient (+31 more)

### Community 49 - "BinderTests"
Cohesion: 0.35
Nodes (10): HttpStatusCode, Body, Dictionary, Fact, HttpClient, JsonElement, Status, string (+2 more)

### Community 50 - "Keyset"
Cohesion: 0.16
Nodes (9): Kind, DbCommand, DbDataReader, Func, int, Key, Keyset, Kind (+1 more)

### Community 51 - "UserRepository"
Cohesion: 0.20
Nodes (6): DateTime, DbConnection, IClock, string, TimeSpan, UserRepository

### Community 52 - "Telemetry"
Cohesion: 0.16
Nodes (10): Backlog, Counter, Histogram, bool, double, long, object, string (+2 more)

### Community 53 - "TcgCsv"
Cohesion: 0.14
Nodes (18): Group, Product, Dictionary, Func, GeneratedRegex, HttpClient, IEnumerable, IReadOnlyDictionary (+10 more)

### Community 54 - "AdminEndpoints"
Cohesion: 0.19
Nodes (9): Dictionary, HttpContext, Task, TimeSpan, WebApplication, AdminEndpoints, Priced, PricedAt (+1 more)

### Community 55 - "binders.js"
Cohesion: 0.20
Nodes (18): applyBinder(), binderManagerHTML(), BINDERS, binderWritable(), currentBinder(), loadBinders(), moveActs(), openBinderManager() (+10 more)

### Community 56 - "Database"
Cohesion: 0.23
Nodes (7): AppPaths, long, SqliteConnection, string, Database, NpgsqlConnectionStringBuilder, NpgsqlDataSource

### Community 57 - ".Start"
Cohesion: 0.13
Nodes (9): string, Cli, AppConfig, ILogger, IPEndPoint, Task, WebApplication, MetricsServer (+1 more)

### Community 58 - "Manifest"
Cohesion: 0.15
Nodes (6): Manifest, AppPaths, JsonSerializerOptions, Json, string, WorkerHost

### Community 59 - ".Initialise"
Cohesion: 0.33
Nodes (4): AppPaths, Fact, string, CatalogSeedTests

### Community 60 - "Backups and restore"
Cohesion: 0.18
Nodes (11): Alerts, Backups and restore, Latest result, Load testing, Metrics, Nightly dumps, a weekly test restore, and an off-site copy, Object storage, Restoring (+3 more)

### Community 61 - "PriceRefresh"
Cohesion: 0.08
Nodes (27): ApiCard, CatalogPrint, Fact, InlineData, Theory, PriceMatchTests, AppPaths, Dictionary (+19 more)

### Community 62 - "Manifest.Tests"
Cohesion: 0.13
Nodes (3): Manifest.Tests, NoOutboundJobs, ModuleInitializer

### Community 63 - ".UsernamesAreCaseInsensitive"
Cohesion: 0.33
Nodes (4): Fact, StringContent, Task, AccountTests

### Community 64 - "WebAssets"
Cohesion: 0.22
Nodes (8): Asset, Assembly, Dictionary, HttpContext, string, Task, Asset, WebAssets

### Community 65 - "User"
Cohesion: 0.15
Nodes (5): IUserRepository, DbDataReader, List, SessionInfo, User

### Community 67 - "README.md"
Cohesion: 0.25
Nodes (6): OPTCGManifest, OPTCGManifest, OPTCGManifest, OPTCGManifest, OPTCGManifest, OPTCGManifest

### Community 69 - "collection.js"
Cohesion: 0.33
Nodes (9): doSearch(), flash(), loadOwned(), logPager, ownPager, paintSetBreak(), quickAdd(), refreshSetBreak() (+1 more)

### Community 71 - "scan.js"
Cohesion: 0.29
Nodes (6): awaitScan(), camConstraints, refocus(), say(), send(), showScanPrints()

### Community 72 - ".Build"
Cohesion: 0.15
Nodes (14): GeneratedRegex, HashSet, IEnumerable, IReadOnlyList, Print, Product, Regex, ScrapedCard (+6 more)

### Community 74 - "Before the day"
Cohesion: 0.29
Nodes (7): Backups, Before the day, Capacity, Configuration, Monitoring, Security, Server and network

### Community 75 - "ICardRepository"
Cohesion: 0.11
Nodes (14): IReadOnlyList, From, IReadOnlyList, To, ICardRepository, List, AdjustResult, BulkLogResult (+6 more)

### Community 76 - "Manifest.Data"
Cohesion: 0.15
Nodes (8): Manifest.Web, Manifest.Data, Manifest.Services.Jobs, EmailPayload, int, string, JobPriority, JobTypes

### Community 77 - "AdminDataTests"
Cohesion: 0.38
Nodes (4): Fact, StringContent, Task, AdminDataTests

### Community 78 - "LimitlessPrints"
Cohesion: 0.24
Nodes (8): Print, GeneratedRegex, List, Regex, Task, TimeSpan, LimitlessPrints, Print

### Community 79 - "MailSettings"
Cohesion: 0.23
Nodes (6): Task, TimeSpan, Mailer, MailOutcome, MailSettings, SecureSocketOptions

### Community 80 - "api.js"
Cohesion: 0.50
Nodes (3): api(), cardIndex, scoped()

### Community 81 - "ExtraPrintsTests"
Cohesion: 0.22
Nodes (4): Fact, JsonElement, string, ExtraPrintsTests

### Community 83 - "Options"
Cohesion: 0.40
Nodes (5): Background jobs and the worker, Card art in object storage, More than one app container: Redis, Options, PostgreSQL instead of SQLite

### Community 84 - "Launch checklist"
Cohesion: 0.50
Nodes (4): Launch checklist, On the day, Rolling back, The first week

### Community 85 - "Series"
Cohesion: 0.25
Nodes (7): Instrument, bool, double, long, string, Series, Spec

### Community 90 - ".Read"
Cohesion: 0.32
Nodes (4): string, Task, ApiScanner, MediaType

### Community 91 - "DatabaseUrlTests"
Cohesion: 0.22
Nodes (6): IReadOnlyList, AppPaths, Fact, InlineData, Theory, DatabaseUrlTests

### Community 92 - "AccessMail"
Cohesion: 0.52
Nodes (3): AccessMail, Message, Message

### Community 93 - "Task"
Cohesion: 0.23
Nodes (5): Body, HttpClient, JsonElement, Status, Task

### Community 94 - ".Of"
Cohesion: 0.24
Nodes (7): DateTime, IClock, SystemClock, DateTime, string, AccessRequestView, AccessStatus

### Community 95 - "TlsSetupTests"
Cohesion: 0.20
Nodes (8): IAsyncLifetime, bool, int, Process, SkippableFact, string, Task, TlsSetupTests

### Community 96 - "MetricsTests"
Cohesion: 0.28
Nodes (7): Fact, HttpClient, int, Process, string, Task, MetricsTests

### Community 99 - "DeckListParser"
Cohesion: 0.31
Nodes (6): Dictionary, GeneratedRegex, IEnumerable, List, Regex, DeckListParser

### Community 100 - "JobWorker"
Cohesion: 0.24
Nodes (8): BackgroundService, CancellationToken, Dictionary, ILogger, int, Task, JobWorker, SemaphoreSlim

### Community 101 - "AppConfig"
Cohesion: 0.23
Nodes (7): HashSet, int, IPEndPoint, long, string, TimeSpan, AppConfig

### Community 102 - "CatalogRefresh"
Cohesion: 0.17
Nodes (10): AppPaths, JsonSerializerOptions, List, string, Task, CatalogRefresh, TitleParts, VegapullCard (+2 more)

### Community 103 - "JobSchedule"
Cohesion: 0.29
Nodes (6): CancellationToken, IClock, ILogger, List, Task, JobSchedule

### Community 105 - "DeckAnalysis"
Cohesion: 0.43
Nodes (4): Band, HashSet, Band, DeckAnalysis

### Community 106 - "Guard"
Cohesion: 0.48
Nodes (4): HttpContext, RequestDelegate, Task, Guard

### Community 107 - ".ReadLinks"
Cohesion: 0.33
Nodes (4): Link, AppPaths, List, Task

### Community 108 - ".Write"
Cohesion: 0.40
Nodes (3): IEnumerable, StringBuilder, CsvExport

### Community 110 - ".Depth"
Cohesion: 0.50
Nodes (3): Failed, Queued, Running

## Knowledge Gaps
- **156 isolated node(s):** `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)`, `xunit.runner.visualstudio (2.8.2)`, `Xunit.SkippableFact (1.5.61)` (+151 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **20 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Manifest.Tests` connect `Manifest.Tests` to `Manifest.Tools`, `BinderRepository`, `AccessTests`, `ExternalWorkerTests`, `MetricsTests.cs`, `Manifest.Data`, `CardId`, `AdminDataTests`, `ServerFixture`, `TlsSetupTests`, `.UsernamesAreCaseInsensitive`?**
  _High betweenness centrality (0.220) - this node is a cross-community bridge._
- **Why does `Manifest.Data` connect `Manifest.Data` to `Job`, `Manifest.Services`, `Manifest.Tools`, `UserAdmin`, `.EnsureSchema`, `RepositoryInterfaces.cs`, `BinderRepository`, `SqlDialect`, `Keyset`, `ReaderExtensions`, `Manifest`, `Manifest.Tests`?**
  _High betweenness centrality (0.117) - this node is a cross-community bridge._
- **Why does `Manifest.Web` connect `Manifest.Data` to `WebAssets`, `LoginPage.cs`, `RedisLoginThrottle`, `AccessPages.cs`, `Responses`, `Requests.cs`, `Manifest`?**
  _High betweenness centrality (0.074) - this node is a cross-community bridge._
- **What connects `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)` to the rest of the system?**
  _156 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Manifest` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._
- **Should `Manifest.Services` be split into smaller, more focused modules?**
  _Cohesion score 0.14285714285714285 - nodes in this community are weakly interconnected._
- **Should `cards.js` be split into smaller, more focused modules?**
  _Cohesion score 0.07662337662337662 - nodes in this community are weakly interconnected._