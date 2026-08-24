# Graph Report - optcg-manifest  (2026-08-18)

## Corpus Check
- 51 files · ~22,062,710 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 676 nodes · 1280 edges · 28 communities (24 shown, 4 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 43 edges (avg confidence: 0.82)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Manifest
- server.py
- api (fetch wrapper)
- .Open
- SeriesParser
- test_manifest.py
- CatalogScraper
- loadFacets
- cornerVariants
- Manifest.Services
- refresh_prices.py
- make_cert.sh
- CardId
- ServerFixture
- Manifest
- Manifest.Tests
- ServingTests
- Requests.cs
- CatalogAndLookupTests
- DeckTests
- CollectionTests
- Manifest.Tests.csproj
- Responses
- ReaderExtensions
- Manifest — C# port
- CLAUDE.md

## God Nodes (most connected - your core abstractions)
1. `CatalogScraper` - 29 edges
2. `CatalogAndLookupTests` - 19 edges
3. `ServingTests` - 19 edges
4. `db()` - 19 edges
5. `api (fetch wrapper)` - 18 edges
6. `ServerFixture` - 17 edges
7. `CardRepository` - 17 edges
8. `DeckTests` - 16 edges
9. `CollectionTests` - 14 edges
10. `CardId` - 13 edges

## Surprising Connections (you probably didn't know these)
- `TesseractScanner` --references--> `Database`  [EXTRACTED]
  Manifest/Services/TesseractScanner.cs → Manifest/Data/Database.cs
- `Guard` --references--> `AppConfig`  [EXTRACTED]
  Manifest/Web/Guard.cs → Manifest/AppConfig.cs
- `CardRepository` --references--> `Database`  [EXTRACTED]
  Manifest/Data/CardRepository.cs → Manifest/Data/Database.cs
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

## Communities (28 total, 4 thin omitted)

### Community 0 - "Manifest"
Cohesion: 0.13
Nodes (14): A note on the network, Card art, Checking which version you are running, Files, Getting your data out, Logging cards, Manifest, Options (+6 more)

### Community 1 - "server.py"
Cohesion: 0.06
Nodes (59): BaseHTTPRequestHandler, adjust(), _band(), card_detail(), card_image(), _catalog_row(), cert_covers_ip(), collection() (+51 more)

### Community 2 - "api (fetch wrapper)"
Cohesion: 0.06
Nodes (74): api (fetch wrapper), startup health bootstrap IIFE, cardIndex (client card cache), cardModalHTML, Abort-and-latest-wins search race guard, Stream-restart autofocus fallback, Duplicated filter controls per pane, Guideline bands are advisory, not rules (+66 more)

### Community 3 - ".Open"
Cohesion: 0.05
Nodes (43): Band, List, SqliteConnection, SqliteDataReader, string, CardRepository, AppPaths, SqliteConnection (+35 more)

### Community 4 - "SeriesParser"
Cohesion: 0.18
Nodes (10): HTMLParser, fetch(), main(), parse_cards(), BOOSTER PACK -Romance Dawn- [OP-01]' -> prefix, title, label., Each card is a <dl id="OP01-016" ...> … </dl> block., Reads <select id="series"><option value="569001">…</option>., SeriesParser (+2 more)

### Community 5 - "test_manifest.py"
Cohesion: 0.36
Nodes (10): check(), get(), main(), post(), Local OCR against synthetic card corners. Skips if the tools are absent., With --https on, the setup page must also be reachable over plain http, or the…, raw(), test_ocr() (+2 more)

### Community 6 - "CatalogScraper"
Cohesion: 0.10
Nodes (19): Label, Fact, string, ScraperTests, AppPaths, GeneratedRegex, HttpClient, List (+11 more)

### Community 7 - "loadFacets"
Cohesion: 0.22
Nodes (9): Facet-driven filter dropdowns, Rarity abbreviation vintage tolerance, Log-anything tolerance for uncatalogued cards, GET /api/facets, fillSelect, loadFacets, orderBy, RARITY_LABEL lookup table (+1 more)

### Community 8 - "cornerVariants"
Cohesion: 0.29
Nodes (8): camConstraints (portrait video), Client-side OCR preprocessing, Freeze-frame capture on shoot, min-channel vs chroma ink separation, Multi-region, multi-config OCR shotgun, object-fit:cover crop compensation, Least-squares plane-fit flat-fielding, cornerVariants

### Community 9 - "Manifest.Services"
Cohesion: 0.06
Nodes (27): byte, Manifest.Web, Manifest.Services, Manifest.Data, Manifest.Models, Exception, IEnumerable, CsvExport (+19 more)

### Community 13 - "CardId"
Cohesion: 0.10
Nodes (17): InlineData, IReadOnlyList, Dictionary, GeneratedRegex, List, Regex, CardId, Task (+9 more)

### Community 14 - "ServerFixture"
Cohesion: 0.10
Nodes (17): Body, bool, IAsyncLifetime, ICollectionFixture, HttpClient, JsonElement, Process, Task (+9 more)

### Community 15 - "Manifest"
Cohesion: 0.08
Nodes (17): Manifest, long, HashSet, int, string, AppConfig, AppPaths, string (+9 more)

### Community 16 - "Manifest.Tests"
Cohesion: 0.07
Nodes (18): Manifest.Tools, Manifest.Tests, AppPaths, JsonSerializerOptions, List, string, Task, CatalogRefresh (+10 more)

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
Cohesion: 0.33
Nodes (5): Key, HttpContext, Task, Responses, Value

### Community 25 - "Manifest — C# port"
Cohesion: 0.33
Nodes (5): Layout, Manifest — C# port, Notes on the port, Running it, Tests

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
  _High betweenness centrality (0.206) - this node is a cross-community bridge._
- **Why does `Manifest.Services` connect `Manifest.Services` to `Manifest.Tests`?**
  _High betweenness centrality (0.156) - this node is a cross-community bridge._
- **Why does `Manifest.Tools` connect `Manifest.Tests` to `Manifest.Services`?**
  _High betweenness centrality (0.110) - this node is a cross-community bridge._
- **What connects `net8.0`, `Microsoft.NET.Test.Sdk (17.11.1)`, `xunit (2.9.2)` to the rest of the system?**
  _46 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Manifest` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._
- **Should `server.py` be split into smaller, more focused modules?**
  _Cohesion score 0.05859969558599695 - nodes in this community are weakly interconnected._