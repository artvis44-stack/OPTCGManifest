using System.Runtime.CompilerServices;

namespace Manifest.Tests;

static class NoOutboundJobs
{
    /// <summary>
    /// New sets and prices are fetched daily by default, starting the moment a server
    /// comes up. Every server the suite starts inherits this process's environment, so
    /// turning them off here keeps the suite from scraping real sites and reseeding
    /// the catalogue underneath a test.
    /// </summary>
    [ModuleInitializer]
    internal static void TurnOff()
    {
        Environment.SetEnvironmentVariable("MANIFEST_SCRAPE_CATALOG_HOURS", "off");
        Environment.SetEnvironmentVariable("MANIFEST_REFRESH_PRICES_HOURS", "off");
    }
}
