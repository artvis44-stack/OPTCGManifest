# Graph Report - optcg-manifest  (2026-10-07)

## Corpus Check
- 121 files · ~294,345 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1634 nodes · 3371 edges · 91 communities (78 shown, 13 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 182 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `f2ecdefb`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Manifest
- Manifest.Services
- cards.js
- ICardRepository
- BinderRepository
- AccessTests
- CatalogScraper
- ExternalWorkerTests
- decks.js
- manifest.js
- RepositoryInterfaces.cs
- JobQueue
- make_cert.sh
- .Normalise
- AccessPages.cs
- Responses
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
- IJobHandler
- AccessRepository
- .SearchPage
- Manifest Scaling Plan
- .Scripts
- RedisLoginThrottle
- SqliteToPostgres
- AGENTS.md
- backup-postgres.sh
- CollectionRow
- .MapAuth
- PrometheusExporter
- .Open
- ContentSecurityTests
- Database
- Task
- ImageCache
- BinderTests
- Keyset
- UserRepository
- Telemetry
- TlsSetupTests
- MetricsTests
- binders.js
- DatabaseUrlTests
- .Start
- Manifest
- AppConfig
- Backups and restore
- PriceRefresh
- Manifest.Tests
- .UsernamesAreCaseInsensitive
- WebAssets
- JobWorker
- UserAdmin
- README.md
- CardRepository
- collection.js
- .Create
- scan.js
- JobSchedule
- MetricsTests.cs
- Before the day
- Cards.cs
- Manifest.Data
- .Depth
- .WriteBinder
- MailSettings
- api.js
- BinderScope
- Options
- Launch checklist
- Guard
- verify-backup.sh
- alertmanager.sh
- run.sh
- Net

## God Nodes (most connected - your core abstractions)
1. `Manifest.Data` - 40 edges
2. `AccessRepository` - 37 edges
3. `Database` - 32 edges
4. `UserRepository` - 30 edges
5. `CatalogScraper` - 30 edges
6. `ServerFixture` - 29 edges
7. `Manifest.Services` - 29 edges
8. `CardRepository` - 28 edges
9. `BinderRepository` - 24 edges
10. `JobQueue` - 24 edges

## Surprising Connections (you probably didn't know these)
- `Guard` --references--> `AppConfig`  [EXTRACTED]
  Manifest/Web/Guard.cs → Manifest/AppConfig.cs
- `AccessRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/AccessRepository.cs → Manifest/Data/Database.cs
- `BinderRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/BinderRepository.cs → Manifest/Data/Database.cs
- `CardRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/Database.cs
- `CardRepository` --implements--> `ICardRepository`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/RepositoryInterfaces.cs

## Import Cycles
- None detected.

## Communities (91 total, 13 thin omitted)

### Community 0 - "Manifest"
Cohesion: 0.13
Nodes (15): A note on the network, Binders: sharing a collection, Card art, Checking which version you are running, Deploying to a VPS, Files, Getting your data out, Logging cards (+7 more)

### Community 1 - "Manifest.Services"
Cohesion: 0.13
Nodes (8): Manifest.Services, Manifest.Models, Exception, BinderError, BinderRights, AccountError, RuleViolation, MigrationError

### Community 2 - "cards.js"
Cohesion: 0.09
Nodes (42): addedBy(), cardModalHTML(), cardViewHTML(), CATEGORY_ORDER, collectionActs(), COLOR_CLASS, COLOR_ORDER, esc() (+34 more)

### Community 3 - "ICardRepository"
Cohesion: 0.20
Nodes (3): IReadOnlyList, List, ICardRepository

### Community 4 - "BinderRepository"
Cohesion: 0.13
Nodes (9): From, DbConnection, DbDataReader, int, List, BinderRepository, List, BinderInfo (+1 more)

### Community 5 - "AccessTests"
Cohesion: 0.15
Nodes (12): DataReceivedEventArgs, Fact, HttpClient, HttpResponseMessage, int, JsonElement, Process, string (+4 more)

### Community 6 - "CatalogScraper"
Cohesion: 0.09
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

### Community 10 - "RepositoryInterfaces.cs"
Cohesion: 0.12
Nodes (9): TimeSpan, ISessionRepository, HashSet, HttpContext, RequestDelegate, string, Task, Auth (+1 more)

### Community 11 - "JobQueue"
Cohesion: 0.15
Nodes (9): DateTime, DbConnection, DbDataReader, IClock, TimeSpan, Backlog, Job, JobQueue (+1 more)

### Community 13 - ".Normalise"
Cohesion: 0.08
Nodes (21): Dictionary, GeneratedRegex, List, Regex, CardId, Dictionary, GeneratedRegex, IEnumerable (+13 more)

### Community 14 - "AccessPages.cs"
Cohesion: 0.27
Nodes (4): string, AccessPages, string, LoginPage

### Community 15 - "Responses"
Cohesion: 0.10
Nodes (14): HttpContext, Key, Task, Responses, HttpContext, string, SecurityHeaders, AppConfig (+6 more)

### Community 16 - "CatalogRefresh"
Cohesion: 0.17
Nodes (10): AppPaths, JsonSerializerOptions, List, string, Task, CatalogRefresh, TitleParts, VegapullCard (+2 more)

### Community 17 - "ServingTests"
Cohesion: 0.19
Nodes (7): HttpMethod, HttpRequestMessage, Action, Fact, HttpResponseMessage, Task, ServingTests

### Community 18 - "Requests.cs"
Cohesion: 0.06
Nodes (35): JsonConverter, JsonSerializerOptions, Type, Utf8JsonReader, Utf8JsonWriter, PythonStyleDoubleConverter, PythonStyleNullableDoubleConverter, ScanResponse (+27 more)

### Community 19 - "CatalogAndLookupTests"
Cohesion: 0.28
Nodes (4): Fact, JsonElement, Task, CatalogAndLookupTests

### Community 20 - "DeckTests"
Cohesion: 0.29
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
Cohesion: 0.12
Nodes (9): ICollectionFixture, HttpClient, Process, string, StringContent, ServerCollection, ServerFixture, Name (+1 more)

### Community 32 - "IJobHandler"
Cohesion: 0.18
Nodes (11): CancellationToken, Task, PurgeAccessTokensHandler, PurgeJobsHandler, PurgeSessionsHandler, RefreshCatalogHandler, RefreshPricesHandler, SendEmailHandler (+3 more)

### Community 33 - "AccessRepository"
Cohesion: 0.05
Nodes (33): Acted, DateTime, DbConnection, DbDataReader, IClock, int, InviteToken, List (+25 more)

### Community 34 - ".SearchPage"
Cohesion: 0.22
Nodes (6): DbCommand, List, Page, IReadOnlyList, CardFilter, SearchRow

### Community 35 - "Manifest Scaling Plan"
Cohesion: 0.14
Nodes (13): Acceptance Criteria, Manifest Scaling Plan, Phase 1: Stabilize Current App, Phase 2: Database Migration to PostgreSQL, Phase 3: Storage and Image Pipeline, Phase 4: Background Jobs, Phase 5: Public Auth, Abuse Protection, and Sessions, Phase 6: API Pagination and Query Scaling (+5 more)

### Community 36 - ".Scripts"
Cohesion: 0.16
Nodes (10): Assembly, DbConnection, IReadOnlyList, List, string, PostgresMigrations, SkippableFact, Task (+2 more)

### Community 37 - "RedisLoginThrottle"
Cohesion: 0.06
Nodes (33): ConfigurationOptions, IClock, IServiceCollection, LuaScript, AppConfig, AppPaths, ServiceSetup, DateTime (+25 more)

### Community 38 - "SqliteToPostgres"
Cohesion: 0.12
Nodes (14): AppPaths, Fact, List, SkippableFact, string, MigrateSqliteTests, AppPaths, DateTime (+6 more)

### Community 41 - "CollectionRow"
Cohesion: 0.33
Nodes (4): CollectionRow, IEnumerable, StringBuilder, CsvExport

### Community 42 - ".MapAuth"
Cohesion: 0.19
Nodes (7): IQueryCollection, AppConfig, HttpContext, string, Task, WebApplication, Endpoints

### Community 43 - "PrometheusExporter"
Cohesion: 0.11
Nodes (16): Instrument, KeyValuePair, bool, ConcurrentDictionary, Dictionary, double, long, object (+8 more)

### Community 44 - ".Open"
Cohesion: 0.08
Nodes (25): Band, DbConnection, DbDataReader, IReadOnlyList, DbConnection, DbConnection, IReadOnlyList, List (+17 more)

### Community 45 - "ContentSecurityTests"
Cohesion: 0.32
Nodes (6): Fact, HttpResponseMessage, InlineData, Task, Theory, ContentSecurityTests

### Community 46 - "Database"
Cohesion: 0.06
Nodes (30): Component, DbParameter, IReadOnlyDictionary, AppPaths, long, SqliteConnection, string, Database (+22 more)

### Community 47 - "Task"
Cohesion: 0.36
Nodes (4): Body, JsonElement, Status, Task

### Community 48 - "ImageCache"
Cohesion: 0.07
Nodes (27): Detail, IAmazonS3, byte, CancellationToken, DateTime, DbConnection, HttpClient, IClock (+19 more)

### Community 49 - "BinderTests"
Cohesion: 0.35
Nodes (10): HttpStatusCode, Body, Dictionary, Fact, HttpClient, JsonElement, Status, string (+2 more)

### Community 50 - "Keyset"
Cohesion: 0.20
Nodes (8): Kind, DbCommand, DbDataReader, Func, int, Key, Keyset, Kind

### Community 51 - "UserRepository"
Cohesion: 0.11
Nodes (11): IUserRepository, DateTime, DbConnection, DbDataReader, IClock, List, string, TimeSpan (+3 more)

### Community 52 - "Telemetry"
Cohesion: 0.16
Nodes (10): Backlog, Counter, Histogram, bool, double, long, object, string (+2 more)

### Community 53 - "TlsSetupTests"
Cohesion: 0.22
Nodes (7): bool, int, Process, SkippableFact, string, Task, TlsSetupTests

### Community 54 - "MetricsTests"
Cohesion: 0.29
Nodes (8): IAsyncLifetime, Fact, HttpClient, int, Process, string, Task, MetricsTests

### Community 55 - "binders.js"
Cohesion: 0.20
Nodes (18): applyBinder(), binderManagerHTML(), BINDERS, binderWritable(), currentBinder(), loadBinders(), moveActs(), openBinderManager() (+10 more)

### Community 56 - "DatabaseUrlTests"
Cohesion: 0.22
Nodes (6): IReadOnlyList, AppPaths, Fact, InlineData, Theory, DatabaseUrlTests

### Community 57 - ".Start"
Cohesion: 0.13
Nodes (9): string, Cli, AppConfig, ILogger, IPEndPoint, Task, WebApplication, MetricsServer (+1 more)

### Community 58 - "Manifest"
Cohesion: 0.11
Nodes (12): Manifest, AppPaths, DateTime, IClock, SystemClock, JsonSerializerOptions, Json, string (+4 more)

### Community 59 - "AppConfig"
Cohesion: 0.24
Nodes (7): HashSet, int, IPEndPoint, long, string, TimeSpan, AppConfig

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

### Community 68 - "CardRepository"
Cohesion: 0.25
Nodes (6): IReadOnlyCollection, Dictionary, Key, string, CardRepository, IScanRepository

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

### Community 75 - "Cards.cs"
Cohesion: 0.29
Nodes (6): List, AdjustResult, BulkLogResult, Facets, SetStat, Stats

### Community 76 - "Manifest.Data"
Cohesion: 0.16
Nodes (8): Manifest.Web, Manifest.Data, Manifest.Services.Jobs, EmailPayload, int, string, JobPriority, JobTypes

### Community 77 - ".Depth"
Cohesion: 0.50
Nodes (3): Failed, Queued, Running

### Community 78 - ".WriteBinder"
Cohesion: 0.38
Nodes (5): Func, HttpContext, Task, WebApplication, BinderEndpoints

### Community 79 - "MailSettings"
Cohesion: 0.23
Nodes (6): Task, TimeSpan, Mailer, MailOutcome, MailSettings, SecureSocketOptions

### Community 80 - "api.js"
Cohesion: 0.50
Nodes (3): api(), cardIndex, scoped()

### Community 81 - "BinderScope"
Cohesion: 0.39
Nodes (3): DbCommand, BinderScope, CardDetailRow

### Community 83 - "Options"
Cohesion: 0.40
Nodes (5): Background jobs and the worker, Card art in object storage, More than one app container: Redis, Options, PostgreSQL instead of SQLite

### Community 84 - "Launch checklist"
Cohesion: 0.50
Nodes (4): Launch checklist, On the day, Rolling back, The first week

### Community 85 - "Guard"
Cohesion: 0.48
Nodes (4): HttpContext, RequestDelegate, Task, Guard

## Knowledge Gaps
- **142 isolated node(s):** `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)`, `xunit.runner.visualstudio (2.8.2)`, `Xunit.SkippableFact (1.5.61)` (+137 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **13 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Manifest.Tests` connect `Manifest.Tests` to `AccessTests`, `ExternalWorkerTests`, `MetricsTests.cs`, `Manifest.Data`, `.Normalise`, `CollectionTests`, `TlsSetupTests`, `ServerFixture`, `.UsernamesAreCaseInsensitive`?**
  _High betweenness centrality (0.184) - this node is a cross-community bridge._
- **Why does `Manifest.Data` connect `Manifest.Data` to `Manifest.Services`, `.SearchPage`, `.Scripts`, `SqliteToPostgres`, `RepositoryInterfaces.cs`, `JobQueue`, `Database`, `ReaderExtensions`, `Manifest`, `PriceRefresh`, `Manifest.Tests`?**
  _High betweenness centrality (0.128) - this node is a cross-community bridge._
- **Why does `Manifest.Web` connect `Manifest.Data` to `WebAssets`, `RedisLoginThrottle`, `RepositoryInterfaces.cs`, `AccessPages.cs`, `Responses`, `Requests.cs`, `Manifest`?**
  _High betweenness centrality (0.084) - this node is a cross-community bridge._
- **What connects `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)` to the rest of the system?**
  _142 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Manifest` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._
- **Should `Manifest.Services` be split into smaller, more focused modules?**
  _Cohesion score 0.12987012987012986 - nodes in this community are weakly interconnected._
- **Should `cards.js` be split into smaller, more focused modules?**
  _Cohesion score 0.08603145235892692 - nodes in this community are weakly interconnected._