namespace Manifest.Services;

/// <summary>
/// The official card site sends a Cross-Origin-Resource-Policy header, so a browser
/// refuses to render its images inside our page. We fetch them server-side instead
/// and serve them from this origin, cached on disk so each picture is only ever
/// pulled once.
/// </summary>
public sealed class ImageCache
{
    readonly AppPaths _paths;
    readonly Data.Database _db;
    readonly HttpClient _http;
    readonly SemaphoreSlim _writeLock = new(1, 1);

    static readonly byte[] PngMagic = { 0x89, (byte)'P', (byte)'N', (byte)'G' };

    public ImageCache(AppPaths paths, Data.Database db)
    {
        _paths = paths;
        _db = db;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.Add(
            "User-Agent", "Mozilla/5.0 (Manifest, self-hosted collection tracker)");
        _http.DefaultRequestHeaders.Add("Referer", "https://en.onepiece-cardgame.com/cardlist/");
        _http.DefaultRequestHeaders.Add("Accept", "image/png,image/*;q=0.8,*/*;q=0.5");
    }

    public sealed record Result(byte[] Bytes, bool FromCache);

    public async Task<Result?> Get(string rawCardId)
    {
        var cid = CardId.Normalise(rawCardId);
        if (cid is null) return null;

        var safe = CardId.SafeFileStem(cid);          // no path traversal
        if (safe.Length == 0) return null;
        var path = Path.Combine(_paths.ImgDir, safe + ".png");

        if (File.Exists(path) && new FileInfo(path).Length > 0)
            return new Result(await File.ReadAllBytesAsync(path), true);

        string? url;
        using (var conn = _db.Open())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT image_url FROM catalog WHERE card_id = @id";
            cmd.Parameters.AddWithValue("@id", cid);
            url = cmd.ExecuteScalar() as string;
        }
        if (string.IsNullOrEmpty(url)) return null;

        byte[] blob;
        try
        {
            blob = await _http.GetByteArrayAsync(url);
        }
        catch
        {
            return null;
        }
        if (blob.Length < 4 || !blob.Take(4).SequenceEqual(PngMagic)) return null;

        await _writeLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(_paths.ImgDir);
            var tmp = path + ".part";
            await File.WriteAllBytesAsync(tmp, blob);
            File.Move(tmp, path, overwrite: true);     // atomic
        }
        finally
        {
            _writeLock.Release();
        }
        return new Result(blob, false);
    }
}
