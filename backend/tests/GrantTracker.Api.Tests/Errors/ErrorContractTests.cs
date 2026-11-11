using System.Net;
using GrantTracker.Api.Tests.Infrastructure;

namespace GrantTracker.Api.Tests.Errors;

public sealed class ErrorContractTests
{
    [Fact]
    public async Task UnknownRouteIsProblemDetails()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/nope", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("NOT_FOUND", content);
        Assert.Contains("traceId", content);
    }
}
