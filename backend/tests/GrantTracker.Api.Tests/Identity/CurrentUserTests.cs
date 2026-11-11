using GrantTracker.Api.Tests.Infrastructure;

namespace GrantTracker.Api.Tests.Identity;

public sealed class CurrentUserTests
{
    [Fact]
    public async Task MiddlewareRequiresKnownSeededUser()
    {
        using var factory = new GrantTrackerApiFactory();
        using var anonymous = factory.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/__test/whoami", TestContext.Current.CancellationToken)).StatusCode);
        using var identified = factory.CreateClient(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var response = await identified.GetAsync("/__test/whoami", TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Alex Nguyen", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
