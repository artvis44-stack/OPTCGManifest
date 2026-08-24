using System.Diagnostics;
using Manifest.Data;

namespace Manifest.Services;

/// <summary>photo -> card number, locally, with tesseract (free, offline).</summary>
public sealed class TesseractScanner
{
    const string Whitelist = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";

    readonly AppPaths _paths;
    readonly Database _db;

    public TesseractScanner(AppPaths paths, Database db)
    {
        _paths = paths;
        _db = db;
    }

    /// <summary>The tesseract binary on PATH, or null if it isn't installed.</summary>
    public static string? Binary { get; } = Which("tesseract");

    public bool Available => Binary is not null;

    static string? Which(string exe)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (dir.Length == 0) continue;
            var candidate = Path.Combine(dir, exe);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public readonly record struct Reading(string? CardId, string Note);

    /// <summary>
    /// variants: base64 PNGs, already cropped and contrast-stretched by the browser.
    /// Tries each in turn and stops at the first number that is a real card.
    /// </summary>
    public Reading Read(IReadOnlyList<string> variants)
    {
        if (Binary is null) return new Reading(null, "no_tesseract");

        var seen = new List<string>();
        string? fallback = null;
        var tmp = Directory.CreateTempSubdirectory("manifest-ocr-").FullName;
        using var conn = _db.Open();

        try
        {
            foreach (var (b64, i) in variants.Take(6).Select((v, i) => (v, i)))
            {
                var path = Path.Combine(tmp, $"v{i}.png");
                try
                {
                    File.WriteAllBytes(path, Convert.FromBase64String(b64));
                }
                catch
                {
                    continue;
                }

                // psm 7 assumes exactly one line, which is all the live-camera guide
                // box is meant to capture. A photo taken freehand (no guide shown)
                // often frames looser and pulls in a line or two of name/type text
                // above the number, which breaks that assumption badly - so also try
                // psm modes that don't assume a single line.
                foreach (var psm in new[] { "7", "11", "6" })
                {
                    var text = RunTesseract(path, psm);
                    if (text is null) continue;
                    if (text.Length > 0) seen.Add(text);

                    foreach (var candidate in CardId.Repairs(text))
                    {
                        var cid = CardId.Normalise(candidate);
                        if (cid is null) continue;
                        if (CardRepository.Resolve(conn, cid) is not null)
                            return new Reading(cid, "");     // in the catalogue: trust it
                        fallback ??= cid;                    // valid shape, unknown card
                    }
                }
            }
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); } catch { /* best effort */ }
        }

        if (fallback is not null)
            return new Reading(fallback, "Not in the catalogue — check the number before logging.");
        if (seen.Count > 0)
            return new Reading(null, $"Read '{Truncate(seen[0], 24)}', which is not a card number. "
                                     + "Line the number up inside the box and hold steady.");
        return new Reading(null, "Nothing legible in the corner. More light, less glare, and "
                                 + "fill the box with just the number.");
    }

    static string Truncate(string s, int n) => s.Length <= n ? s : s[..n];

    static string? RunTesseract(string imagePath, string psm)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Binary!,
                    ArgumentList =
                    {
                        imagePath, "-", "--psm", psm,
                        "-c", "tessedit_char_whitelist=" + Whitelist,
                    },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                },
            };
            proc.Start();
            var stdout = proc.StandardOutput.ReadToEndAsync();
            var stderr = proc.StandardError.ReadToEndAsync();
            if (!proc.WaitForExit(TimeSpan.FromSeconds(25)))
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* already gone */ }
                return null;
            }
            _ = stderr;
            return stdout.GetAwaiter().GetResult().Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Write what the phone sent, for inspecting a scan that failed. Off unless
    /// --verbose: it puts caller-supplied bytes on disk, so it validates the PNG
    /// signature first rather than trusting the .png name it writes.
    /// </summary>
    public void DumpFailedScan(IReadOnlyList<string> variants)
    {
        Directory.CreateDirectory(_paths.DebugScans);
        foreach (var (b64, i) in variants.Take(6).Select((v, i) => (v, i)))
        {
            try
            {
                var blob = Convert.FromBase64String(b64);
                if (blob.Length < 4 || blob[0] != 0x89 || blob[1] != 'P'
                    || blob[2] != 'N' || blob[3] != 'G') continue;
                File.WriteAllBytes(Path.Combine(_paths.DebugScans, $"last_v{i}.png"), blob);
            }
            catch
            {
                // a variant that isn't decodable is not worth failing the scan over
            }
        }
    }
}
