using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GrantTracker.Api.Tests.Infrastructure;

namespace GrantTracker.Api.Tests.Tasks;

public sealed class EditTaskEndpointsTests
{
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    private static async Task<HttpResponseMessage> EditAsync(HttpClient client, Guid taskId, string? ifMatch, string key, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/tasks/{taskId}") { Content = JsonContent.Create(body) };
        if (ifMatch is not null) request.Headers.Add("If-Match", ifMatch);
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static readonly object RenamedBody = new { title = "Renamed", dueDate = "2026-03-07", priority = "High" };

    [Fact]
    public async Task EditWithoutIfMatchIsPreconditionRequired()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await EditAsync(client, SeedIds.VerifyBankDetails, null, Guid.NewGuid().ToString(), RenamedBody);
        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        var problem = await ReadAsync(response);
        Assert.Equal("PRECONDITION_REQUIRED", problem.GetProperty("code").GetString());
        Assert.Equal("Send the task's current version in the If-Match header.", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task EditWithStaleVersionIsAVersionConflict()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await EditAsync(client, SeedIds.VerifyBankDetails, "\"5\"", Guid.NewGuid().ToString(), RenamedBody);
        Assert.Equal((HttpStatusCode)412, response.StatusCode);
        var problem = await ReadAsync(response);
        Assert.Equal("VERSION_CONFLICT", problem.GetProperty("code").GetString());
        Assert.Equal(
            "This task was changed by someone else before your edit was saved. Reload to see the latest version, then edit again.",
            problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task EditWithCurrentVersionSavesAndReturnsTheNewVersion()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await EditAsync(client, SeedIds.VerifyBankDetails, "\"1\"", Guid.NewGuid().ToString(), RenamedBody);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"2\"", response.Headers.ETag?.Tag);
        var task = await ReadAsync(response);
        Assert.Equal("Renamed", task.GetProperty("title").GetString());
        Assert.Equal("High", task.GetProperty("priority").GetString());
    }

    [Fact]
    public async Task ReplayOfASuccessfulEditWithTheSameKeyReturnsTheSameResult()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var key = Guid.NewGuid().ToString();
        var first = await EditAsync(client, SeedIds.VerifyBankDetails, "\"1\"", key, RenamedBody);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await EditAsync(client, SeedIds.VerifyBankDetails, "\"1\"", key, RenamedBody);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal((await ReadAsync(first)).GetRawText(), (await ReadAsync(replay)).GetRawText());
    }

    [Fact]
    public async Task EditingACompletedTaskIsTaskCompleted()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        await client.PutAsync($"/tasks/{SeedIds.VerifyBankDetails}/complete", null, TestContext.Current.CancellationToken);

        var response = await EditAsync(client, SeedIds.VerifyBankDetails, "\"2\"", Guid.NewGuid().ToString(), RenamedBody);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("TASK_COMPLETED", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task EditingAPendingTaskOnAClosedGrantIsGrantClosed()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        await client.PutAsync($"/grants/{SeedIds.Grant2}/close", null, TestContext.Current.CancellationToken);

        var response = await EditAsync(client, SeedIds.ReviewProgressReport, "\"1\"", Guid.NewGuid().ToString(),
            new { title = "Renamed", dueDate = "2026-03-09", priority = "Low" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("GRANT_CLOSED", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task EditWithInvalidBodyIsBr3()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await EditAsync(client, SeedIds.VerifyBankDetails, "\"1\"", Guid.NewGuid().ToString(),
            new { title = " ", dueDate = "2026-03-07", priority = "High" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("BR-3", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task EditWithUnreadableIfMatchIsMalformedRequest()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await EditAsync(client, SeedIds.VerifyBankDetails, "\"one\"", Guid.NewGuid().ToString(), RenamedBody);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MALFORMED_REQUEST", (await ReadAsync(response)).GetProperty("code").GetString());
    }
}
