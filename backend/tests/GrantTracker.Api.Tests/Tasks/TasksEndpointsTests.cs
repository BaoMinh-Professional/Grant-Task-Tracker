using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GrantTracker.Api.Tests.Infrastructure;

namespace GrantTracker.Api.Tests.Tasks;

public sealed class TasksEndpointsTests
{
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    [Fact]
    public async Task CompleteReturnsCompletedTaskWithEtag()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await client.PutAsync($"/tasks/{SeedIds.VerifyBankDetails}/complete", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"2\"", response.Headers.ETag?.Tag);
        var task = await ReadAsync(response);
        Assert.Equal("Completed", task.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, task.GetProperty("completedAtUtc").ValueKind);
    }

    [Fact]
    public async Task RepeatCompleteKeepsTheOriginalCompletionTime()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var first = await ReadAsync(await client.PutAsync($"/tasks/{SeedIds.VerifyBankDetails}/complete", null, TestContext.Current.CancellationToken));
        factory.Clock.UtcNow = factory.Clock.UtcNow.AddHours(1);
        var repeat = await client.PutAsync($"/tasks/{SeedIds.VerifyBankDetails}/complete", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        var second = await ReadAsync(repeat);
        Assert.Equal(first.GetProperty("completedAtUtc").GetString(), second.GetProperty("completedAtUtc").GetString());
    }

    [Fact]
    public async Task CompleteUnknownTaskIsNotFound()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await client.PutAsync($"/tasks/{Guid.NewGuid()}/complete", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("NOT_FOUND", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CompletingOnAClosedGrantFollowsBr4AndGrantClosed()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var alreadyDone = await client.PutAsync($"/tasks/{SeedIds.FinalAcquittal}/complete", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, alreadyDone.StatusCode);

        var close = await client.PutAsync($"/grants/{SeedIds.Grant2}/close", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        var pending = await client.PutAsync($"/tasks/{SeedIds.ReviewProgressReport}/complete", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, pending.StatusCode);
        Assert.Equal("GRANT_CLOSED", (await ReadAsync(pending)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task OverdueListIsOrderedWithGrantFields()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await client.GetAsync("/tasks/overdue", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await ReadAsync(response)).EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("Verify bank details", items[0].GetProperty("title").GetString());
        Assert.Equal(3, items[0].GetProperty("daysOverdue").GetInt32());
        Assert.Equal("Review progress report", items[1].GetProperty("title").GetString());
        Assert.Equal(1, items[1].GetProperty("daysOverdue").GetInt32());
        Assert.Equal("GR-100001", items[0].GetProperty("grantReferenceNumber").GetString());
        Assert.Equal("Riverside Community Garden Inc.", items[0].GetProperty("organisationName").GetString());
        Assert.Equal("Open", items[0].GetProperty("grantStatus").GetString());
    }
}
