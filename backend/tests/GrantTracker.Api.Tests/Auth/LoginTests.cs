using System.Net;
using System.Net.Http.Json;
using GrantTracker.Api.Tests.Infrastructure;

namespace GrantTracker.Api.Tests.Auth;

public sealed class LoginTests
{
    [Fact]
    public async Task LoginReturnsSeededUserAndRejectsUnknownEmail()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new { email = "ALEX.Nguyen@Example.org " }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("00000000-0000-0000-0000-000000000001", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var unknown = await client.PostAsJsonAsync("/auth/login", new { email = "nobody@example.org" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Contains("UNKNOWN_USER", await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
