using System.Net;
using System.Text.RegularExpressions;

namespace Manifest.Tools;

/// <summary>
/// Pull the current card list straight from the official One Piece card site.
///
/// Deliberately slow — one request per set with a pause between, because this is
/// someone's website and we are guests on it.
///
/// Approach ported from the vegapull scraper (github.com/Coko7/vegapull, MIT).
/// The site is server-rendered: /cardlist lists sets in a &lt;select id="series"&gt;,
/// and /cardlist?series=&lt;id&gt; returns every card in that set as &lt;dl&gt; blocks.
/// </summary>
public static partial class CatalogScraper
{
    /// <summary>
    /// One language's edition of the card site, and the file its scrape is kept in.
    /// The Japanese site is the same markup on another host; what it says inside the
    /// fields is Japanese, which <see cref="Services.JapanesePrints"/> deals with at seeding.
    /// </summary>
    public sealed record Site(string Code, string Host, string FileName)
    {
        // Trailing slash matters: /cardlist redirects to /cardlist/ over plain http.
        public string CardList => Host + "/cardlist/";
        public string PathIn(AppPaths paths) => Path.Combine(paths.Root, FileName);
    }

    public static readonly Site English = new("en", "https://en.onepiece-cardgame.com", "catalog.json");
    public static readonly Site Japanese = new("jp", "https://www.onepiece-cardgame.com", "catalog-jp.json");
    const string UserAgent =
        "Mozilla/5.0 (compatible; Manifest/1.0; self-hosted collection tracker)";
    static readonly TimeSpan DefaultPause = TimeSpan.FromSeconds(1.5);

    public sealed record Pack(string Id, string RawTitle)
    {
        public string? Prefix { get; set; }
        public string? Title { get; set; }
        public string? Label { get; set; }
    }

    public sealed class ScrapedCard
    {
        public string CardId { get; set; } = "";
        public string BaseId { get; set; } = "";
        public string Variant { get; set; } = "";
        public string Name { get; set; } = "";
        public string SetLabel { get; set; } = "";
        public string SetName { get; set; } = "";
        public string Rarity { get; set; } = "";
        public string Category { get; set; } = "";
        public string Colors { get; set; } = "";
        public int? Cost { get; set; }
        public int? Power { get; set; }
        public int? Counter { get; set; }
        public string Types { get; set; } = "";
        public string Attributes { get; set; } = "";
        public string Effect { get; set; } = "";
        public string Trigger { get; set; } = "";
        public int? BlockIcon { get; set; }
        public string ImageUrl { get; set; } = "";
    }

    [GeneratedRegex(@"(?is)<br\s*/?>")] private static partial Regex BrTag();
    [GeneratedRegex(@"(?is)<h3\b[^>]*>.*?</h3>")] private static partial Regex H3Block();
    [GeneratedRegex(@"(?s)<[^>]+>")] private static partial Regex AnyTag();
    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
    // [OP-11], and the combined boosters' [OP14-EB04]; the Japanese site writes 【OP-11】.
    [GeneratedRegex(@"[\[【]([A-Z]{2,4}-?\d{2}(?:-[A-Z]{2,4}-?\d{2})?)[\]】]")]
    private static partial Regex LabelPattern();
    [GeneratedRegex(@"^([A-Z][A-Z !'&\.]+?)\s*-")] private static partial Regex PrefixPattern();
    [GeneratedRegex(@"(?is)<select[^>]*\bid=""series""[^>]*>(.*?)</select>")]
    private static partial Regex SeriesSelect();
    [GeneratedRegex(@"(?is)<option[^>]*\bvalue=""([^""]*)""[^>]*>(.*?)</option>")]
    private static partial Regex OptionTag();
    [GeneratedRegex(@"(?is)<dl\b[^>]*\bid=""([^""]+)""[^>]*>(.*?)</dl>")]
    private static partial Regex DlBlock();
    [GeneratedRegex(@"^[A-Z]{1,4}\d{0,2}-?\d{3}(_[a-z]\d)?$", RegexOptions.IgnoreCase)]
    private static partial Regex CardIdShape();
    [GeneratedRegex(@"(?is)<span[^>]*>(.*?)</span>")] private static partial Regex SpanTag();
    [GeneratedRegex(@"(?is)<div\s+class=""[^""]*\bcardName\b[^""]*""[^>]*>(.*?)</div>")]
    private static partial Regex CardNameDiv();
    [GeneratedRegex(@"(?is)<div\s+class=""[^""]*\bfrontCol\b[^""]*""[^>]*>\s*<img[^>]*\bsrc=""([^""]+)""")]
    private static partial Regex FrontColImg();
    [GeneratedRegex(@"(?is)<img[^>]*\bdata-src=""([^""]+)""")] private static partial Regex DataSrcImg();
    [GeneratedRegex(@"(?is)<div\s+class=""[^""]*\battribute\b[^""]*""[^>]*>(?:(?!</div>).)*?<img[^>]*\balt=""([^""]*)""")]
    private static partial Regex AttributeIcon();
    [GeneratedRegex(@"[^\d]")] private static partial Regex NonDigit();
    [GeneratedRegex(@"^p(\d+)$")] private static partial Regex AltArtSuffix();
    [GeneratedRegex(@"^r(\d+)$")] private static partial Regex ReprintSuffix();

    public static string StripTags(string? s)
    {
        s = BrTag().Replace(s ?? "", " ");
        s = H3Block().Replace(s, "");                    // field label
        s = AnyTag().Replace(s, "");
        return Spaces().Replace(WebUtility.HtmlDecode(s), " ").Trim();
    }

    /// <summary>Reads &lt;select id="series"&gt;&lt;option value="569001"&gt;…&lt;/option&gt;.</summary>
    public static List<Pack> ParseSeries(string doc)
    {
        var packs = new List<Pack>();
        var select = SeriesSelect().Match(doc);
        if (!select.Success) return packs;

        foreach (Match option in OptionTag().Matches(select.Groups[1].Value))
        {
            var value = option.Groups[1].Value;
            var title = CleanTitle(option.Groups[2].Value);
            if (value.Length > 0 && title.Length > 0)
                packs.Add(new Pack(value, title));
        }
        return packs;
    }

    /// <summary>
    /// A set title as text. Some carry a &lt;br class="spInline"&gt; for the site's phone
    /// layout, which older scrapes kept, so a set name read "BOOSTER PACK &lt;br …&gt;-ROMANCE DAWN".
    /// The site escapes it inside the &lt;option&gt; (&amp;lt;br …&amp;gt;), so it only becomes
    /// a tag to strip once decoded.
    /// </summary>
    public static string CleanTitle(string raw) =>
        Spaces().Replace(AnyTag().Replace(WebUtility.HtmlDecode(raw), " "), " ").Trim();

    /// <summary>
    /// Puts right a printing an older scrape filed under a set name with markup in it -
    /// and, for the combined boosters, under the whole title in place of a label.
    /// </summary>
    public static void RepairSet(ScrapedCard card)
    {
        if (!card.SetName.Contains('<') && !card.SetLabel.Contains('<')) return;
        var (_, title, label) = SplitTitle(CleanTitle(card.SetLabel.Contains('<') ? card.SetLabel : card.SetName));
        if (card.SetLabel.Contains('<')) card.SetLabel = label ?? title;
        card.SetName = title;
    }

    /// <summary>'BOOSTER PACK -Romance Dawn- [OP-01]' -> prefix, title, label.</summary>
    public static (string? Prefix, string Title, string? Label) SplitTitle(string raw)
    {
        string? label = null;
        var m = LabelPattern().Match(raw);
        if (m.Success)
        {
            label = m.Groups[1].Value;
            raw = raw.Replace(m.Value, "").Trim();
        }

        string? prefix = null;
        m = PrefixPattern().Match(raw);
        if (m.Success)
        {
            prefix = m.Groups[1].Value.Trim();
            raw = raw[(m.Index + m.Length - 1)..].Trim();
        }

        return (prefix, raw.Trim(' ', '-', '–', '—'), label);
    }

    /// <summary>Each card is a &lt;dl id="OP01-016" ...&gt; … &lt;/dl&gt; block.</summary>
    public static List<ScrapedCard> ParseCards(string doc, string? host = null)
    {
        host ??= English.Host;
        var out_ = new List<ScrapedCard>();

        foreach (Match block in DlBlock().Matches(doc))
        {
            var cid = block.Groups[1].Value.Trim();
            var blk = block.Groups[2].Value;
            if (!CardIdShape().IsMatch(cid)) continue;

            string Field(string cls)
            {
                var f = Regex.Match(blk,
                    @"(?is)<div\s+class=""[^""]*\b" + cls + @"\b[^""]*""[^>]*>(.*?)</div>");
                var v = f.Success ? StripTags(f.Groups[1].Value) : "";
                return v is "-" or "—" or "ー" ? "" : v;
            }

            int? Number(string cls)
            {
                var v = NonDigit().Replace(Field(cls), "");
                return v.Length > 0 ? int.Parse(v) : null;
            }

            var nameMatch = CardNameDiv().Match(blk);
            var name = nameMatch.Success ? StripTags(nameMatch.Groups[1].Value) : "";

            var info = SpanTag().Matches(blk).Select(x => StripTags(x.Groups[1].Value)).ToList();
            var rarity = info.Count > 1 ? info[1] : "";
            var category = info.Count > 2 ? info[2] : "";

            var img = "";
            var i = FrontColImg().Match(blk);
            if (!i.Success) i = DataSrcImg().Match(blk);
            if (i.Success)
            {
                var src = i.Groups[1].Value;
                img = src.StartsWith('.') ? host + "/" + src.TrimStart('.', '/')
                    : src.StartsWith("http") ? src
                    : host + "/" + src.TrimStart('/');
            }

            var colors = Regex.Split(Field("color"), "[/,]")
                              .Select(c => c.Trim()).Where(c => c.Length > 0);
            var types = Field("feature").Split('/')
                              .Select(t => t.Trim()).Where(t => t.Length > 0);
            // Read off the icon: the Japanese site prints no text beside it. A card
            // with two attributes has one icon for both, "Slash/Special".
            var icon = AttributeIcon().Match(blk);
            var attributes = (icon.Success ? WebUtility.HtmlDecode(icon.Groups[1].Value) : "")
                              .Split('/').Select(a => a.Trim()).Where(a => a.Length > 0);

            var parts = cid.Split('_');
            var suffix = parts.Length > 1 ? parts[1] : "";
            var variant = VariantLabel(suffix);

            out_.Add(new ScrapedCard
            {
                CardId = cid,
                BaseId = parts[0],
                Variant = variant,
                Name = name,
                Rarity = rarity,
                Category = category,
                Colors = string.Join(", ", colors),
                Cost = Number("cost"),
                Power = Number("power"),
                Counter = Number("counter"),
                Types = string.Join(", ", types),
                Attributes = string.Join(", ", attributes),
                Effect = Field("text"),
                // A box of its own on the site, separate from the effect text.
                Trigger = Field("trigger"),
                BlockIcon = Number("block"),
                ImageUrl = img,
            });
        }
        return out_;
    }

    public static string VariantLabel(string suffix)
    {
        if (suffix.Length == 0) return "";
        var m = AltArtSuffix().Match(suffix);
        if (m.Success) return "Alt art" + (m.Groups[1].Value == "1" ? "" : " " + m.Groups[1].Value);
        m = ReprintSuffix().Match(suffix);
        if (m.Success) return "Reprint" + (m.Groups[1].Value == "1" ? "" : " " + m.Groups[1].Value);
        return suffix;
    }

    static HttpClient NewClient(Site site)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        http.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        http.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
        http.DefaultRequestHeaders.Add("Accept-Language",
                                       site == Japanese ? "ja-JP,ja;q=0.9" : "en-US,en;q=0.9");
        return http;
    }

    /// <summary>
    /// Fetch a page, following the site's redirects.
    ///
    /// HttpClient follows redirects itself, but it refuses to follow one that
    /// downgrades https to http, and hands the 3xx back instead. The card site
    /// does exactly that (it redirects to an http:// URL and relies on the
    /// browser's HSTS to put it back on https), so we walk those hops by hand
    /// and force the scheme back to https each time.
    /// </summary>
    static async Task<string> GetHtml(HttpClient http, string url)
    {
        for (var hop = 0; hop < 5; hop++)
        {
            using var res = await http.GetAsync(url);
            if ((int)res.StatusCode is < 300 or >= 400)
            {
                res.EnsureSuccessStatusCode();
                return await res.Content.ReadAsStringAsync();
            }

            var next = res.Headers.Location
                ?? throw new HttpRequestException($"{(int)res.StatusCode} with no Location header");
            var absolute = next.IsAbsoluteUri ? next : new Uri(new Uri(url), next);
            url = new UriBuilder(absolute) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri.ToString();
        }
        throw new HttpRequestException($"too many redirects fetching {url}");
    }

    /// <summary>What a scrape did, for a job's result and the admin page.</summary>
    public sealed record Outcome(int Printings, int Added, IReadOnlyList<string> Fetched);

    /// <summary>
    /// The sets a --new run fetches: any whose label the catalogue does not have yet,
    /// and the unlabelled lists (promotion cards, other products), which keep growing
    /// without ever becoming a new set.
    /// </summary>
    public static List<Pack> NotYetHeld(IEnumerable<Pack> packs, IReadOnlySet<string> heldLabels) =>
        packs.Where(p => p.Label is null || !heldLabels.Contains(p.Label)).ToList();

    /// <summary>Printings scraped before the Trigger box was read: there is not one Trigger among them.</summary>
    public static bool PredatesTriggers(IReadOnlyCollection<ScrapedCard> rows) =>
        rows.Count > 0 && rows.All(r => string.IsNullOrEmpty(r.Trigger));

    public static async Task<int> Run(string[] args, AppPaths paths)
    {
        var only = new List<string>();
        var listOnly = false;
        var merge = false;
        var newOnly = false;
        var pause = DefaultPause;
        var site = English;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--only":
                    while (i + 1 < args.Length && !args[i + 1].StartsWith("--")) only.Add(args[++i]);
                    break;
                case "--list": listOnly = true; break;
                case "--merge": merge = true; break;
                case "--new": newOnly = true; break;
                case "--pause": pause = TimeSpan.FromSeconds(double.Parse(args[++i])); break;
                case "--lang":
                    var lang = i + 1 < args.Length ? args[++i].ToLowerInvariant() : "";
                    site = lang is "jp" or "ja" ? Japanese : lang is "en" ? English
                        : throw new ArgumentException($"unknown --lang {lang}; use en or jp");
                    break;
            }
        }

        var outcome = await Scrape(paths, site, only, listOnly, merge, newOnly, pause);
        if (outcome is null) return 1;
        if (!listOnly) Console.WriteLine("\nnow run: manifest --reseed");
        return 0;
    }

    /// <summary>
    /// Sets that are already in catalog.json are skipped and everything else is merged in;
    /// this is what the scheduled job and the admin page's button run. A handful of
    /// requests when nothing is new, so it is fine to run daily.
    /// </summary>
    public static Task<Outcome?> ScrapeNew(AppPaths paths, Site? site = null) =>
        Scrape(paths, site ?? English, new List<string>(), listOnly: false, merge: true, newOnly: true,
               DefaultPause);

    static async Task<Outcome?> Scrape(AppPaths paths, Site site, List<string> only, bool listOnly,
                                       bool merge, bool newOnly, TimeSpan pause)
    {
        using var http = NewClient(site);
        var file = site.PathIn(paths);
        var cardList = site.CardList;
        Console.WriteLine($"reading {cardList}");
        string doc;
        try
        {
            doc = await GetHtml(http, cardList);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"could not reach the card site: {e.Message}\n"
                                    + "Check this machine's connection, then try again.");
            return null;
        }

        var packs = ParseSeries(doc);
        if (packs.Count == 0)
        {
            Console.Error.WriteLine(
                "no sets found on the page. The site's markup has probably changed;\n"
                + "the bundled catalog.json still works, it is just older.");
            return null;
        }

        foreach (var p in packs)
        {
            var (prefix, title, label) = SplitTitle(p.RawTitle);
            p.Prefix = prefix; p.Title = title; p.Label = label;
        }

        Console.WriteLine($"{packs.Count} sets listed");
        if (listOnly)
        {
            foreach (var p in packs)
                Console.WriteLine($"  {(p.Label ?? "-"),-14} {p.Id,-8} {p.Title}");
            return new Outcome(0, 0, Array.Empty<string>());
        }

        var wanted = packs;
        if (only.Count > 0)
        {
            var keep = only.Select(s => s.ToUpperInvariant()).ToHashSet();
            wanted = packs.Where(p => keep.Contains((p.Label ?? "").ToUpperInvariant())).ToList();
            if (wanted.Count == 0)
            {
                Console.Error.WriteLine($"none of {string.Join(", ", keep.Order())} are listed. "
                                        + "Run --list to see set labels.");
                return null;
            }
        }

        var rows = new Dictionary<string, ScrapedCard>();
        if ((merge || newOnly) && File.Exists(file))
        {
            await using var stream = File.OpenRead(file);
            var existing = await System.Text.Json.JsonSerializer
                .DeserializeAsync<List<ScrapedCard>>(stream, Json.Options) ?? new();
            foreach (var r in existing)
            {
                RepairSet(r);
                rows[r.CardId] = r;
            }
            Console.WriteLine($"starting from {rows.Count} existing printings");
        }

        // A file from before the scraper read the Trigger box has none on any card,
        // so every set is fetched again, once, rather than only the new ones.
        if (newOnly && PredatesTriggers(rows.Values))
            Console.WriteLine("no Trigger text in the existing printings; fetching every set again");
        else if (newOnly)
        {
            var held = rows.Values.Select(r => r.SetLabel).ToHashSet();
            wanted = NotYetHeld(wanted, held);
        }

        var added = 0;
        var fetched = new List<string>();
        for (var i = 0; i < wanted.Count; i++)
        {
            var p = wanted[i];
            var label = p.Label ?? (p.Title ?? "").PadRight(18)[..18].Trim();
            List<ScrapedCard> cards;
            try
            {
                var page = await GetHtml(http, $"{cardList}?series={p.Id}");
                cards = ParseCards(page, site.Host);
            }
            catch (Exception e)
            {
                Console.WriteLine($"  [{i + 1}/{wanted.Count}] {label,-14} failed: {e.Message}");
                continue;
            }
            foreach (var c in cards)
            {
                c.SetLabel = p.Label ?? p.Title ?? "";
                c.SetName = p.Title ?? "";
                if (!rows.ContainsKey(c.CardId)) added++;
                rows[c.CardId] = c;
            }
            if (cards.Count > 0) fetched.Add(p.Label ?? p.Title ?? p.Id);
            Console.WriteLine($"  [{i + 1}/{wanted.Count}] {label,-14} {cards.Count,4} cards");
            await Task.Delay(pause);
        }

        if (rows.Count == 0)
        {
            Console.Error.WriteLine($"nothing scraped; {site.FileName} left untouched");
            return null;
        }

        var output = rows.Values.OrderBy(r => r.CardId, StringComparer.Ordinal).ToList();
        // Unique, so a scheduled run and one from the admin page cannot trip over
        // each other's half-written file; the move is what makes it visible.
        var tmp = $"{file}.{Guid.NewGuid():N}.part";
        await using (var stream = File.Create(tmp))
            await System.Text.Json.JsonSerializer.SerializeAsync(stream, output, Json.Options);
        File.Move(tmp, file, overwrite: true);

        var sets = output.Select(r => r.SetLabel).Where(s => s.Length > 0)
                         .Distinct().Order(StringComparer.Ordinal);
        Console.WriteLine($"\nwrote {site.FileName} — {output.Count} printings, {added} new");
        Console.WriteLine("sets: " + string.Join(", ", sets));
        return new Outcome(output.Count, added, fetched);
    }
}
