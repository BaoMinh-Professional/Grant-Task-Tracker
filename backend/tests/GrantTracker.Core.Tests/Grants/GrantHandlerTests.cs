using GrantTracker.Core.Auditing;
using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Time;
using GrantTracker.Core.Grants;
using GrantTracker.Core.Grants.CloseGrant;
using GrantTracker.Core.Grants.GetGrant;
using GrantTracker.Core.Grants.ListGrants;
using GrantTracker.Core.Tasks;
using GrantTracker.Core.Tasks.ListOverdueTasks;
using GrantTracker.Core.Tests.Common;
using GrantTracker.Core.Tests.Common.Time;
using Moq;

namespace GrantTracker.Core.Tests.Grants;

public sealed class GrantHandlerTests
{
    private static readonly FakeBusinessClock Clock = new(TestGrants.Now, TestGrants.Today);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankStatusListsAllGrants(string? status)
    {
        var repository = new Mock<IGrantRepository>();
        repository.Setup(x => x.ListAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await new ListGrantsHandler(repository.Object).Handle(new ListGrantsQuery(status), TestContext.Current.CancellationToken);
        repository.Verify(x => x.ListAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("open", GrantStatus.Open)]
    [InlineData(" CLOSED ", GrantStatus.Closed)]
    public async Task StatusIsMatchedCaseInsensitively(string status, GrantStatus expected)
    {
        var repository = new Mock<IGrantRepository>();
        repository.Setup(x => x.ListAsync(expected, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await new ListGrantsHandler(repository.Object).Handle(new ListGrantsQuery(status), TestContext.Current.CancellationToken);
        repository.Verify(x => x.ListAsync(expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Foo")]
    [InlineData("1")]
    [InlineData("Open1")]
    public async Task UnknownStatusIsAValidationError(string status)
    {
        var repository = new Mock<IGrantRepository>();
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            new ListGrantsHandler(repository.Object).Handle(new ListGrantsQuery(status), TestContext.Current.CancellationToken));
        Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
        Assert.Contains("status", exception.Errors.Keys);
        repository.Verify(x => x.ListAsync(It.IsAny<GrantStatus?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListMapsCanCloseFromTheRule()
    {
        var repository = new Mock<IGrantRepository>();
        var id = Guid.NewGuid();
        repository.Setup(x => x.ListAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new GrantSummary(Guid.NewGuid(), "GR-1", "Org", TestGrants.ApplicationDate, GrantStatus.Open, 1, 0, 0),
            new GrantSummary(Guid.NewGuid(), "GR-2", "Org", TestGrants.ApplicationDate, GrantStatus.Open, 2, 0, 2),
            new GrantSummary(id, "GR-3", "Org", TestGrants.ApplicationDate, GrantStatus.Closed, 0, 0, 0)
        ]);
        var items = await new ListGrantsHandler(repository.Object).Handle(new ListGrantsQuery(null), TestContext.Current.CancellationToken);
        Assert.True(items[0].CanClose);
        Assert.False(items[1].CanClose);
        Assert.False(items[2].CanClose);
    }

    [Fact]
    public async Task GetGrantForUnknownIdIsNotFound()
    {
        var repository = new Mock<IGrantRepository>();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetGrantHandler(repository.Object, Clock).Handle(new GetGrantQuery(Guid.NewGuid()), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetGrantReturnsFreshTodayTasksInDueOrderAndCanClose()
    {
        var grant = TestGrants.Open();
        grant.AddTask("Later", TestGrants.Today.AddDays(5), TaskPriority.Medium);
        grant.AddTask("Verify bank details", TestGrants.Today.AddDays(-3), TaskPriority.High);
        grant.AddTask("Done late", TestGrants.Today.AddDays(-1), TaskPriority.Low, GrantTaskStatus.Completed);
        var repository = new Mock<IGrantRepository>();
        repository.Setup(x => x.FindWithTasksAsync(grant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grant);

        var detail = await new GetGrantHandler(repository.Object, Clock).Handle(new GetGrantQuery(grant.Id), TestContext.Current.CancellationToken);

        Assert.Equal(TestGrants.Today, detail.Today);
        Assert.Equal(GrantStatus.Open, detail.Status);
        Assert.False(detail.CanClose);
        Assert.Equal(1, detail.PendingHighTasks);
        Assert.Equal(["Verify bank details", "Done late", "Later"], detail.Tasks.Select(t => t.Title));
        var overdue = detail.Tasks[0];
        Assert.True(overdue.IsOverdue);
        Assert.Equal(3, overdue.DaysOverdue);
        Assert.False(detail.Tasks[1].IsOverdue);
    }

    [Fact]
    public async Task OverdueTasksMapGrantFieldsAndDaysOverdue()
    {
        var grant = TestGrants.Closed();
        var task = grant.AddTask("Final report", TestGrants.Today.AddDays(-2), TaskPriority.High);
        var tasks = new Mock<ITaskRepository>();
        tasks.Setup(x => x.ListOverdueAsync(TestGrants.Today, It.IsAny<CancellationToken>())).ReturnsAsync([task]);

        var items = await new ListOverdueTasksHandler(tasks.Object, Clock)
            .Handle(new ListOverdueTasksQuery(), TestContext.Current.CancellationToken);

        var item = Assert.Single(items);
        Assert.Equal("GR-2", item.GrantReferenceNumber);
        Assert.Equal("Northside Youth Sports Club", item.OrganisationName);
        Assert.Equal(GrantStatus.Closed, item.GrantStatus);
        Assert.Equal(2, item.DaysOverdue);
    }

    [Fact]
    public async Task CloseUnknownGrantIsNotFound()
    {
        var repository = new Mock<IGrantRepository>();
        var audit = new AuditContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CloseGrantHandler(repository.Object, Clock, audit).Handle(new CloseGrantCommand(Guid.NewGuid()), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CloseIsRefusedWhileHighPriorityTaskIsPending()
    {
        var grant = TestGrants.Open();
        grant.AddTask("Verify bank details", TestGrants.Today.AddDays(5), TaskPriority.High);
        var repository = new Mock<IGrantRepository>();
        repository.Setup(x => x.FindWithTasksAsync(grant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grant);
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new CloseGrantHandler(repository.Object, Clock, new AuditContext()).Handle(new CloseGrantCommand(grant.Id), TestContext.Current.CancellationToken));
        Assert.Equal(ErrorCodes.CloseBlockedByHighPriorityTask, exception.Code);
    }

    [Fact]
    public async Task CloseWithOnlyMediumAndLowTasksPendingClosesTheGrant()
    {
        var grant = TestGrants.Open();
        grant.AddTask("Quote", TestGrants.Today.AddDays(5), TaskPriority.Medium);
        grant.AddTask("Survey", TestGrants.Today.AddDays(6), TaskPriority.Low);
        var repository = new Mock<IGrantRepository>();
        repository.Setup(x => x.FindWithTasksAsync(grant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grant);
        var audit = new AuditContext();

        var result = await new CloseGrantHandler(repository.Object, Clock, audit).Handle(new CloseGrantCommand(grant.Id), TestContext.Current.CancellationToken);

        Assert.Equal(GrantStatus.Closed, result.Status);
        Assert.False(result.CanClose);
        Assert.Equal(AuditOutcome.Changed, audit.Outcome);
    }

    [Fact]
    public async Task RepeatCloseIsNoChange()
    {
        var grant = TestGrants.Closed();
        var repository = new Mock<IGrantRepository>();
        repository.Setup(x => x.FindWithTasksAsync(grant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grant);
        var audit = new AuditContext();

        var result = await new CloseGrantHandler(repository.Object, Clock, audit).Handle(new CloseGrantCommand(grant.Id), TestContext.Current.CancellationToken);

        Assert.Equal(GrantStatus.Closed, result.Status);
        Assert.Equal(AuditOutcome.NoChange, audit.Outcome);
    }
}
