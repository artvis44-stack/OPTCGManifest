# Graph Report - optcg-manifest  (2026-08-18)

## Corpus Check
- 46 files · ~22,388,365 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 571 nodes · 1082 edges · 29 communities (25 shown, 4 thin omitted)
- Extraction: 96% EXTRACTED · 4% INFERRED · 0% AMBIGUOUS · INFERRED: 43 edges (avg confidence: 0.82)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Manifest
- Manifest.Web
- api (fetch wrapper)
- .Open
- Manifest.Services
- Manifest.Tests
- CatalogScraper
- loadFacets
- cornerVariants
- Endpoints
- TesseractScanner
- .Read
- make_cert.sh
- CardId
- ServerFixture
- Manifest
- CatalogRefresh
- ServingTests
- Requests.cs
- CatalogAndLookupTests
- DeckTests
- CollectionTests
- Manifest.Tests.csproj
- Responses
- ReaderExtensions
- Manifest — server
- CLAUDE.md
- PriceRefresh

## God Nodes (most connected - your core abstractions)
1. `CatalogScraper` - 29 edges
2. `CatalogAndLookupTests` - 19 edges
3. `ServingTests` - 19 edges
4. `api (fetch wrapper)` - 18 edges
5. `ServerFixture` - 17 edges
6. `CardRepository` - 17 edges
7. `DeckTests` - 16 edges
8. `CollectionTests` - 14 edges
9. `CardId` - 13 edges
10. `Manifest` - 13 edges

## Surprising Connections (you probably didn't know these)
- `ApiScanner` --references--> `CardRepository`  [EXTRACTED]
  Manifest/Services/ApiScanner.cs → Manifest/Data/CardRepository.cs
- `TesseractScanner` --references--> `Database`  [EXTRACTED]
  Manifest/Services/TesseractScanner.cs → Manifest/Data/Database.cs
- `ScanResponse` --references--> `CatalogRow`  [EXTRACTED]
  Manifest/Models/Scan.cs → Manifest/Models/Cards.cs
- `Guard` --references--> `AppConfig`  [EXTRACTED]
  Manifest/Web/Guard.cs → Manifest/AppConfig.cs
- `CardRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/Database.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Client-side OCR capture and preprocessing pipeline** — ui_shrink, ui_cornervariants, ui_send, ui_concept_object_fit_cover_crop, ui_concept_min_vs_chroma_channel, ui_concept_plane_fit_flatfield, ui_concept_multi_region_multi_config_ocr [EXTRACTED 1.00]
- **Deck editor re-render pipeline** — ui_renderdeckeditor, ui_renderdeckhead, ui_renderdecklegal, ui_renderdeckguide, ui_renderdeckcards, ui_renderbuylist, ui_setdeckcardqty [EXTRACTED 1.00]
- **Abort-latest-wins async fetch family** — ui_dosearch, ui_doleadersearch, ui_dodecksearch, ui_loadpreview, ui_concept_abort_latest_wins [INFERRED 0.95]

## Communities (29 total, 4 thin omitted)

### Community 0 - "Manifest"
Cohesion: 0.13
Nodes (14): A note on the network, Card art, Checking which version you are running, Files, Getting your data out, Logging cards, Manifest, Options (+6 more)

### Community 1 - "Manifest.Web"
Cohesion: 0.17
Nodes (7): Manifest.Web, AppConfig, AppPaths, WebApplication, SetupHelper, string, SetupPage

### Community 2 - "api (fetch wrapper)"
Cohesion: 0.06
Nodes (74): api (fetch wrapper), startup health bootstrap IIFE, cardIndex (client card cache), cardModalHTML, Abort-and-latest-wins search race guard, Stream-restart autofocus fallback, Duplicated filter controls per pane, Guideline bands are advisory, not rules (+66 more)

### Community 3 - ".Open"
Cohesion: 0.06
Nodes (37): Band, List, SqliteConnection, SqliteDataReader, string, CardRepository, AppPaths, SqliteConnection (+29 more)

### Community 4 - "Manifest.Services"
Cohesion: 0.24
Nodes (5): Manifest.Services, Manifest.Data, Manifest.Models, Exception, RuleViolation

### Community 6 - "CatalogScraper"
Cohesion: 0.10
Nodes (19): Label, Fact, string, ScraperTests, AppPaths, GeneratedRegex, HttpClient, List (+11 more)

### Community 7 - "loadFacets"
Cohesion: 0.22
Nodes (9): Facet-driven filter dropdowns, Rarity abbreviation vintage tolerance, Log-anything tolerance for uncatalogued cards, GET /api/facets, fillSelect, loadFacets, orderBy, RARITY_LABEL lookup table (+1 more)

### Community 8 - "cornerVariants"
Cohesion: 0.29
Nodes (8): camConstraints (portrait video), Client-side OCR preprocessing, Freeze-frame capture on shoot, min-channel vs chroma ink separation, Multi-region, multi-config OCR shotgun, object-fit:cover crop compensation, Least-squares plane-fit flat-fielding, cornerVariants

### Community 9 - "Endpoints"
Cohesion: 0.16
Nodes (8): IEnumerable, CsvExport, HttpContext, string, Task, WebApplication, Endpoints, StringBuilder

### Community 10 - "TesseractScanner"
Cohesion: 0.24
Nodes (6): IReadOnlyList, AppPaths, string, Reading, TesseractScanner, Reading

### Community 11 - ".Read"
Cohesion: 0.22
Nodes (6): ScanResponse, VisionReading, string, Task, ApiScanner, MediaType

### Community 13 - "CardId"
Cohesion: 0.10
Nodes (18): byte, InlineData, Dictionary, GeneratedRegex, List, Regex, CardId, AppPaths (+10 more)

### Community 14 - "ServerFixture"
Cohesion: 0.10
Nodes (17): Body, bool, IAsyncLifetime, ICollectionFixture, HttpClient, JsonElement, Process, Task (+9 more)

### Community 15 - "Manifest"
Cohesion: 0.08
Nodes (17): Manifest, long, HashSet, int, string, AppConfig, AppPaths, string (+9 more)

### Community 16 - "CatalogRefresh"
Cohesion: 0.17
Nodes (10): AppPaths, JsonSerializerOptions, List, string, Task, CatalogRefresh, TitleParts, VegapullCard (+2 more)

### Community 17 - "ServingTests"
Cohesion: 0.20
Nodes (7): Action, HttpMethod, HttpRequestMessage, HttpResponseMessage, Fact, Task, ServingTests

### Community 18 - "Requests.cs"
Cohesion: 0.11
Nodes (17): JsonConverter, JsonSerializerOptions, Type, Utf8JsonReader, Utf8JsonWriter, PythonStyleDoubleConverter, PythonStyleNullableDoubleConverter, JsonSerializerOptions (+9 more)

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
Cohesion: 0.17
Nodes (10): net8.0, net8.0, Anthropic (12.40.0), Microsoft.Data.Sqlite (8.0.11), Microsoft.NET.Test.Sdk (17.11.1), xunit (2.9.2), xunit.runner.visualstudio (2.8.2), Xunit.SkippableFact (1.5.61) (+2 more)

### Community 23 - "Responses"
Cohesion: 0.39
Nodes (5): Key, HttpContext, Task, Responses, Value

### Community 25 - "Manifest — server"
Cohesion: 0.33
Nodes (5): Layout, Manifest — server, Notes, Running it, Tests

### Community 27 - "PriceRefresh"
Cohesion: 0.29
Nodes (6): Dictionary, JsonSerializerOptions, string, FxResponse, PricedCard, PriceRefresh

## Ambiguous Edges - Review These
- `Leader colour lock for the add-cards browser` → `Server-computed legality and guideline bands`  [AMBIGUOUS]
  ui.html · relation: conceptually_related_to

## Knowledge Gaps
- **46 isolated node(s):** `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)`, `xunit.runner.visualstudio (2.8.2)`, `Xunit.SkippableFact (1.5.61)` (+41 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **4 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `Leader colour lock for the add-cards browser` and `Server-computed legality and guideline bands`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `Manifest.Tests` connect `Manifest.Tests` to `ServerFixture`?**
  _High betweenness centrality (0.289) - this node is a cross-community bridge._
- **Why does `Manifest.Services` connect `Manifest.Services` to `Endpoints`, `CardId`, `Manifest.Tests`, `Manifest.Web`?**
  _High betweenness centrality (0.218) - this node is a cross-community bridge._
- **Why does `Manifest.Tools` connect `Manifest.Tests` to `CatalogRefresh`, `PriceRefresh`?**
  _High betweenness centrality (0.155) - this node is a cross-community bridge._
- **What connects `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)` to the rest of the system?**
  _46 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Manifest` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._
- **Should `api (fetch wrapper)` be split into smaller, more focused modules?**
  _Cohesion score 0.05960755275823769 - nodes in this community are weakly interconnected._