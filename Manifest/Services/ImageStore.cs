using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Manifest.Services;

/// <summary>
/// Where card pictures are kept once fetched. Keys are deterministic -
/// cards/OP01-016_p1.png - so any container can find what any other stored.
/// </summary>
public interface IImageStore
{
    string Description { get; }
    Task<byte[]?> Get(string key, CancellationToken cancel = default);
    Task Put(string key, byte[] png, CancellationToken cancel = default);
}

/// <summary>
/// img-cache/ beside the database, as it always was: one machine, one disk. Only the
/// file name of the key is used, so a cache filled before keys had a cards/ prefix
/// is still found.
/// </summary>
public sealed class LocalImageStore : IImageStore
{
    readonly string _dir;
    public LocalImageStore(AppPaths paths) => _dir = paths.ImgDir;

    public string Description => $"local disk at {_dir}";

    string PathOf(string key) => Path.Combine(_dir, Path.GetFileName(key));

    public async Task<byte[]?> Get(string key, CancellationToken cancel = default)
    {
        var path = PathOf(key);
        if (!File.Exists(path) || new FileInfo(path).Length == 0) return null;
        try { return await File.ReadAllBytesAsync(path, cancel); }
        catch (FileNotFoundException) { return null; }
    }

    public async Task Put(string key, byte[] png, CancellationToken cancel = default)
    {
        Directory.CreateDirectory(_dir);
        var path = PathOf(key);
        // A unique temporary name, then a rename, so two workers storing the same
        // picture never interleave and a reader never sees half a file.
        var tmp = $"{path}.{Guid.NewGuid():N}.part";
        await File.WriteAllBytesAsync(tmp, png, cancel);
        File.Move(tmp, path, overwrite: true);
    }
}

/// <summary>
/// Any S3-compatible bucket - AWS, Cloudflare R2, Backblaze, MinIO - so every app
/// container shares one set of pictures and none of them needs a disk worth keeping.
/// </summary>
public sealed class S3ImageStore : IImageStore
{
    readonly IAmazonS3 _s3;
    readonly string _bucket;
    readonly string _endpoint;

    public S3ImageStore(AppConfig config)
    {
        _bucket = config.ObjectStorageBucket!;
        _endpoint = config.ObjectStorageEndpoint!;
        var credentials = string.IsNullOrEmpty(config.ObjectStorageAccessKey)
            ? (AWSCredentials)new AnonymousAWSCredentials()
            : new BasicAWSCredentials(config.ObjectStorageAccessKey, config.ObjectStorageSecretKey);
        _s3 = new AmazonS3Client(credentials, new AmazonS3Config
        {
            ServiceURL = config.ObjectStorageEndpoint,
            ForcePathStyle = config.ObjectStoragePathStyle,
            AuthenticationRegion = config.ObjectStorageRegion,
            Timeout = TimeSpan.FromSeconds(20),
            MaxErrorRetry = 2,
        });
    }

    public string Description => $"bucket {_bucket} at {_endpoint}";

    public async Task<byte[]?> Get(string key, CancellationToken cancel = default)
    {
        try
        {
            using var response = await _s3.GetObjectAsync(_bucket, key, cancel);
            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancel);
            return buffer.ToArray();
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task Put(string key, byte[] png, CancellationToken cancel = default)
    {
        using var body = new MemoryStream(png);
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = body,
            ContentType = "image/png",
            // Card art for one printing never changes, so caches anywhere may keep it.
            Headers = { CacheControl = "public, max-age=31536000, immutable" },
        }, cancel);
    }
}
