using System.Text;
using System.Text.Json;

namespace Manifest.Tests;

[Collection("server")]
public class AccountTests(ServerFixture server)
{
    static StringContent Body(object payload) => new(
        JsonSerializer.Serialize(payload, Manifest.Json.Options),
        Encoding.UTF8, "application/json");

    /// <summary>
    /// "Tester" and "tester" are one account. SQLite gets this from COLLATE NOCASE;
    /// PostgreSQL only from citext on both sides of the comparison, which is easy to
    /// lose without noticing because uniqueness still holds either way.
    /// </summary>
    [Fact]
    public async Task UsernamesAreCaseInsensitive()
    {
        using var client = server.NewAnonymousClient();
        var shouted = ServerFixture.Username.ToUpperInvariant();

        var login = await client.PostAsync("/api/auth/login",
            Body(new { username = shouted, password = ServerFixture.Password }));
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());

        var again = await client.PostAsync("/api/auth/register",
            Body(new { username = shouted, password = ServerFixture.Password,
                       invite = ServerFixture.InviteCode }));
        Assert.False(again.IsSuccessStatusCode);
        Assert.Contains("taken", await again.Content.ReadAsStringAsync());
    }
}
