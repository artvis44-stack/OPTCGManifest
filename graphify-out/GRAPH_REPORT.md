# Graph Report - stage-11-load-metrics  (2026-10-05)

## Corpus Check
- 113 files · ~275,810 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1505 nodes · 2971 edges · 90 communities (74 shown, 16 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 145 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `9068d9a9`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Manifest
- Manifest.Data
- cards.js
- .Normalise
- .Open
- AccessTests
- CatalogScraper
- ExternalWorkerTests
- decks.js
- manifest.js
- ISessionRepository
- JobQueue
- make_cert.sh
- CardId
- Manifest.Web
- AppConfig
- CatalogRefresh
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
- Handlers.cs
- AccessRepository
- CardRepository
- Manifest Scaling Plan
- DatabaseUrlTests
- RedisLoginThrottle
- Database
- AGENTS.md
- backup-postgres.sh
- .Write
- .MapAuth
- PrometheusExporter
- DeckDetail
- ContentSecurityTests
- .Read
- Task
- ImageCache
- DeckAnalysis
- Keyset
- UserRepository
- Telemetry
- TlsSetupTests
- MetricsTests
- MailSettings
- DeckListParser
- .Start
- Manifest
- User
- Backups and restore
- PriceRefresh
- Manifest.Tests
- .UsernamesAreCaseInsensitive
- WebAssets
- JobWorker
- UserAdmin
- README.md
- Series
- collection.js
- .Create
- scan.js
- JobSchedule
- MetricsTests.cs
- Before the day
- WorkerHost.cs
- JobPriority
- .Depth
- SystemClock
- Mailer
- api.js
- UserRepository.cs
- Options
- Launch checklist
- Cli
- verify-backup.sh
- alertmanager.sh
- run.sh
- JobTypes

## God Nodes (most connected - your core abstractions)
1. `AccessRepository` - 37 edges
2. `Manifest.Data` - 36 edges
3. `UserRepository` - 30 edges
4. `CatalogScraper` - 30 edges
5. `ServerFixture` - 29 edges
6. `Database` - 29 edges
7. `CardRepository` - 28 edges
8. `Manifest.Services` - 28 edges
9. `JobQueue` - 24 edges
10. `ServingTests` - 21 edges

## Surprising Connections (you probably didn't know these)
- `AccessRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/AccessRepository.cs → Manifest/Data/Database.cs
- `CardRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/Database.cs
- `CardRepository` --implements--> `ICardRepository`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/RepositoryInterfaces.cs
- `CardRepository` --implements--> `IScanRepository`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/RepositoryInterfaces.cs
- `ApiScanner` --references--> `CardRepository`  [EXTRACTED]
  Manifest/Services/ApiScanner.cs → Manifest/Data/CardRepository.cs

## Import Cycles
- None detected.

## Communities (90 total, 16 thin omitted)

### Community 0 - "Manifest"
Cohesion: 0.14
Nodes (14): A note on the network, Card art, Checking which version you are running, Deploying to a VPS, Files, Getting your data out, Logging cards, Manifest (+6 more)

### Community 1 - "Manifest.Data"
Cohesion: 0.17
Nodes (5): Manifest.Services, Manifest.Data, Manifest.Models, Manifest.Services.Jobs, IScanRepository

### Community 2 - "cards.js"
Cohesion: 0.09
Nodes (40): cardModalHTML(), cardViewHTML(), CATEGORY_ORDER, collectionActs(), COLOR_CLASS, COLOR_ORDER, esc(), fillEffect() (+32 more)

### Community 3 - ".Normalise"
Cohesion: 0.09
Nodes (12): IReadOnlyList, IReadOnlyList, List, ICardRepository, List, AdjustResult, BulkLogResult, CardDetailRow (+4 more)

### Community 4 - ".Open"
Cohesion: 0.21
Nodes (7): DbConnection, DbDataReader, DbConnection, DbConnection, IReadOnlyList, DeckRepository, CatalogRow

### Community 5 - "AccessTests"
Cohesion: 0.15
Nodes (12): DataReceivedEventArgs, Fact, HttpClient, HttpResponseMessage, int, JsonElement, Process, string (+4 more)

### Community 6 - "CatalogScraper"
Cohesion: 0.10
Nodes (19): Label, Fact, string, ScraperTests, AppPaths, GeneratedRegex, HttpClient, List (+11 more)

### Community 7 - "ExternalWorkerTests"
Cohesion: 0.12
Nodes (16): HttpListener, IDisposable, byte, Fact, HttpClient, HttpResponseMessage, int, JsonElement (+8 more)

### Community 8 - "decks.js"
Cohesion: 0.20
Nodes (24): deckBrowsePager, deckListPager, deckQtyOf(), doDeckSearch(), doLeaderSearch(), guidePill(), hoverPreview(), leaderPager (+16 more)

### Community 9 - "manifest.js"
Cohesion: 0.10
Nodes (34): ACTIVE, asMe(), BASE, browse(), COLORS, contention, contentionSent, deck (+26 more)

### Community 10 - "ISessionRepository"
Cohesion: 0.13
Nodes (9): TimeSpan, ISessionRepository, HashSet, HttpContext, RequestDelegate, string, Task, Auth (+1 more)

### Community 11 - "JobQueue"
Cohesion: 0.15
Nodes (9): DateTime, DbConnection, DbDataReader, IClock, TimeSpan, Backlog, Job, JobQueue (+1 more)

### Community 13 - "CardId"
Cohesion: 0.10
Nodes (15): Dictionary, GeneratedRegex, List, Regex, CardId, AppPaths, IReadOnlyList, string (+7 more)

### Community 14 - "Manifest.Web"
Cohesion: 0.20
Nodes (5): Manifest.Web, string, AccessPages, string, LoginPage

### Community 15 - "AppConfig"
Cohesion: 0.06
Nodes (26): HashSet, int, IPEndPoint, IReadOnlyList, long, string, TimeSpan, AppConfig (+18 more)

### Community 16 - "CatalogRefresh"
Cohesion: 0.17
Nodes (10): AppPaths, JsonSerializerOptions, List, string, Task, CatalogRefresh, TitleParts, VegapullCard (+2 more)

### Community 17 - "ServingTests"
Cohesion: 0.19
Nodes (7): HttpMethod, HttpRequestMessage, Action, Fact, HttpResponseMessage, Task, ServingTests

### Community 18 - "Requests.cs"
Cohesion: 0.07
Nodes (27): JsonConverter, JsonSerializerOptions, Type, Utf8JsonReader, Utf8JsonWriter, PythonStyleDoubleConverter, PythonStyleNullableDoubleConverter, ScanResponse (+19 more)

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
Cohesion: 0.23
Nodes (8): Clock, IEnumerable, string, Task, JobQueueTests, MemberData, Queue, SkippableTheory

### Community 31 - "ServerFixture"
Cohesion: 0.13
Nodes (8): ICollectionFixture, Process, string, StringContent, ServerCollection, ServerFixture, Name, Url

### Community 32 - "Handlers.cs"
Cohesion: 0.18
Nodes (12): CancellationToken, Task, EmailPayload, PurgeAccessTokensHandler, PurgeJobsHandler, PurgeSessionsHandler, RefreshCatalogHandler, RefreshPricesHandler (+4 more)

### Community 33 - "AccessRepository"
Cohesion: 0.05
Nodes (34): Acted, DateTime, DbConnection, DbDataReader, IClock, int, InviteToken, List (+26 more)

### Community 34 - "CardRepository"
Cohesion: 0.17
Nodes (12): IReadOnlyCollection, DbCommand, Dictionary, Key, List, string, CardRepository, Page (+4 more)

### Community 35 - "Manifest Scaling Plan"
Cohesion: 0.14
Nodes (13): Acceptance Criteria, Manifest Scaling Plan, Phase 1: Stabilize Current App, Phase 2: Database Migration to PostgreSQL, Phase 3: Storage and Image Pipeline, Phase 4: Background Jobs, Phase 5: Public Auth, Abuse Protection, and Sessions, Phase 6: API Pagination and Query Scaling (+5 more)

### Community 36 - "DatabaseUrlTests"
Cohesion: 0.10
Nodes (15): Assembly, DbConnection, IReadOnlyList, List, string, PostgresMigrations, AppPaths, Fact (+7 more)

### Community 37 - "RedisLoginThrottle"
Cohesion: 0.07
Nodes (29): ConfigurationOptions, IClock, LuaScript, DateTime, Clock, DateTime, Fact, HttpClient (+21 more)

### Community 38 - "Database"
Cohesion: 0.06
Nodes (28): DbParameter, AppPaths, long, SqliteConnection, string, Database, DateTime, DbCommand (+20 more)

### Community 41 - ".Write"
Cohesion: 0.40
Nodes (3): IEnumerable, StringBuilder, CsvExport

### Community 42 - ".MapAuth"
Cohesion: 0.17
Nodes (7): IQueryCollection, AppConfig, HttpContext, string, Task, WebApplication, Endpoints

### Community 43 - "PrometheusExporter"
Cohesion: 0.16
Nodes (9): KeyValuePair, ConcurrentDictionary, Dictionary, object, Kind, PrometheusExporter, Spec, MeterListener (+1 more)

### Community 44 - "DeckDetail"
Cohesion: 0.15
Nodes (12): List, IDeckRepository, Dictionary, List, BuyListItem, DeckCardRow, DeckDetail, DeckListItem (+4 more)

### Community 45 - "ContentSecurityTests"
Cohesion: 0.32
Nodes (6): Fact, HttpResponseMessage, InlineData, Task, Theory, ContentSecurityTests

### Community 46 - ".Read"
Cohesion: 0.32
Nodes (4): string, Task, ApiScanner, MediaType

### Community 47 - "Task"
Cohesion: 0.23
Nodes (5): Body, HttpClient, JsonElement, Status, Task

### Community 48 - "ImageCache"
Cohesion: 0.05
Nodes (39): Component, Detail, IAmazonS3, IReadOnlyDictionary, byte, CancellationToken, DateTime, DbConnection (+31 more)

### Community 49 - "DeckAnalysis"
Cohesion: 0.43
Nodes (4): Band, HashSet, Band, DeckAnalysis

### Community 50 - "Keyset"
Cohesion: 0.20
Nodes (8): Func, Kind, DbCommand, DbDataReader, int, Key, Keyset, Kind

### Community 51 - "UserRepository"
Cohesion: 0.16
Nodes (8): DateTime, DbConnection, DbDataReader, IClock, List, string, TimeSpan, UserRepository

### Community 52 - "Telemetry"
Cohesion: 0.16
Nodes (10): Backlog, Counter, Histogram, bool, double, long, object, string (+2 more)

### Community 53 - "TlsSetupTests"
Cohesion: 0.20
Nodes (8): IAsyncLifetime, bool, int, Process, SkippableFact, string, Task, TlsSetupTests

### Community 54 - "MetricsTests"
Cohesion: 0.28
Nodes (7): Fact, HttpClient, int, Process, string, Task, MetricsTests

### Community 55 - "MailSettings"
Cohesion: 0.22
Nodes (6): IServiceCollection, MailSettings, AppConfig, AppPaths, ServiceSetup, SecureSocketOptions

### Community 56 - "DeckListParser"
Cohesion: 0.31
Nodes (6): Dictionary, GeneratedRegex, IEnumerable, List, Regex, DeckListParser

### Community 57 - ".Start"
Cohesion: 0.18
Nodes (7): AppConfig, ILogger, IPEndPoint, Task, WebApplication, MetricsServer, Task

### Community 58 - "Manifest"
Cohesion: 0.13
Nodes (7): Manifest, JsonSerializerOptions, Json, string, AccessStatus, HashSet, Net

### Community 59 - "User"
Cohesion: 0.23
Nodes (3): IUserRepository, SessionInfo, User

### Community 60 - "Backups and restore"
Cohesion: 0.18
Nodes (11): Alerts, Backups and restore, Latest result, Load testing, Metrics, Nightly dumps, a weekly test restore, and an off-site copy, Object storage, Restoring (+3 more)

### Community 61 - "PriceRefresh"
Cohesion: 0.29
Nodes (6): Dictionary, JsonSerializerOptions, string, FxResponse, PricedCard, PriceRefresh

### Community 63 - ".UsernamesAreCaseInsensitive"
Cohesion: 0.33
Nodes (4): Fact, StringContent, Task, AccountTests

### Community 64 - "WebAssets"
Cohesion: 0.22
Nodes (8): Asset, Assembly, Dictionary, HttpContext, string, Task, Asset, WebAssets

### Community 65 - "JobWorker"
Cohesion: 0.27
Nodes (8): BackgroundService, CancellationToken, Dictionary, ILogger, int, Task, JobWorker, SemaphoreSlim

### Community 67 - "README.md"
Cohesion: 0.25
Nodes (6): OPTCGManifest, OPTCGManifest, OPTCGManifest, OPTCGManifest, OPTCGManifest, OPTCGManifest

### Community 68 - "Series"
Cohesion: 0.25
Nodes (7): Instrument, bool, double, long, string, Series, Spec

### Community 69 - "collection.js"
Cohesion: 0.33
Nodes (9): doSearch(), flash(), loadOwned(), logPager, ownPager, paintSetBreak(), quickAdd(), refreshSetBreak() (+1 more)

### Community 71 - "scan.js"
Cohesion: 0.31
Nodes (5): awaitScan(), camConstraints, refocus(), say(), send()

### Community 72 - "JobSchedule"
Cohesion: 0.29
Nodes (6): CancellationToken, IClock, ILogger, List, Task, JobSchedule

### Community 73 - "MetricsTests.cs"
Cohesion: 0.22
Nodes (3): InlineData, Theory, PrometheusExporterTests

### Community 74 - "Before the day"
Cohesion: 0.29
Nodes (7): Backups, Before the day, Capacity, Configuration, Monitoring, Security, Server and network

### Community 75 - "WorkerHost.cs"
Cohesion: 0.29
Nodes (3): AppPaths, string, WorkerHost

### Community 77 - ".Depth"
Cohesion: 0.50
Nodes (3): Failed, Queued, Running

### Community 78 - "SystemClock"
Cohesion: 1.00
Nodes (3): DateTime, IClock, SystemClock

### Community 79 - "Mailer"
Cohesion: 0.38
Nodes (4): Task, TimeSpan, Mailer, MailOutcome

### Community 81 - "UserRepository.cs"
Cohesion: 0.33
Nodes (4): Exception, AccountError, RuleViolation, MigrationError

### Community 83 - "Options"
Cohesion: 0.40
Nodes (5): Background jobs and the worker, Card art in object storage, More than one app container: Redis, Options, PostgreSQL instead of SQLite

### Community 84 - "Launch checklist"
Cohesion: 0.50
Nodes (4): Launch checklist, On the day, Rolling back, The first week

## Knowledge Gaps
- **136 isolated node(s):** `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)`, `xunit.runner.visualstudio (2.8.2)`, `Xunit.SkippableFact (1.5.61)` (+131 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **16 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Manifest.Tests` connect `Manifest.Tests` to `Manifest.Data`, `DatabaseUrlTests`, `ExternalWorkerTests`, `MetricsTests.cs`, `CardId`, `TlsSetupTests`, `ServerFixture`, `.UsernamesAreCaseInsensitive`?**
  _High betweenness centrality (0.173) - this node is a cross-community bridge._
- **Why does `Manifest.Data` connect `Manifest.Data` to `Handlers.cs`, `DatabaseUrlTests`, `Database`, `JobQueue`, `WorkerHost.cs`, `Manifest.Web`, `UserRepository.cs`, `Keyset`, `ReaderExtensions`, `Manifest`, `PriceRefresh`, `Manifest.Tests`?**
  _High betweenness centrality (0.128) - this node is a cross-community bridge._
- **Why does `Manifest.Services` connect `Manifest.Data` to `.Create`, `MetricsTests.cs`, `PrometheusExporter`, `WorkerHost.cs`, `CardId`, `Manifest.Web`, `Mailer`, `ImageCache`, `UserRepository.cs`, `Manifest`, `Manifest.Tests`?**
  _High betweenness centrality (0.095) - this node is a cross-community bridge._
- **What connects `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)` to the rest of the system?**
  _136 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Manifest` be split into smaller, more focused modules?**
  _Cohesion score 0.14285714285714285 - nodes in this community are weakly interconnected._
- **Should `cards.js` be split into smaller, more focused modules?**
  _Cohesion score 0.08787878787878788 - nodes in this community are weakly interconnected._
- **Should `.Normalise` be split into smaller, more focused modules?**
  _Cohesion score 0.09462365591397849 - nodes in this community are weakly interconnected._