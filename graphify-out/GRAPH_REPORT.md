# Graph Report - optcg-manifest  (2026-10-12)

## Corpus Check
- 148 files · ~422,698 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2135 nodes · 4610 edges · 125 communities (112 shown, 13 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 213 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `50daa1f0`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Manifest
- Manifest.Services
- cards.js
- CardRepository
- BinderRepository
- AccessTests
- CatalogScraper
- ExternalWorkerTests
- decks.js
- manifest.js
- ISessionRepository
- JobQueue
- make_cert.sh
- .Normalise
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
- .CreateTestDatabase
- IJobHandler
- AccessRepository
- SqliteToPostgres
- Manifest Scaling Plan
- .Add
- RedisLoginThrottle
- LimitlessScraper
- AGENTS.md
- backup-postgres.sh
- AccessRequest
- .MapAuth
- PrometheusExporter
- .CatalogRowById
- ContentSecurityTests
- SqlDialect
- DeckDetail
- ImageCache
- BinderTests
- Keyset
- .Open
- Telemetry
- TcgCsv
- .Ready
- binders.js
- CancellationToken
- .Start
- Manifest
- .Initialise
- Backups and restore
- PriceRefresh
- Manifest.Tests
- .UsernamesAreCaseInsensitive
- WebAssets
- User
- .Create
- README.md
- CustomPrintRepository
- collection.js
- MetricsTests
- scan.js
- .Build
- ICardRepository
- Before the day
- Cards.cs
- Manifest.Data
- ListingRepository
- LimitlessPrints
- MailSettings
- api.js
- ExtraPrintsTests
- Options
- Launch checklist
- selling.js
- verify-backup.sh
- alertmanager.sh
- run.sh
- .AFetchedPictureLandsInTheBucket
- .Read
- DatabaseUrlTests
- .Announce
- ServerFixture
- CatalogRow
- TlsSetupTests
- AdminDataTests
- ListingTests
- .FromScrape
- Series
- JobWorker
- AppConfig
- CatalogRefresh
- JobSchedule
- JapanesePrintsTests
- ListingWriter
- Guard
- .Run
- .Write
- .EnsureSchema
- Manifest.Tools
- .ReadScope
- AdminEndpoints
- Database
- .Lookup
- JsonSerializerOptions
- .EverythingArrivesAndStillWorks
- .Answer
- .Timed
- Exception
- LenientIntConverter
- .Depth
- SystemClock
- Access.cs
- .InsertCatalog

## God Nodes (most connected - your core abstractions)
1. `Manifest.Data` - 46 edges
2. `CatalogScraper` - 40 edges
3. `Manifest.Services` - 38 edges
4. `AccessRepository` - 37 edges
5. `Database` - 34 edges
6. `CardRepository` - 31 edges
7. `Manifest.Models` - 31 edges
8. `UserRepository` - 30 edges
9. `Manifest.Tests` - 29 edges
10. `ServerFixture` - 29 edges

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

## Communities (125 total, 13 thin omitted)

### Community 0 - "Manifest"
Cohesion: 0.12
Nodes (16): A note on the network, Binders: sharing a collection, Card art, Checking which version you are running, Deploying to a VPS, Files, Getting your data out, Keeping cards and prices current (+8 more)

### Community 1 - "Manifest.Services"
Cohesion: 0.19
Nodes (3): Manifest.Services, Manifest.Models, SessionInfo

### Community 2 - "cards.js"
Cohesion: 0.07
Nodes (56): addedBy(), cardModalHTML(), cardViewHTML(), CATEGORY_ORDER, clearInspector(), collectionActs(), COLOR_CLASS, COLOR_ORDER (+48 more)

### Community 3 - "CardRepository"
Cohesion: 0.15
Nodes (14): DbCommand, BinderScope, DbCommand, Dictionary, IReadOnlyCollection, List, string, CardRepository (+6 more)

### Community 4 - "BinderRepository"
Cohesion: 0.16
Nodes (9): DbConnection, DbDataReader, From, int, List, To, BinderRepository, List (+1 more)

### Community 5 - "AccessTests"
Cohesion: 0.15
Nodes (12): DataReceivedEventArgs, Fact, HttpClient, HttpResponseMessage, int, JsonElement, Process, string (+4 more)

### Community 6 - "CatalogScraper"
Cohesion: 0.07
Nodes (26): IReadOnlySet, Label, ScrapedCard, Fact, string, ScraperTests, AppPaths, GeneratedRegex (+18 more)

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
Cohesion: 0.13
Nodes (9): TimeSpan, ISessionRepository, HashSet, HttpContext, RequestDelegate, string, Task, Auth (+1 more)

### Community 11 - "JobQueue"
Cohesion: 0.12
Nodes (11): DateTime, DbConnection, DbDataReader, IClock, TimeSpan, Backlog, Job, JobQueue (+3 more)

### Community 13 - ".Normalise"
Cohesion: 0.07
Nodes (24): From, To, Dictionary, GeneratedRegex, List, Regex, CardId, Dictionary (+16 more)

### Community 14 - "AccessPages.cs"
Cohesion: 0.27
Nodes (4): string, AccessPages, string, LoginPage

### Community 15 - "Responses"
Cohesion: 0.10
Nodes (14): HttpContext, Key, Task, Responses, HttpContext, string, SecurityHeaders, AppConfig (+6 more)

### Community 16 - ".MapPublic"
Cohesion: 0.16
Nodes (11): Acted, Task, Outbox, AppConfig, HttpContext, IClock, string, Task (+3 more)

### Community 17 - "ServingTests"
Cohesion: 0.19
Nodes (7): HttpMethod, HttpRequestMessage, Action, Fact, HttpResponseMessage, Task, ServingTests

### Community 18 - "Requests.cs"
Cohesion: 0.12
Nodes (16): List, AccessDecisionPost, AccessRequestPost, BinderPost, BulkCardsPost, CollectionPost, CollectionPrintPost, CustomPrintPost (+8 more)

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

### Community 32 - "IJobHandler"
Cohesion: 0.21
Nodes (10): CancellationToken, Task, PurgeAccessTokensHandler, PurgeJobsHandler, PurgeSessionsHandler, RefreshCatalogHandler, RefreshPricesHandler, ScrapeCatalogHandler (+2 more)

### Community 33 - "AccessRepository"
Cohesion: 0.14
Nodes (12): DateTime, DbConnection, DbDataReader, IClock, int, InviteToken, Request, TimeSpan (+4 more)

### Community 34 - "SqliteToPostgres"
Cohesion: 0.20
Nodes (9): Fact, AppPaths, DateTime, DbConnection, Dictionary, HashSet, SqliteConnection, string (+1 more)

### Community 35 - "Manifest Scaling Plan"
Cohesion: 0.14
Nodes (13): Acceptance Criteria, Manifest Scaling Plan, Phase 1: Stabilize Current App, Phase 2: Database Migration to PostgreSQL, Phase 3: Storage and Image Pipeline, Phase 4: Background Jobs, Phase 5: Public Auth, Abuse Protection, and Sessions, Phase 6: API Pagination and Query Scaling (+5 more)

### Community 36 - ".Add"
Cohesion: 0.35
Nodes (9): Body, Fact, HttpClient, HttpStatusCode, JsonElement, Status, string, Task (+1 more)

### Community 37 - "RedisLoginThrottle"
Cohesion: 0.06
Nodes (33): ConfigurationOptions, IClock, IServiceCollection, LuaScript, AppConfig, AppPaths, ServiceSetup, DateTime (+25 more)

### Community 38 - "LimitlessScraper"
Cohesion: 0.09
Nodes (19): Found, Fact, ScrapedCard, string, LimitlessScraperTests, AppPaths, GeneratedRegex, IEnumerable (+11 more)

### Community 41 - "AccessRequest"
Cohesion: 0.14
Nodes (6): List, InviteToken, Request, IAccessRepository, DateTime, AccessRequest

### Community 42 - ".MapAuth"
Cohesion: 0.18
Nodes (7): IQueryCollection, AppConfig, HttpContext, string, Task, WebApplication, Endpoints

### Community 43 - "PrometheusExporter"
Cohesion: 0.15
Nodes (9): KeyValuePair, ConcurrentDictionary, Dictionary, object, Kind, PrometheusExporter, Spec, MeterListener (+1 more)

### Community 44 - ".CatalogRowById"
Cohesion: 0.22
Nodes (7): Band, DbConnection, IReadOnlyList, DeckRepository, HashSet, Band, DeckAnalysis

### Community 45 - "ContentSecurityTests"
Cohesion: 0.32
Nodes (6): Fact, HttpResponseMessage, InlineData, Task, Theory, ContentSecurityTests

### Community 46 - "SqlDialect"
Cohesion: 0.15
Nodes (7): DbParameter, DateTime, DbCommand, DbConnection, Exception, CommandExtensions, SqlDialect

### Community 47 - "DeckDetail"
Cohesion: 0.15
Nodes (12): List, IDeckRepository, Dictionary, List, BuyListItem, DeckCardRow, DeckDetail, DeckListItem (+4 more)

### Community 48 - "ImageCache"
Cohesion: 0.16
Nodes (10): byte, DateTime, DbConnection, HttpClient, IClock, TimeSpan, FetchPayload, ImageCache (+2 more)

### Community 49 - "BinderTests"
Cohesion: 0.35
Nodes (10): Body, Dictionary, Fact, HttpClient, HttpStatusCode, JsonElement, Status, string (+2 more)

### Community 50 - "Keyset"
Cohesion: 0.16
Nodes (8): Key, DbCommand, DbDataReader, Func, int, Key, Keyset, Kind

### Community 51 - ".Open"
Cohesion: 0.15
Nodes (8): DateTime, DbConnection, DbDataReader, IClock, List, string, TimeSpan, UserRepository

### Community 52 - "Telemetry"
Cohesion: 0.16
Nodes (10): Backlog, Counter, Histogram, bool, double, long, object, string (+2 more)

### Community 53 - "TcgCsv"
Cohesion: 0.14
Nodes (18): Group, Product, Dictionary, Func, GeneratedRegex, HttpClient, IEnumerable, IReadOnlyDictionary (+10 more)

### Community 54 - ".Ready"
Cohesion: 0.22
Nodes (12): Component, AppConfig, Body, CancellationToken, IConnectionMultiplexer, IReadOnlyDictionary, Status, Task (+4 more)

### Community 55 - "binders.js"
Cohesion: 0.20
Nodes (18): applyBinder(), binderManagerHTML(), BINDERS, binderWritable(), currentBinder(), loadBinders(), moveActs(), openBinderManager() (+10 more)

### Community 56 - "CancellationToken"
Cohesion: 0.24
Nodes (9): Detail, IAmazonS3, CancellationToken, string, Task, IImageStore, LocalImageStore, S3ImageStore (+1 more)

### Community 57 - ".Start"
Cohesion: 0.13
Nodes (9): string, Cli, AppConfig, ILogger, IPEndPoint, Task, WebApplication, MetricsServer (+1 more)

### Community 58 - "Manifest"
Cohesion: 0.11
Nodes (8): Manifest, AppPaths, JsonSerializerOptions, Json, HashSet, Net, string, WorkerHost

### Community 59 - ".Initialise"
Cohesion: 0.33
Nodes (4): AppPaths, Fact, string, CatalogSeedTests

### Community 60 - "Backups and restore"
Cohesion: 0.18
Nodes (11): Alerts, Backups and restore, Latest result, Load testing, Metrics, Nightly dumps, a weekly test restore, and an off-site copy, Object storage, Restoring (+3 more)

### Community 61 - "PriceRefresh"
Cohesion: 0.07
Nodes (31): ApiCard, CatalogPrint, Link, Fact, InlineData, Theory, PriceMatchTests, AppPaths (+23 more)

### Community 62 - "Manifest.Tests"
Cohesion: 0.12
Nodes (3): Manifest.Tests, NoOutboundJobs, ModuleInitializer

### Community 63 - ".UsernamesAreCaseInsensitive"
Cohesion: 0.33
Nodes (4): Fact, StringContent, Task, AccountTests

### Community 64 - "WebAssets"
Cohesion: 0.22
Nodes (8): Asset, Assembly, Dictionary, HttpContext, string, Task, Asset, WebAssets

### Community 66 - ".Create"
Cohesion: 0.21
Nodes (4): int, Passwords, string, UserAdmin

### Community 67 - "README.md"
Cohesion: 0.25
Nodes (6): OPTCGManifest, OPTCGManifest, OPTCGManifest, OPTCGManifest, OPTCGManifest, OPTCGManifest

### Community 68 - "CustomPrintRepository"
Cohesion: 0.18
Nodes (8): DbConnection, GeneratedRegex, IReadOnlyList, List, Regex, string, CustomPrintInput, CustomPrintRepository

### Community 69 - "collection.js"
Cohesion: 0.33
Nodes (9): doSearch(), flash(), loadOwned(), logPager, ownPager, paintSetBreak(), quickAdd(), refreshSetBreak() (+1 more)

### Community 70 - "MetricsTests"
Cohesion: 0.18
Nodes (8): Fact, HttpClient, int, Process, string, Task, MetricsTests, PrometheusExporterTests

### Community 71 - "scan.js"
Cohesion: 0.29
Nodes (6): awaitScan(), camConstraints, refocus(), say(), send(), showScanPrints()

### Community 72 - ".Build"
Cohesion: 0.15
Nodes (14): GeneratedRegex, HashSet, IEnumerable, IReadOnlyList, Print, Product, Regex, ScrapedCard (+6 more)

### Community 73 - "ICardRepository"
Cohesion: 0.18
Nodes (4): From, List, To, ICardRepository

### Community 74 - "Before the day"
Cohesion: 0.29
Nodes (7): Backups, Before the day, Capacity, Configuration, Monitoring, Security, Server and network

### Community 75 - "Cards.cs"
Cohesion: 0.13
Nodes (11): DbConnection, IReadOnlyList, IReadOnlyList, List, AdjustResult, BulkLogResult, CardDetailRow, CardQuantity (+3 more)

### Community 76 - "Manifest.Data"
Cohesion: 0.12
Nodes (8): Manifest.Web, Manifest.Data, Manifest.Services.Jobs, EmailPayload, int, string, JobPriority, JobTypes

### Community 77 - "ListingRepository"
Cohesion: 0.13
Nodes (14): Action, DateTime, DbCommand, DbConnection, IEnumerable, int, List, string (+6 more)

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

### Community 85 - "selling.js"
Cohesion: 0.19
Nodes (25): composerHTML(), CONDITIONS, copyText(), day(), LISTING_STATUS, listingBody(), listingRowHTML(), LISTINGS (+17 more)

### Community 89 - ".AFetchedPictureLandsInTheBucket"
Cohesion: 0.33
Nodes (5): AppConfig, SkippableFact, string, Task, ObjectStorageTests

### Community 90 - ".Read"
Cohesion: 0.32
Nodes (4): string, Task, ApiScanner, MediaType

### Community 91 - "DatabaseUrlTests"
Cohesion: 0.22
Nodes (6): IReadOnlyList, AppPaths, Fact, InlineData, Theory, DatabaseUrlTests

### Community 92 - ".Announce"
Cohesion: 0.39
Nodes (4): AccessMail, Message, Submission, Message

### Community 93 - "ServerFixture"
Cohesion: 0.15
Nodes (11): ICollectionFixture, Body, HttpClient, JsonElement, Process, Status, string, StringContent (+3 more)

### Community 94 - "CatalogRow"
Cohesion: 0.24
Nodes (7): DbDataReader, CatalogRow, SourcePriceRow, List, ItemSpecific, ListingDraft, List

### Community 95 - "TlsSetupTests"
Cohesion: 0.20
Nodes (8): IAsyncLifetime, bool, int, Process, SkippableFact, string, Task, TlsSetupTests

### Community 96 - "AdminDataTests"
Cohesion: 0.38
Nodes (4): Fact, StringContent, Task, AdminDataTests

### Community 97 - "ListingTests"
Cohesion: 0.31
Nodes (9): Body, Fact, HttpClient, HttpStatusCode, JsonElement, Status, string, Task (+1 more)

### Community 98 - ".FromScrape"
Cohesion: 0.33
Nodes (5): Dictionary, IReadOnlyList, List, string, JapanesePrints

### Community 99 - "Series"
Cohesion: 0.22
Nodes (8): Instrument, Kind, bool, double, long, string, Series, Spec

### Community 100 - "JobWorker"
Cohesion: 0.27
Nodes (8): BackgroundService, CancellationToken, Dictionary, ILogger, int, Task, JobWorker, SemaphoreSlim

### Community 101 - "AppConfig"
Cohesion: 0.17
Nodes (9): HashSet, int, IPEndPoint, long, string, TimeSpan, AppConfig, InlineData (+1 more)

### Community 102 - "CatalogRefresh"
Cohesion: 0.17
Nodes (10): AppPaths, JsonSerializerOptions, List, string, Task, CatalogRefresh, TitleParts, VegapullCard (+2 more)

### Community 103 - "JobSchedule"
Cohesion: 0.29
Nodes (6): CancellationToken, IClock, ILogger, List, Task, JobSchedule

### Community 104 - "JapanesePrintsTests"
Cohesion: 0.39
Nodes (4): Fact, List, string, JapanesePrintsTests

### Community 105 - "ListingWriter"
Cohesion: 0.19
Nodes (10): Code, Dictionary, GeneratedRegex, HashSet, int, Name, Regex, string (+2 more)

### Community 106 - "Guard"
Cohesion: 0.48
Nodes (4): HttpContext, RequestDelegate, Task, Guard

### Community 107 - ".Run"
Cohesion: 0.33
Nodes (4): AppPaths, string, Task, PrintAdmin

### Community 108 - ".Write"
Cohesion: 0.40
Nodes (3): IEnumerable, StringBuilder, CsvExport

### Community 109 - ".EnsureSchema"
Cohesion: 0.15
Nodes (10): Assembly, DbConnection, IReadOnlyList, List, string, PostgresMigrations, SkippableFact, Task (+2 more)

### Community 111 - ".ReadScope"
Cohesion: 0.25
Nodes (5): Func, HttpContext, Task, WebApplication, BinderEndpoints

### Community 112 - "AdminEndpoints"
Cohesion: 0.19
Nodes (9): Dictionary, HttpContext, Task, TimeSpan, WebApplication, AdminEndpoints, Priced, PricedAt (+1 more)

### Community 113 - "Database"
Cohesion: 0.23
Nodes (7): AppPaths, long, SqliteConnection, string, Database, NpgsqlConnectionStringBuilder, NpgsqlDataSource

### Community 114 - ".Lookup"
Cohesion: 0.22
Nodes (4): CancellationToken, Task, Fact, Result

### Community 115 - "JsonSerializerOptions"
Cohesion: 0.27
Nodes (7): JsonConverter, JsonSerializerOptions, Type, Utf8JsonReader, Utf8JsonWriter, PythonStyleDoubleConverter, PythonStyleNullableDoubleConverter

### Community 116 - ".EverythingArrivesAndStillWorks"
Cohesion: 0.39
Nodes (5): AppPaths, List, SkippableFact, string, MigrateSqliteTests

### Community 117 - ".Answer"
Cohesion: 0.25
Nodes (7): ListingInput, Func, HttpContext, Task, WebApplication, ListingEndpoints, ListingPost

### Community 118 - ".Timed"
Cohesion: 0.36
Nodes (6): ScanResponse, VisionReading, CancellationToken, Task, ScanService, ScanPost

### Community 119 - "Exception"
Cohesion: 0.22
Nodes (8): Exception, BinderError, BinderRights, CustomPrintError, ListingError, AccountError, RuleViolation, MigrationError

### Community 120 - "LenientIntConverter"
Cohesion: 0.33
Nodes (5): JsonSerializerOptions, Type, Utf8JsonReader, Utf8JsonWriter, LenientIntConverter

### Community 121 - ".Depth"
Cohesion: 0.50
Nodes (3): Failed, Queued, Running

### Community 122 - "SystemClock"
Cohesion: 1.00
Nodes (3): DateTime, IClock, SystemClock

### Community 123 - "Access.cs"
Cohesion: 0.50
Nodes (3): string, AccessRequestView, AccessStatus

## Knowledge Gaps
- **160 isolated node(s):** `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)`, `xunit.runner.visualstudio (2.8.2)`, `Xunit.SkippableFact (1.5.61)` (+155 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **13 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Manifest.Tests` connect `Manifest.Tests` to `AdminDataTests`, `Manifest.Services`, `AccessTests`, `MetricsTests`, `ExternalWorkerTests`, `Manifest.Data`, `.Normalise`, `Manifest.Tools`, `ServerFixture`, `TlsSetupTests`, `.UsernamesAreCaseInsensitive`?**
  _High betweenness centrality (0.208) - this node is a cross-community bridge._
- **Why does `Manifest.Data` connect `Manifest.Data` to `Manifest.Services`, `JobQueue`, `.EnsureSchema`, `SqlDialect`, `Manifest.Tools`, `Keyset`, `Exception`, `ReaderExtensions`, `Manifest`, `Manifest.Tests`?**
  _High betweenness centrality (0.115) - this node is a cross-community bridge._
- **Why does `Database` connect `Database` to `Manifest.Services`, `CardRepository`, `BinderRepository`, `JobQueue`, `.Normalise`, `AccessRepository`, `SqliteToPostgres`, `RedisLoginThrottle`, `.CatalogRowById`, `SqlDialect`, `ImageCache`, `.Open`, `.Ready`, `.Initialise`, `PriceRefresh`, `.Create`, `DatabaseUrlTests`, `.EnsureSchema`, `AdminEndpoints`, `.EverythingArrivesAndStillWorks`, `.InsertCatalog`?**
  _High betweenness centrality (0.068) - this node is a cross-community bridge._
- **What connects `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)` to the rest of the system?**
  _160 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Manifest` be split into smaller, more focused modules?**
  _Cohesion score 0.125 - nodes in this community are weakly interconnected._
- **Should `cards.js` be split into smaller, more focused modules?**
  _Cohesion score 0.07213114754098361 - nodes in this community are weakly interconnected._
- **Should `CatalogScraper` be split into smaller, more focused modules?**
  _Cohesion score 0.07017543859649122 - nodes in this community are weakly interconnected._