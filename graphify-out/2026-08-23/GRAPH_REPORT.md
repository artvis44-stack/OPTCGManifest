# Graph Report - optcg-manifest  (2026-08-22)

## Corpus Check
- 60 files · ~24,887,226 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 782 nodes · 1561 edges · 38 communities (34 shown, 4 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 94 edges (avg confidence: 0.81)
- Token cost: 0 input · 0 output

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
- HttpContext
- send (scan submit)
- make_cert.sh
- CardId
- ServerFixture
- AppConfig
- CatalogRefresh
- ServingTests
- JsonSerializerOptions
- CatalogAndLookupTests
- DeckTests
- CollectionTests
- Manifest.Tests.csproj
- Responses
- ReaderExtensions
- Manifest — server
- CLAUDE.md
- Database
- setDeckCardQty
- wire (row +/- handlers)
- Requests.cs
- api (fetch wrapper)
- Manifest
- pickLeader
- LenientIntConverter
- Guard
- Net

## God Nodes (most connected - your core abstractions)
1. `AccessRepository` - 35 edges
2. `CatalogScraper` - 30 edges
3. `UserRepository` - 28 edges
4. `ServerFixture` - 22 edges
5. `CatalogAndLookupTests` - 19 edges
6. `ServingTests` - 19 edges
7. `AccessTests` - 18 edges
8. `api (fetch wrapper)` - 18 edges
9. `CardRepository` - 17 edges
10. `Manifest.Models` - 17 edges

## Surprising Connections (you probably didn't know these)
- `Guard` --references--> `AppConfig`  [EXTRACTED]
  Manifest/Web/Guard.cs → Manifest/AppConfig.cs
- `AccessRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/AccessRepository.cs → Manifest/Data/Database.cs
- `CardRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/Database.cs
- `DeckRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/DeckRepository.cs → Manifest/Data/Database.cs
- `UserRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/UserRepository.cs → Manifest/Data/Database.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Client-side OCR capture and preprocessing pipeline** — ui_shrink, ui_cornervariants, ui_send, ui_concept_object_fit_cover_crop, ui_concept_min_vs_chroma_channel, ui_concept_plane_fit_flatfield, ui_concept_multi_region_multi_config_ocr [EXTRACTED 1.00]
- **Deck editor re-render pipeline** — ui_renderdeckeditor, ui_renderdeckhead, ui_renderdecklegal, ui_renderdeckguide, ui_renderdeckcards, ui_renderbuylist, ui_setdeckcardqty [EXTRACTED 1.00]
- **Abort-latest-wins async fetch family** — ui_dosearch, ui_doleadersearch, ui_dodecksearch, ui_loadpreview, ui_concept_abort_latest_wins [INFERRED 0.95]

## Communities (38 total, 4 thin omitted)

### Community 0 - "Manifest"
Cohesion: 0.13
Nodes (14): A note on the network, Card art, Checking which version you are running, Files, Getting your data out, Logging cards, Manifest, Options (+6 more)

### Community 1 - "Manifest.Models"
Cohesion: 0.06
Nodes (22): Manifest.Tools, Manifest.Web, Manifest.Services, Manifest.Data, Manifest.Models, Exception, IEnumerable, AccountError (+14 more)

### Community 2 - "esc (HTML escape)"
Cohesion: 0.23
Nodes (16): cardModalHTML, Deferred effect-text fetch, Leader cost column holds Life, Scheduled price snapshots, not live pricing, /img/{card_id} image proxy, esc (HTML escape), flash, fmtGBP (+8 more)

### Community 3 - "CardRepository"
Cohesion: 0.05
Nodes (37): Band, List, SqliteConnection, SqliteDataReader, string, CardRepository, List, SqliteConnection (+29 more)

### Community 4 - ".Open"
Cohesion: 0.06
Nodes (33): Acted, InviteToken, DateTime, int, List, SqliteConnection, SqliteDataReader, TimeSpan (+25 more)

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
Cohesion: 0.06
Nodes (24): ConcurrentDictionary, DateTime, List, SqliteConnection, SqliteDataReader, string, TimeSpan, UserRepository (+16 more)

### Community 10 - "HttpContext"
Cohesion: 0.19
Nodes (7): HashSet, HttpContext, RequestDelegate, string, Task, Auth, AuthContext

### Community 11 - "send (scan submit)"
Cohesion: 0.16
Nodes (14): startup health bootstrap IIFE, Stream-restart autofocus fallback, Photo-first scanning fallback, OCR-vs-API scan engine selection, Secure-context requirement for live camera, GET /api/health, POST /api/scan, GET /api/stats (+6 more)

### Community 13 - "CardId"
Cohesion: 0.07
Nodes (24): byte, InlineData, IReadOnlyList, Dictionary, GeneratedRegex, List, Regex, CardId (+16 more)

### Community 14 - "ServerFixture"
Cohesion: 0.07
Nodes (20): Body, bool, Manifest.Tests, IAsyncLifetime, ICollectionFixture, HttpClient, JsonElement, Process (+12 more)

### Community 15 - "AppConfig"
Cohesion: 0.20
Nodes (7): HashSet, int, long, string, AppConfig, string, Cli

### Community 16 - "CatalogRefresh"
Cohesion: 0.17
Nodes (10): AppPaths, JsonSerializerOptions, List, string, Task, CatalogRefresh, TitleParts, VegapullCard (+2 more)

### Community 17 - "ServingTests"
Cohesion: 0.20
Nodes (7): Action, HttpMethod, HttpRequestMessage, Fact, HttpResponseMessage, Task, ServingTests

### Community 18 - "JsonSerializerOptions"
Cohesion: 0.29
Nodes (6): JsonSerializerOptions, Type, Utf8JsonReader, Utf8JsonWriter, PythonStyleDoubleConverter, PythonStyleNullableDoubleConverter

### Community 19 - "CatalogAndLookupTests"
Cohesion: 0.28
Nodes (4): Fact, JsonElement, Task, CatalogAndLookupTests

### Community 20 - "DeckTests"
Cohesion: 0.29
Nodes (7): Deck, Id, Fact, JsonElement, string, Task, DeckTests

### Community 21 - "CollectionTests"
Cohesion: 0.33
Nodes (3): Fact, Task, CollectionTests

### Community 22 - "Manifest.Tests.csproj"
Cohesion: 0.15
Nodes (11): net8.0, net8.0, Anthropic (12.40.0), MailKit (4.17.0), Microsoft.Data.Sqlite (8.0.11), Microsoft.NET.Test.Sdk (17.11.1), xunit (2.9.2), xunit.runner.visualstudio (2.8.2) (+3 more)

### Community 23 - "Responses"
Cohesion: 0.33
Nodes (5): Key, HttpContext, Task, Responses, Value

### Community 25 - "Manifest — server"
Cohesion: 0.33
Nodes (5): Layout, Manifest — server, Notes, Running it, Tests

### Community 27 - "Database"
Cohesion: 0.13
Nodes (13): AppPaths, long, SqliteConnection, string, Database, AppPaths, Dictionary, JsonSerializerOptions (+5 more)

### Community 28 - "setDeckCardQty"
Cohesion: 0.22
Nodes (14): Guideline bands are advisory, not rules, Leader colour lock for the add-cards browser, Preview panel as single quantity source of truth, Server-computed legality and guideline bands, deckQtyOf, guidePill, renderDeckBrowse, renderDeckCards (+6 more)

### Community 29 - "wire (row +/- handlers)"
Cohesion: 0.27
Nodes (11): cardIndex (client card cache), Duplicated filter controls per pane, Stable sort with card_id tiebreak, /api/collection, indexCards, loadOwned, paintOwned, readFilters (+3 more)

### Community 31 - "Requests.cs"
Cohesion: 0.20
Nodes (9): List, AccessDecisionPost, AccessRequestPost, CollectionPost, DeckCardPost, DeckPost, LoginPost, RegisterPost (+1 more)

### Community 32 - "api (fetch wrapper)"
Cohesion: 0.40
Nodes (10): api (fetch wrapper), Abort-and-latest-wins search race guard, doDeckSearch, doLeaderSearch, doSearch, GET /api/card/{id}, GET /api/search, hoverPreview (+2 more)

### Community 33 - "Manifest"
Cohesion: 0.22
Nodes (4): Manifest, AppPaths, JsonSerializerOptions, Json

### Community 34 - "pickLeader"
Cohesion: 0.28
Nodes (9): Conditional wide desktop workspace, /api/decks, openDeck, openLeaderPicker, pickLeader, renderDeckList, showDeckList, showDeckSub (+1 more)

### Community 35 - "LenientIntConverter"
Cohesion: 0.29
Nodes (6): JsonConverter, JsonSerializerOptions, Type, Utf8JsonReader, Utf8JsonWriter, LenientIntConverter

### Community 36 - "Guard"
Cohesion: 0.48
Nodes (4): HttpContext, RequestDelegate, Task, Guard

## Ambiguous Edges - Review These
- `Leader colour lock for the add-cards browser` → `Server-computed legality and guideline bands`  [AMBIGUOUS]
  ui.html · relation: conceptually_related_to

## Knowledge Gaps
- **56 isolated node(s):** `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)`, `xunit.runner.visualstudio (2.8.2)`, `Xunit.SkippableFact (1.5.61)` (+51 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **4 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `Leader colour lock for the add-cards browser` and `Server-computed legality and guideline bands`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `Manifest.Tests` connect `ServerFixture` to `Manifest.Models`, `CardId`?**
  _High betweenness centrality (0.286) - this node is a cross-community bridge._
- **Why does `Manifest.Services` connect `Manifest.Models` to `UserRepository`, `.Open`, `CardId`?**
  _High betweenness centrality (0.245) - this node is a cross-community bridge._
- **Why does `Manifest.Tools` connect `Manifest.Models` to `CatalogRefresh`, `Database`?**
  _High betweenness centrality (0.136) - this node is a cross-community bridge._
- **What connects `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)` to the rest of the system?**
  _56 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Manifest` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._
- **Should `Manifest.Models` be split into smaller, more focused modules?**
  _Cohesion score 0.06292517006802721 - nodes in this community are weakly interconnected._