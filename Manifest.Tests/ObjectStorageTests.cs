using Manifest.Data;
using Manifest.Services;

namespace Manifest.Tests;

/// <summary>
/// Card art in an S3-compatible bucket. Needs MANIFEST_TEST_S3 - an endpoint with a
/// bucket called manifest-test, e.g. http://127.0.0.1:58333 - and skips without it.
/// </summary>
public sealed class ObjectStorageTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("manifest-s3-").FullName;

    static string? Endpoint =>
        Environment.GetEnvironmentVariable("MANIFEST_TEST_S3") is { Length: > 0 } url ? url : null;

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    static async Task EnsureBucket()
    {
        using var s3 = new Amazon.S3.AmazonS3Client(new Amazon.Runtime.BasicAWSCredentials("test", "test"),
            new Amazon.S3.AmazonS3Config { ServiceURL = Endpoint, ForcePathStyle = true });
        try { await s3.PutBucketAsync("manifest-test"); }
        catch (Amazon.S3.AmazonS3Exception e) when (e.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists") { }
    }

    static AppConfig Config() => new()
    {
        ObjectStorageEndpoint = Endpoint,
        ObjectStorageBucket = "manifest-test",
        ObjectStorageAccessKey = "test",
        ObjectStorageSecretKey = "test",
    };

    [SkippableFact]
    public async Task StoresAndReadsBack()
    {
        Skip.If(Endpoint is null, "MANIFEST_TEST_S3 is not set");
        await EnsureBucket();
        var store = new S3ImageStore(Config());
        var key = $"cards/test-{Guid.NewGuid():N}.png";

        Assert.Null(await store.Get(key));
        await store.Put(key, FakeUpstream.Png);
        Assert.Equal(FakeUpstream.Png, await store.Get(key));
    }

    /// <summary>The whole pipeline, with the bucket in place of img-cache/.</summary>
    [SkippableFact]
    public async Task AFetchedPictureLandsInTheBucket()
    {
        Skip.If(Endpoint is null, "MANIFEST_TEST_S3 is not set");
        await EnsureBucket();
        File.Copy(Path.Combine(ServerFixture.RepoRoot, "catalog.json"), Path.Combine(_root, "catalog.json"));
        var db = new Database(new AppPaths(_root), "sqlite://manifest.db");
        Assert.Null(db.Initialise(false));

        using var upstream = new FakeUpstream();
        var card = "OP01-0" + Random.Shared.Next(10, 99);
        using (var conn = db.Open())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE catalog SET image_url = @u WHERE card_id = @id";
            cmd.Bind("@u", upstream.Base + "ok/x.png");
            cmd.Bind("@id", card);
            cmd.ExecuteNonQuery();
        }

        var store = new S3ImageStore(Config());
        var queue = new JobQueue(db, SystemClock.Instance);
        var images = new ImageCache(store, db, queue, SystemClock.Instance);

        // A bucket shared between runs may already hold it; the job is idempotent.
        var first = await images.Get(card);
        if (first.State == ImageCache.State.Pending)
            await images.Run(queue.Claim()!, CancellationToken.None);

        var got = await images.Get(card);
        Assert.Equal(ImageCache.State.Found, got.State);
        Assert.Equal(FakeUpstream.Png, got.Bytes);
        Assert.Equal(FakeUpstream.Png, await store.Get(ImageCache.KeyFor(card)));
        Assert.False(Directory.Exists(Path.Combine(_root, "img-cache")));   // nothing on local disk
    }
}
