# Graph Report - optcg-manifest  (2026-10-07)

## Corpus Check
- 127 files · ~286,041 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1769 nodes · 3695 edges · 98 communities (79 shown, 19 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 197 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `98a234d5`
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
- .Open
- make_cert.sh
- CardId
- AccessPages.cs
- AppConfig
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
- SqliteToPostgres
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
- UserRepository
- Telemetry
- .FromScrape
- AdminEndpoints
- binders.js
- Database
- .Start
- Manifest
- .Initialise
- Backups and restore
- .Match
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
- .EverythingArrivesAndStillWorks
- MetricsTests.cs
- Before the day
- .Normalise
- Manifest.Data
- AdminDataTests
- .WriteBinder
- MailSettings
- api.js
- .Build
- Options
- Launch checklist
- Series
- verify-backup.sh
- alertmanager.sh
- run.sh
- Net
- .Read
- BinderRepository.cs
- AccessMail
- Cli
- SystemClock
- Access.cs
- NoOutboundJobs.cs
- LoginPage.cs

## God Nodes (most connected - your core abstractions)
1. `Manifest.Data` - 41 edges
2. `CatalogScraper` - 40 edges
3. `AccessRepository` - 37 edges
4. `Database` - 33 edges
5. `Manifest.Services` - 32 edges
6. `CardRepository` - 30 edges
7. `UserRepository` - 30 edges
8. `ServerFixture` - 29 edges
9. `JobQueue` - 27 edges
10. `Manifest.Models` - 26 edges

## Surprising Connections (you probably didn't know these)
- `JapanesePrintsTests` --references--> `CatalogRow`  [EXTRACTED]
  Manifest.Tests/JapanesePrintsTests.cs → Manifest/Models/Cards.cs
- `AccessRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/AccessRepository.cs → Manifest/Data/Database.cs
- `AccessRepository` --implements--> `IAccessRepository`  [EXTRACTED]
  Manifest/Data/AccessRepository.cs → Manifest/Data/RepositoryInterfaces.cs
- `BinderRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/BinderRepository.cs → Manifest/Data/Database.cs
- `CardRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/Database.cs

## Import Cycles
- None detected.

## Communities (98 total, 19 thin omitted)

### Community 0 - "Manifest"
Cohesion: 0.13
Nodes (15): A note on the network, Binders: sharing a collection, Card art, Checking which version you are running, Deploying to a VPS, Files, Getting your data out, Keeping cards and prices current (+7 more)

### Community 2 - "cards.js"
Cohesion: 0.08
Nodes (47): addedBy(), cardModalHTML(), cardViewHTML(), CATEGORY_ORDER, collectionActs(), COLOR_CLASS, COLOR_ORDER, esc() (+39 more)

### Community 3 - "BinderScope"
Cohesion: 0.12
Nodes (9): DbCommand, BinderScope, From, List, To, ICardRepository, CardDetailRow, CollectionRow (+1 more)

### Community 4 - "BinderRepository"
Cohesion: 0.16
Nodes (9): DbConnection, DbDataReader, From, int, List, To, BinderRepository, List (+1 more)

### Community 5 - "AccessTests"
Cohesion: 0.15
Nodes (12): DataReceivedEventArgs, Fact, HttpClient, HttpResponseMessage, int, JsonElement, Process, string (+4 more)

### Community 6 - "CatalogScraper"
Cohesion: 0.07
Nodes (26): IReadOnlySet, Label, Fact, string, ScraperTests, AppPaths, GeneratedRegex, HttpClient (+18 more)

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

### Community 11 - ".Open"
Cohesion: 0.11
Nodes (12): Failed, DbConnection, DateTime, DbConnection, DbDataReader, IClock, TimeSpan, Backlog (+4 more)

### Community 13 - "CardId"
Cohesion: 0.10
Nodes (15): Dictionary, GeneratedRegex, List, Regex, CardId, AppPaths, IReadOnlyList, string (+7 more)

### Community 15 - "AppConfig"
Cohesion: 0.06
Nodes (25): HashSet, int, IPEndPoint, IReadOnlyList, long, string, TimeSpan, AppConfig (+17 more)

### Community 16 - ".MapPublic"
Cohesion: 0.17
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
Cohesion: 0.11
Nodes (20): Clock, IClock, CancellationToken, IClock, ILogger, List, Task, JobSchedule (+12 more)

### Community 31 - "ServerFixture"
Cohesion: 0.06
Nodes (28): IAsyncLifetime, ICollectionFixture, Fact, HttpClient, int, Process, string, Task (+20 more)

### Community 32 - "Job"
Cohesion: 0.08
Nodes (30): BackgroundService, Job, CancellationToken, Task, EmailPayload, PurgeAccessTokensHandler, PurgeJobsHandler, PurgeSessionsHandler (+22 more)

### Community 33 - "AccessRepository"
Cohesion: 0.12
Nodes (13): DateTime, DbConnection, DbDataReader, IClock, int, InviteToken, List, Request (+5 more)

### Community 34 - "CardRepository"
Cohesion: 0.19
Nodes (10): DbCommand, Dictionary, IReadOnlyCollection, Key, List, string, CardRepository, IReadOnlyList (+2 more)

### Community 35 - "Manifest Scaling Plan"
Cohesion: 0.14
Nodes (13): Acceptance Criteria, Manifest Scaling Plan, Phase 1: Stabilize Current App, Phase 2: Database Migration to PostgreSQL, Phase 3: Storage and Image Pipeline, Phase 4: Background Jobs, Phase 5: Public Auth, Abuse Protection, and Sessions, Phase 6: API Pagination and Query Scaling (+5 more)

### Community 36 - ".EnsureSchema"
Cohesion: 0.15
Nodes (10): Assembly, DbConnection, IReadOnlyList, List, string, PostgresMigrations, SkippableFact, Task (+2 more)

### Community 37 - "RedisLoginThrottle"
Cohesion: 0.07
Nodes (28): ConfigurationOptions, IServiceCollection, LuaScript, AppConfig, AppPaths, ServiceSetup, Fact, HttpClient (+20 more)

### Community 38 - "SqliteToPostgres"
Cohesion: 0.20
Nodes (9): Fact, AppPaths, DateTime, DbConnection, Dictionary, HashSet, SqliteConnection, string (+1 more)

### Community 41 - "AccessRequest"
Cohesion: 0.15
Nodes (5): InviteToken, Request, IAccessRepository, DateTime, AccessRequest

### Community 42 - ".MapAuth"
Cohesion: 0.09
Nodes (16): IQueryCollection, IEnumerable, StringBuilder, CsvExport, Dictionary, GeneratedRegex, IEnumerable, List (+8 more)

### Community 43 - "PrometheusExporter"
Cohesion: 0.16
Nodes (9): KeyValuePair, ConcurrentDictionary, Dictionary, object, Kind, PrometheusExporter, Spec, MeterListener (+1 more)

### Community 44 - ".CatalogRowById"
Cohesion: 0.16
Nodes (10): Band, DbConnection, DbDataReader, DbConnection, IReadOnlyList, DeckRepository, CatalogRow, HashSet (+2 more)

### Community 45 - "ContentSecurityTests"
Cohesion: 0.29
Nodes (6): Fact, HttpResponseMessage, InlineData, Task, Theory, ContentSecurityTests

### Community 46 - "SqlDialect"
Cohesion: 0.15
Nodes (7): DbParameter, DateTime, DbCommand, DbConnection, Exception, CommandExtensions, SqlDialect

### Community 47 - "DeckDetail"
Cohesion: 0.20
Nodes (10): IDeckRepository, Dictionary, List, BuyListItem, DeckCardRow, DeckDetail, Guideline, Guidelines (+2 more)

### Community 48 - "ImageCache"
Cohesion: 0.05
Nodes (39): Component, Detail, IAmazonS3, IReadOnlyDictionary, byte, CancellationToken, DateTime, DbConnection (+31 more)

### Community 49 - "BinderTests"
Cohesion: 0.35
Nodes (10): HttpStatusCode, Body, Dictionary, Fact, HttpClient, JsonElement, Status, string (+2 more)

### Community 50 - "Keyset"
Cohesion: 0.15
Nodes (11): Kind, List, DbCommand, DbDataReader, Func, int, Key, Keyset (+3 more)

### Community 51 - "UserRepository"
Cohesion: 0.15
Nodes (8): DateTime, DbConnection, DbDataReader, IClock, List, string, TimeSpan, UserRepository

### Community 52 - "Telemetry"
Cohesion: 0.16
Nodes (10): Backlog, Counter, Histogram, bool, double, long, object, string (+2 more)

### Community 53 - ".FromScrape"
Cohesion: 0.18
Nodes (9): Dictionary, IReadOnlyList, List, string, JapanesePrints, Fact, List, string (+1 more)

### Community 54 - "AdminEndpoints"
Cohesion: 0.17
Nodes (10): Dictionary, HttpContext, Task, TimeSpan, WebApplication, AdminEndpoints, Priced, PricedAt (+2 more)

### Community 55 - "binders.js"
Cohesion: 0.20
Nodes (18): applyBinder(), binderManagerHTML(), BINDERS, binderWritable(), currentBinder(), loadBinders(), moveActs(), openBinderManager() (+10 more)

### Community 56 - "Database"
Cohesion: 0.23
Nodes (7): AppPaths, long, SqliteConnection, string, Database, NpgsqlConnectionStringBuilder, NpgsqlDataSource

### Community 57 - ".Start"
Cohesion: 0.18
Nodes (7): AppConfig, ILogger, IPEndPoint, Task, WebApplication, MetricsServer, Task

### Community 58 - "Manifest"
Cohesion: 0.15
Nodes (6): Manifest, AppPaths, JsonSerializerOptions, Json, string, WorkerHost

### Community 59 - ".Initialise"
Cohesion: 0.33
Nodes (4): AppPaths, Fact, string, CatalogSeedTests

### Community 60 - "Backups and restore"
Cohesion: 0.18
Nodes (11): Alerts, Backups and restore, Latest result, Load testing, Metrics, Nightly dumps, a weekly test restore, and an off-site copy, Object storage, Restoring (+3 more)

### Community 61 - ".Match"
Cohesion: 0.10
Nodes (20): ApiCard, CatalogPrint, Fact, InlineData, Theory, PriceMatchTests, AppPaths, Dictionary (+12 more)

### Community 63 - ".UsernamesAreCaseInsensitive"
Cohesion: 0.33
Nodes (4): Fact, StringContent, Task, AccountTests

### Community 64 - "WebAssets"
Cohesion: 0.22
Nodes (8): Asset, Assembly, Dictionary, HttpContext, string, Task, Asset, WebAssets

### Community 65 - "User"
Cohesion: 0.23
Nodes (3): IUserRepository, SessionInfo, User

### Community 67 - "README.md"
Cohesion: 0.25
Nodes (6): OPTCGManifest, OPTCGManifest, OPTCGManifest, OPTCGManifest, OPTCGManifest, OPTCGManifest

### Community 69 - "collection.js"
Cohesion: 0.33
Nodes (9): doSearch(), flash(), loadOwned(), logPager, ownPager, paintSetBreak(), quickAdd(), refreshSetBreak() (+1 more)

### Community 71 - "scan.js"
Cohesion: 0.29
Nodes (6): awaitScan(), camConstraints, refocus(), say(), send(), showScanPrints()

### Community 72 - ".EverythingArrivesAndStillWorks"
Cohesion: 0.39
Nodes (5): AppPaths, List, SkippableFact, string, MigrateSqliteTests

### Community 73 - "MetricsTests.cs"
Cohesion: 0.22
Nodes (3): InlineData, Theory, PrometheusExporterTests

### Community 74 - "Before the day"
Cohesion: 0.29
Nodes (7): Backups, Before the day, Capacity, Configuration, Monitoring, Security, Server and network

### Community 75 - ".Normalise"
Cohesion: 0.13
Nodes (10): From, IReadOnlyList, To, IReadOnlyList, List, AdjustResult, BulkLogResult, CardQuantity (+2 more)

### Community 76 - "Manifest.Data"
Cohesion: 0.16
Nodes (7): Manifest.Web, Manifest.Data, Manifest.Services.Jobs, int, string, JobPriority, JobTypes

### Community 77 - "AdminDataTests"
Cohesion: 0.38
Nodes (4): Fact, StringContent, Task, AdminDataTests

### Community 78 - ".WriteBinder"
Cohesion: 0.38
Nodes (5): Func, HttpContext, Task, WebApplication, BinderEndpoints

### Community 79 - "MailSettings"
Cohesion: 0.23
Nodes (6): Task, TimeSpan, Mailer, MailOutcome, MailSettings, SecureSocketOptions

### Community 80 - "api.js"
Cohesion: 0.50
Nodes (3): api(), cardIndex, scoped()

### Community 81 - ".Build"
Cohesion: 0.20
Nodes (6): AppConfig, AppPaths, WebApplication, SetupHelper, string, SetupPage

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

### Community 91 - "BinderRepository.cs"
Cohesion: 0.29
Nodes (6): Exception, BinderError, BinderRights, AccountError, RuleViolation, MigrationError

### Community 92 - "AccessMail"
Cohesion: 0.52
Nodes (3): AccessMail, Message, Message

### Community 94 - "SystemClock"
Cohesion: 1.00
Nodes (3): DateTime, IClock, SystemClock

### Community 95 - "Access.cs"
Cohesion: 0.50
Nodes (3): string, AccessRequestView, AccessStatus

## Knowledge Gaps
- **148 isolated node(s):** `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)`, `xunit.runner.visualstudio (2.8.2)`, `Xunit.SkippableFact (1.5.61)` (+143 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **19 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Manifest.Tests` connect `Manifest.Tests` to `NoOutboundJobs.cs`, `Manifest.Services`, `ExternalWorkerTests`, `MetricsTests.cs`, `Manifest.Data`, `CardId`, `ContentSecurityTests`, `AdminDataTests`, `ServerFixture`, `.UsernamesAreCaseInsensitive`?**
  _High betweenness centrality (0.232) - this node is a cross-community bridge._
- **Why does `Manifest.Data` connect `Manifest.Data` to `Job`, `Manifest.Services`, `UserAdmin`, `.EnsureSchema`, `RepositoryInterfaces.cs`, `SqlDialect`, `Keyset`, `ReaderExtensions`, `Manifest`, `BinderRepository.cs`, `Manifest.Tests`?**
  _High betweenness centrality (0.121) - this node is a cross-community bridge._
- **Why does `Manifest.Web` connect `Manifest.Data` to `WebAssets`, `LoginPage.cs`, `RedisLoginThrottle`, `AccessPages.cs`, `AppConfig`, `.Build`, `Requests.cs`, `Manifest`?**
  _High betweenness centrality (0.081) - this node is a cross-community bridge._
- **What connects `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)` to the rest of the system?**
  _148 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Manifest` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._
- **Should `cards.js` be split into smaller, more focused modules?**
  _Cohesion score 0.08055152394775036 - nodes in this community are weakly interconnected._
- **Should `BinderScope` be split into smaller, more focused modules?**
  _Cohesion score 0.11904761904761904 - nodes in this community are weakly interconnected._