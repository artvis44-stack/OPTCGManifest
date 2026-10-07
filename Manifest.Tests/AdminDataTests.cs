using System.Net;
using System.Text;
using System.Text.Json;

namespace Manifest.Tests;

/// <summary>
/// The admin page's card-data panel: the owner sees when new sets and prices were
/// last fetched, and nobody else can see it or start either job. Pressing the
/// buttons is left to JobQueueTests - here it would scrape the real card site.
/// </summary>
[Collection("server")]
public class AdminDataTests(ServerFixture server)
{
    static StringContent Empty() => new("{}", Encoding.UTF8, "application/json");

    [Fact]
    public async Task TheOwnerSeesTheCatalogueAndBothJobs()
    {
        var data = await server.Get("/api/admin/data");
        Assert.True(data.GetProperty("printings").GetInt64() > 1000);
        var jobs = data.GetProperty("jobs");
        Assert.True(jobs.TryGetProperty("scrape-catalog", out _));
        Assert.True(jobs.TryGetProperty("refresh-prices", out _));
        // The suite turns the schedules off, so neither claims to run on its own.
        Assert.Equal(JsonValueKind.Null, jobs.GetProperty("scrape-catalog").GetProperty("every_hours").ValueKind);
    }

    [Fact]
    public async Task SomeoneElseCanNeitherSeeNorStartThem()
    {
        var other = await server.NewClientFor($"crew{Guid.NewGuid():N}"[..20], "correct-horse-battery");
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/admin/data")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
                     (await other.PostAsync("/api/admin/data/scrape-catalog", Empty())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
                     (await other.PostAsync("/api/admin/data/refresh-prices", Empty())).StatusCode);
    }

    [Fact]
    public async Task AJobThePageDoesNotOfferIsNotFound()
    {
        var response = await server.Client.PostAsync("/api/admin/data/purge", Empty());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheOwnerIsOfferedTheAdminPage()
    {
        var session = await server.Get("/api/session");
        Assert.True(session.GetProperty("user").GetProperty("owner").GetBoolean());
        var (status, body) = await server.Raw("/admin");
        Assert.Equal(200, status);
        Assert.Contains("Card data", body);
    }
}
