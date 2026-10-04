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
    public string CaPem => Path.Combine(Root, "ca.pem");
    public string CertPem => Path.Combine(Root, "cert.pem");
    public string KeyPem => Path.Combine(Root, "key.pem");
    public string ImgDir => Path.Combine(Root, "img-cache");
    public string DebugScans => Path.Combine(Root, "debug-scans");

    public AppPaths(string? explicitRoot)
    {
        Root = explicitRoot is { Length: > 0 }
            ? Path.GetFullPath(explicitRoot)
            : Discover();
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
