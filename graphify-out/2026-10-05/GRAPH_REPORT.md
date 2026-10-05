# Graph Report - optcg-manifest  (2026-10-04)

## Corpus Check
- 100 files · ~263,703 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1339 nodes · 2689 edges · 83 communities (71 shown, 12 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 137 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `35eb041c`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Manifest
- Manifest.Services
- cards.js
- Cards.cs
- DeckRepository
- AccessTests
- CatalogScraper
- ExternalWorkerTests
- decks.js
- UserRepository
- ISessionRepository
- JobQueue
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
- .Ready
- PaginationTests
- JobQueueTests
- SqliteToPostgres
- IJobHandler
- .Open
- .SearchPage
- Manifest Scaling Plan
- .EnsureSchema
- RedisLoginThrottle
- SqlDialect
- AGENTS.md
- .Apply
- .Write
- .EverythingArrivesAndStillWorks
- Manifest.Web
- DeckDetail
- ContentSecurityTests
- .Read
- Database
- ImageCache
- DeckAnalysis
- Keyset
- User
- MailSettings
- TlsSetupTests
- .MapAuth
- Manifest.Data
- .CreateTestDatabase
- .Create
- Manifest
- ServerFixture.cs
- DatabaseUrlTests
- PriceRefresh
- Manifest.Tests
- .UsernamesAreCaseInsensitive
- WebAssets
- JobWorker
- CardRepository
- ICardRepository
- UserAdmin
- collection.js
- ScanService.cs
- scan.js
- JobSchedule
- .Run
- Guard
- Net
- JobSchedule.cs
- .Depth
- SystemClock
- SetupPage
- api.js
- .Body

## God Nodes (most connected - your core abstractions)
1. `AccessRepository` - 37 edges
2. `Manifest.Data` - 34 edges
3. `UserRepository` - 30 edges
4. `CatalogScraper` - 30 edges
5. `ServerFixture` - 29 edges
6. `Database` - 29 edges
7. `CardRepository` - 28 edges
8. `Manifest.Services` - 23 edges
9. `ServingTests` - 21 edges
10. `Manifest.Models` - 21 edges

## Surprising Connections (you probably didn't know these)
- `Guard` --references--> `AppConfig`  [EXTRACTED]
  Manifest/Web/Guard.cs → Manifest/AppConfig.cs
- `AccessRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/AccessRepository.cs → Manifest/Data/Database.cs
- `CardRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/Database.cs
- `CardRepository` --implements--> `ICardRepository`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/RepositoryInterfaces.cs
- `CardRepository` --implements--> `IScanRepository`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/RepositoryInterfaces.cs

## Import Cycles
- None detected.

## Communities (83 total, 12 thin omitted)

### Community 0 - "Manifest"
Cohesion: 0.08
Nodes (24): A note on the network, Background jobs and the worker, Card art, Card art in object storage, Checking which version you are running, Files, Getting your data out, Logging cards (+16 more)

### Community 1 - "Manifest.Services"
Cohesion: 0.15
Nodes (7): Manifest.Services, Manifest.Models, Exception, IScanRepository, AccountError, RuleViolation, MigrationError

### Community 2 - "cards.js"
Cohesion: 0.09
Nodes (40): cardModalHTML(), cardViewHTML(), CATEGORY_ORDER, collectionActs(), COLOR_CLASS, COLOR_ORDER, esc(), fillEffect() (+32 more)

### Community 3 - "Cards.cs"
Cohesion: 0.16
Nodes (9): IReadOnlyList, IReadOnlyList, List, AdjustResult, BulkLogResult, CardQuantity, Facets, SetStat (+1 more)

### Community 4 - "DeckRepository"
Cohesion: 0.19
Nodes (7): DbConnection, DbDataReader, DbConnection, IReadOnlyList, List, DeckRepository, CatalogRow

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

### Community 9 - "UserRepository"
Cohesion: 0.20
Nodes (6): DateTime, DbConnection, IClock, string, TimeSpan, UserRepository

### Community 10 - "ISessionRepository"
Cohesion: 0.13
Nodes (9): TimeSpan, ISessionRepository, HashSet, HttpContext, RequestDelegate, string, Task, Auth (+1 more)

### Community 11 - "JobQueue"
Cohesion: 0.16
Nodes (8): DateTime, DbConnection, DbDataReader, IClock, TimeSpan, Job, JobQueue, List

### Community 13 - ".Normalise"
Cohesion: 0.07
Nodes (21): Dictionary, GeneratedRegex, List, Regex, CardId, Dictionary, GeneratedRegex, IEnumerable (+13 more)

### Community 14 - "ServerFixture"
Cohesion: 0.20
Nodes (8): Body, HttpClient, JsonElement, Process, Status, string, Task, ServerFixture

### Community 15 - "AppConfig"
Cohesion: 0.22
Nodes (7): HashSet, int, IReadOnlyList, long, string, TimeSpan, AppConfig

### Community 16 - "CatalogRefresh"
Cohesion: 0.17
Nodes (10): AppPaths, JsonSerializerOptions, List, string, Task, CatalogRefresh, TitleParts, VegapullCard (+2 more)

### Community 17 - "ServingTests"
Cohesion: 0.19
Nodes (7): HttpMethod, HttpRequestMessage, Action, Fact, HttpResponseMessage, Task, ServingTests

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
Cohesion: 0.12
Nodes (14): net8.0, net8.0, Anthropic (12.40.0), AWSSDK.S3 (3.7.410.4), MailKit (4.17.0), Microsoft.Data.Sqlite (8.0.11), Microsoft.NET.Test.Sdk (17.11.1), Npgsql (8.0.6) (+6 more)

### Community 23 - "Responses"
Cohesion: 0.38
Nodes (5): HttpContext, Key, Task, Responses, Value

### Community 24 - "ReaderExtensions"
Cohesion: 0.29
Nodes (3): DbDataReader, string, ReaderExtensions

### Community 25 - "Manifest — server"
Cohesion: 0.33
Nodes (5): Layout, Manifest — server, Notes, Running it, Tests

### Community 27 - ".Ready"
Cohesion: 0.22
Nodes (12): Component, IReadOnlyDictionary, AppConfig, Body, CancellationToken, IConnectionMultiplexer, Status, Task (+4 more)

### Community 28 - "PaginationTests"
Cohesion: 0.27
Nodes (9): Action, Fact, IEnumerable, InlineData, JsonElement, List, Task, Theory (+1 more)

### Community 29 - "JobQueueTests"
Cohesion: 0.25
Nodes (8): Clock, IEnumerable, string, Task, JobQueueTests, MemberData, Queue, SkippableTheory

### Community 31 - "SqliteToPostgres"
Cohesion: 0.20
Nodes (9): Fact, AppPaths, DateTime, DbConnection, Dictionary, HashSet, SqliteConnection, string (+1 more)

### Community 32 - "IJobHandler"
Cohesion: 0.21
Nodes (9): CancellationToken, Task, PurgeAccessTokensHandler, PurgeJobsHandler, PurgeSessionsHandler, RefreshCatalogHandler, RefreshPricesHandler, SendEmailHandler (+1 more)

### Community 33 - ".Open"
Cohesion: 0.05
Nodes (34): Acted, DateTime, DbConnection, DbDataReader, IClock, int, InviteToken, List (+26 more)

### Community 34 - ".SearchPage"
Cohesion: 0.23
Nodes (7): DbCommand, List, Page, IReadOnlyList, CardFilter, CollectionRow, SearchRow

### Community 35 - "Manifest Scaling Plan"
Cohesion: 0.14
Nodes (13): Acceptance Criteria, Manifest Scaling Plan, Phase 1: Stabilize Current App, Phase 2: Database Migration to PostgreSQL, Phase 3: Storage and Image Pipeline, Phase 4: Background Jobs, Phase 5: Public Auth, Abuse Protection, and Sessions, Phase 6: API Pagination and Query Scaling (+5 more)

### Community 36 - ".EnsureSchema"
Cohesion: 0.15
Nodes (10): Assembly, DbConnection, IReadOnlyList, List, string, PostgresMigrations, SkippableFact, Task (+2 more)

### Community 37 - "RedisLoginThrottle"
Cohesion: 0.06
Nodes (32): ConcurrentDictionary, ConfigurationOptions, IClock, IServiceCollection, LuaScript, AppConfig, AppPaths, DateTime (+24 more)

### Community 38 - "SqlDialect"
Cohesion: 0.14
Nodes (7): DbParameter, DateTime, DbCommand, DbConnection, Exception, CommandExtensions, SqlDialect

### Community 40 - ".Apply"
Cohesion: 0.20
Nodes (7): HttpContext, string, SecurityHeaders, AppConfig, AppPaths, WebApplication, SetupHelper

### Community 41 - ".Write"
Cohesion: 0.33
Nodes (3): IEnumerable, StringBuilder, CsvExport

### Community 42 - ".EverythingArrivesAndStillWorks"
Cohesion: 0.38
Nodes (5): AppPaths, List, SkippableFact, string, MigrateSqliteTests

### Community 43 - "Manifest.Web"
Cohesion: 0.17
Nodes (5): Manifest.Web, string, AccessPages, string, LoginPage

### Community 44 - "DeckDetail"
Cohesion: 0.18
Nodes (11): IDeckRepository, Dictionary, List, BuyListItem, DeckCardRow, DeckDetail, DeckListItem, Guideline (+3 more)

### Community 45 - "ContentSecurityTests"
Cohesion: 0.32
Nodes (6): Fact, HttpResponseMessage, InlineData, Task, Theory, ContentSecurityTests

### Community 46 - ".Read"
Cohesion: 0.28
Nodes (4): string, Task, ApiScanner, MediaType

### Community 47 - "Database"
Cohesion: 0.31
Nodes (6): AppPaths, long, SqliteConnection, string, Database, NpgsqlDataSource

### Community 48 - "ImageCache"
Cohesion: 0.08
Nodes (25): IAmazonS3, byte, CancellationToken, DateTime, DbConnection, HttpClient, IClock, Task (+17 more)

### Community 49 - "DeckAnalysis"
Cohesion: 0.43
Nodes (4): Band, HashSet, Band, DeckAnalysis

### Community 50 - "Keyset"
Cohesion: 0.20
Nodes (8): Func, Kind, DbCommand, DbDataReader, int, Key, Keyset, Kind

### Community 51 - "User"
Cohesion: 0.15
Nodes (5): IUserRepository, DbDataReader, List, SessionInfo, User

### Community 52 - "MailSettings"
Cohesion: 0.23
Nodes (6): Task, TimeSpan, Mailer, MailOutcome, MailSettings, SecureSocketOptions

### Community 53 - "TlsSetupTests"
Cohesion: 0.20
Nodes (8): bool, IAsyncLifetime, int, Process, SkippableFact, string, Task, TlsSetupTests

### Community 54 - ".MapAuth"
Cohesion: 0.20
Nodes (7): IQueryCollection, AppConfig, HttpContext, string, Task, WebApplication, Endpoints

### Community 55 - "Manifest.Data"
Cohesion: 0.21
Nodes (5): Manifest.Tools, Manifest.Data, Manifest.Services.Jobs, EmailPayload, ServiceSetup

### Community 58 - "Manifest"
Cohesion: 0.12
Nodes (9): Manifest, AppPaths, JsonSerializerOptions, Json, string, AccessRequestView, AccessStatus, string (+1 more)

### Community 60 - "DatabaseUrlTests"
Cohesion: 0.20
Nodes (6): AppPaths, Fact, InlineData, Theory, DatabaseUrlTests, NpgsqlConnectionStringBuilder

### Community 61 - "PriceRefresh"
Cohesion: 0.22
Nodes (8): AppPaths, Dictionary, JsonSerializerOptions, string, Task, FxResponse, PricedCard, PriceRefresh

### Community 63 - ".UsernamesAreCaseInsensitive"
Cohesion: 0.33
Nodes (4): Fact, StringContent, Task, AccountTests

### Community 64 - "WebAssets"
Cohesion: 0.24
Nodes (8): Asset, Assembly, Dictionary, HttpContext, string, Task, Asset, WebAssets

### Community 65 - "JobWorker"
Cohesion: 0.27
Nodes (8): BackgroundService, CancellationToken, Dictionary, ILogger, int, Task, JobWorker, SemaphoreSlim

### Community 66 - "CardRepository"
Cohesion: 0.27
Nodes (5): IReadOnlyCollection, Dictionary, Key, string, CardRepository

### Community 67 - "ICardRepository"
Cohesion: 0.20
Nodes (3): List, ICardRepository, CardDetailRow

### Community 69 - "collection.js"
Cohesion: 0.33
Nodes (9): doSearch(), flash(), loadOwned(), logPager, ownPager, paintSetBreak(), quickAdd(), refreshSetBreak() (+1 more)

### Community 70 - "ScanService.cs"
Cohesion: 0.28
Nodes (5): ScanResponse, VisionReading, CancellationToken, Task, ScanService

### Community 71 - "scan.js"
Cohesion: 0.31
Nodes (5): awaitScan(), camConstraints, refocus(), say(), send()

### Community 72 - "JobSchedule"
Cohesion: 0.29
Nodes (6): CancellationToken, IClock, ILogger, List, Task, JobSchedule

### Community 73 - ".Run"
Cohesion: 0.29
Nodes (3): string, Cli, Task

### Community 74 - "Guard"
Cohesion: 0.48
Nodes (4): HttpContext, RequestDelegate, Task, Guard

### Community 76 - "JobSchedule.cs"
Cohesion: 0.40
Nodes (4): int, string, JobPriority, JobTypes

### Community 77 - ".Depth"
Cohesion: 0.50
Nodes (3): Failed, Queued, Running

### Community 78 - "SystemClock"
Cohesion: 1.00
Nodes (3): DateTime, IClock, SystemClock

## Knowledge Gaps
- **93 isolated node(s):** `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)`, `xunit.runner.visualstudio (2.8.2)`, `Xunit.SkippableFact (1.5.61)` (+88 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **12 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Manifest.Tests` connect `Manifest.Tests` to `AccessTests`, `ExternalWorkerTests`, `Manifest.Web`, `.Normalise`, `TlsSetupTests`, `Manifest.Data`, `ServerFixture.cs`, `DatabaseUrlTests`, `.UsernamesAreCaseInsensitive`?**
  _High betweenness centrality (0.244) - this node is a cross-community bridge._
- **Why does `Manifest.Data` connect `Manifest.Data` to `IJobHandler`, `Manifest.Services`, `.SearchPage`, `.Open`, `.EnsureSchema`, `SqlDialect`, `ScanService.cs`, `ISessionRepository`, `JobQueue`, `JobSchedule.cs`, `.Normalise`, `.Read`, `Manifest.Web`, `ImageCache`, `ReaderExtensions`, `Manifest`, `DatabaseUrlTests`, `Manifest.Tests`?**
  _High betweenness centrality (0.158) - this node is a cross-community bridge._
- **Why does `Manifest.Web` connect `Manifest.Web` to `RedisLoginThrottle`, `ScanService.cs`, `.Apply`, `ISessionRepository`, `SetupPage`, `Requests.cs`, `Manifest.Data`, `Manifest`?**
  _High betweenness centrality (0.103) - this node is a cross-community bridge._
- **What connects `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)` to the rest of the system?**
  _93 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Manifest` be split into smaller, more focused modules?**
  _Cohesion score 0.08 - nodes in this community are weakly interconnected._
- **Should `Manifest.Services` be split into smaller, more focused modules?**
  _Cohesion score 0.14705882352941177 - nodes in this community are weakly interconnected._
- **Should `cards.js` be split into smaller, more focused modules?**
  _Cohesion score 0.08787878787878788 - nodes in this community are weakly interconnected._