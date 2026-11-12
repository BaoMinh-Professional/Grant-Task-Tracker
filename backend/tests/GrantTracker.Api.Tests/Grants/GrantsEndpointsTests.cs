using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GrantTracker.Api.Tests.Infrastructure;
using GrantTracker.Core.Auditing;
using GrantTracker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrantTracker.Api.Tests.Grants;

public sealed class GrantsEndpointsTests
{
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    private static async Task<HttpResponseMessage> AddTaskAsync(HttpClient client, Guid grantId, string key, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/grants/{grantId}/tasks") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ListAllReturnsSeededGrantsInOrderWithCounts()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await client.GetAsync("/grants", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await ReadAsync(response)).EnumerateArray().ToList();
        Assert.Equal(4, items.Count);
        Assert.Equal("GR-100001", items[0].GetProperty("referenceNumber").GetString());
        Assert.Equal("GR-100003", items[3].GetProperty("referenceNumber").GetString());
        Assert.Equal("Closed", items[3].GetProperty("status").GetString());
        Assert.Equal(3, items[0].GetProperty("totalTasks").GetInt32());
        Assert.Equal(1, items[0].GetProperty("completedTasks").GetInt32());
        Assert.Equal(1, items[0].GetProperty("pendingHighTasks").GetInt32());
        Assert.False(items[0].GetProperty("canClose").GetBoolean());
    }

    [Fact]
    public async Task ListFilteredByStatusIsCaseInsensitive()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await client.GetAsync("/grants?status=open", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await ReadAsync(response)).EnumerateArray().ToList();
        Assert.Equal(3, items.Count);
        Assert.All(items, item => Assert.Equal("Open", item.GetProperty("status").GetString()));
    }

    [Theory]
    [InlineData("Foo")]
    [InlineData("1")]
    public async Task ListWithInvalidStatusIsAValidationError(string status)
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await client.GetAsync($"/grants?status={status}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadAsync(response);
        Assert.Equal("VALIDATION_FAILED", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("status", out _));
    }

    [Fact]
    public async Task DetailReturnsTodayTasksAndOverdueFlags()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await client.GetAsync($"/grants/{SeedIds.Grant1}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await ReadAsync(response);
        Assert.Equal("2026-03-10", detail.GetProperty("today").GetString());
        Assert.Equal("2026-01-09", detail.GetProperty("applicationDate").GetString());
        Assert.False(detail.GetProperty("canClose").GetBoolean());
        var tasks = detail.GetProperty("tasks").EnumerateArray().ToList();
        var verify = tasks.Single(t => t.GetProperty("title").GetString() == "Verify bank details");
        Assert.True(verify.GetProperty("isOverdue").GetBoolean());
        Assert.Equal(3, verify.GetProperty("daysOverdue").GetInt32());
        Assert.Equal(1, verify.GetProperty("version").GetInt32());
        var completed = tasks.Single(t => t.GetProperty("status").GetString() == "Completed");
        Assert.False(completed.GetProperty("isOverdue").GetBoolean());
    }

    [Fact]
    public async Task DetailUsesTheServerBusinessDate()
    {
        using var factory = new GrantTrackerApiFactory();
        factory.Clock.Today = new DateOnly(2026, 3, 11);
        using var client = factory.CreateClient(SeedIds.Alex);
        var detail = await ReadAsync(await client.GetAsync($"/grants/{SeedIds.Grant1}", TestContext.Current.CancellationToken));
        Assert.Equal("2026-03-11", detail.GetProperty("today").GetString());
    }

    [Fact]
    public async Task DetailForUnknownOrMalformedIdIsNotFound()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        foreach (var path in new[] { $"/grants/{Guid.NewGuid()}", "/grants/not-a-guid" })
        {
            var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("NOT_FOUND", (await ReadAsync(response)).GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task RequestWithoutUserIsUnauthorised()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/grants", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("USER_REQUIRED", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task AddTaskCreatesPendingTaskWithEtagAndIsVisibleInDetail()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await AddTaskAsync(client, SeedIds.Grant4, Guid.NewGuid().ToString(),
            new { title = "Call applicant", dueDate = "2026-03-10", priority = "Low" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("\"1\"", response.Headers.ETag?.Tag);
        var task = await ReadAsync(response);
        Assert.Equal("Pending", task.GetProperty("status").GetString());
        Assert.False(task.GetProperty("isOverdue").GetBoolean());

        var detail = await ReadAsync(await client.GetAsync($"/grants/{SeedIds.Grant4}", TestContext.Current.CancellationToken));
        Assert.Single(detail.GetProperty("tasks").EnumerateArray());
    }

    [Fact]
    public async Task ReplayWithSameKeyReturnsTheSameTaskOnce()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var key = Guid.NewGuid().ToString();
        var body = new { title = "Call applicant", dueDate = "2026-03-10", priority = "Low" };
        var first = await ReadAsync(await AddTaskAsync(client, SeedIds.Grant4, key, body));
        var replay = await AddTaskAsync(client, SeedIds.Grant4, key, body);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(first.GetProperty("id").GetGuid(), (await ReadAsync(replay)).GetProperty("id").GetGuid());

        var detail = await ReadAsync(await client.GetAsync($"/grants/{SeedIds.Grant4}", TestContext.Current.CancellationToken));
        Assert.Single(detail.GetProperty("tasks").EnumerateArray());
    }

    [Fact]
    public async Task ReusingKeyWithDifferentBodyIsAMismatch()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var key = Guid.NewGuid().ToString();
        await AddTaskAsync(client, SeedIds.Grant4, key, new { title = "Call applicant", dueDate = "2026-03-10", priority = "Low" });
        var response = await AddTaskAsync(client, SeedIds.Grant4, key, new { title = "Different", dueDate = "2026-03-10", priority = "Low" });
        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_MISMATCH", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task AddWithoutKeyIsRejected()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await client.PostAsJsonAsync($"/grants/{SeedIds.Grant4}/tasks",
            new { title = "Call applicant", dueDate = "2026-03-10", priority = "Low" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REQUIRED", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task BlankAndTooLongTitlesAreBr3()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        foreach (var title in new[] { "", new string('a', 101) })
        {
            var response = await AddTaskAsync(client, SeedIds.Grant4, Guid.NewGuid().ToString(),
                new { title, dueDate = "2026-03-10", priority = "Low" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await ReadAsync(response);
            Assert.Equal("BR-3", problem.GetProperty("code").GetString());
            Assert.True(problem.GetProperty("errors").TryGetProperty("title", out _));
        }
    }

    [Fact]
    public async Task DueDateInThePastOrBeforeApplicationIsBr2AndTodayIsAccepted()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var past = await AddTaskAsync(client, SeedIds.Grant1, Guid.NewGuid().ToString(),
            new { title = "Late", dueDate = "2026-03-09", priority = "Low" });
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        var pastProblem = await ReadAsync(past);
        Assert.Equal("BR-2", pastProblem.GetProperty("code").GetString());
        Assert.True(pastProblem.GetProperty("errors").TryGetProperty("dueDate", out _));

        var beforeApplication = await AddTaskAsync(client, SeedIds.Grant4, Guid.NewGuid().ToString(),
            new { title = "Early", dueDate = "2026-03-04", priority = "Low" });
        Assert.Equal("BR-2", (await ReadAsync(beforeApplication)).GetProperty("code").GetString());

        var today = await AddTaskAsync(client, SeedIds.Grant4, Guid.NewGuid().ToString(),
            new { title = "Today", dueDate = "2026-03-10", priority = "Low" });
        Assert.Equal(HttpStatusCode.Created, today.StatusCode);
    }

    [Fact]
    public async Task MixedErrorsAreReportedTogether()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await AddTaskAsync(client, SeedIds.Grant1, Guid.NewGuid().ToString(),
            new { title = " ", dueDate = "2026-03-09", priority = "Low" });
        var problem = await ReadAsync(response);
        Assert.Equal("VALIDATION_FAILED", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("title", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("dueDate", out _));
    }

    [Fact]
    public async Task AddToClosedGrantIsBr1ButInputErrorsComeFirst()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var closed = await AddTaskAsync(client, SeedIds.Grant3, Guid.NewGuid().ToString(),
            new { title = "Valid", dueDate = "2026-03-10", priority = "Low" });
        Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
        Assert.Equal("BR-1", (await ReadAsync(closed)).GetProperty("code").GetString());

        var blank = await AddTaskAsync(client, SeedIds.Grant3, Guid.NewGuid().ToString(),
            new { title = "", dueDate = "2026-03-10", priority = "Low" });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal("BR-3", (await ReadAsync(blank)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("""{ "title": "x", "dueDate": "2026-03-10", "priority": "Urgent" }""")]
    [InlineData("""{ "title": "x", "dueDate": "tomorrow", "priority": "Low" }""")]
    public async Task MalformedBodyIsMalformedRequest(string json)
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/grants/{SeedIds.Grant4}/tasks")
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MALFORMED_REQUEST", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ClosingWithPendingHighTaskIsBr5()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var response = await client.PutAsync($"/grants/{SeedIds.Grant1}/close", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("BR-5", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ClosingSucceedsWithoutPendingHighTasksAndRepeatsReturnTheSameGrant()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var first = await client.PutAsync($"/grants/{SeedIds.Grant4}/close", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await ReadAsync(first);
        Assert.Equal("Closed", firstBody.GetProperty("status").GetString());
        Assert.False(firstBody.GetProperty("canClose").GetBoolean());

        var repeat = await client.PutAsync($"/grants/{SeedIds.Grant4}/close", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Equal(firstBody.GetRawText(), (await ReadAsync(repeat)).GetRawText());

        var grant2 = await client.PutAsync($"/grants/{SeedIds.Grant2}/close", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, grant2.StatusCode);
    }

    [Fact]
    public async Task ClosingAfterCompletingTheHighTaskSucceeds()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        var complete = await client.PutAsync($"/tasks/{SeedIds.VerifyBankDetails}/complete", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var close = await client.PutAsync($"/grants/{SeedIds.Grant1}/close", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
    }

    [Fact]
    public async Task AuditRecordsChangedAndRejectedOutcomes()
    {
        using var factory = new GrantTrackerApiFactory();
        using var client = factory.CreateClient(SeedIds.Alex);
        await AddTaskAsync(client, SeedIds.Grant4, Guid.NewGuid().ToString(),
            new { title = "Call applicant", dueDate = "2026-03-10", priority = "Low" });
        await client.PutAsync($"/grants/{SeedIds.Grant1}/close", null, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GrantTrackerDbContext>();
        var entries = await db.Audits.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(entries, e => e.Action == "AddTask" && e.Outcome == AuditOutcome.Changed);
        Assert.Contains(entries, e => e.Action == "CloseGrant" && e.Outcome == AuditOutcome.Rejected && e.ErrorCode == "BR-5");
    }
}
