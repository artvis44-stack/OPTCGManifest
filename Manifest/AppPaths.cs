namespace Manifest;

/// <summary>
/// Where the app's data lives. A compiled binary sits in bin/Debug/..., not next to
/// manifest.db, so the root is discovered by walking up until the catalogue is
/// found. --root overrides it.
/// </summary>
public sealed class AppPaths
{
    public string Root { get; }
    public string DbPath => Path.Combine(Root, "manifest.db");
    public string Catalog => Path.Combine(Root, "catalog.json");
    public string CatalogJapanese => Path.Combine(Root, "catalog-jp.json");
    public string CaPem => Path.Combine(Root, "ca.pem");
    public string CertPem => Path.Combine(Root, "cert.pem");
    public string KeyPem => Path.Combine(Root, "key.pem");
    public string ImgDir => Path.Combine(Root, "img-cache");
    public string DebugScans => Path.Combine(Root, "debug-scans");

    /// <summary>
    /// --root, else MANIFEST_ROOT - which is how the container image points every
    /// subcommand at its data volume without each having to be told - else found.
    /// </summary>
    public AppPaths(string? explicitRoot)
    {
        var root = explicitRoot is { Length: > 0 }
            ? explicitRoot
            : Environment.GetEnvironmentVariable("MANIFEST_ROOT");
        Root = root is { Length: > 0 } ? Path.GetFullPath(root) : Discover();
    }

    static string Discover()
    {
        // The build output is Manifest/bin/<cfg>/net8.0/, so the repo root is a
        // few levels up. Check the working directory first: running the binary
        // from the repo root should just work.
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "catalog.json")))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        return Directory.GetCurrentDirectory();
    }
}
