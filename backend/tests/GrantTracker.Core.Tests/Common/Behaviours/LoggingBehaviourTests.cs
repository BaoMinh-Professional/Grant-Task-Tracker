using GrantTracker.Core.Common.Behaviours;
using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Identity;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Idempotency;
using Microsoft.Extensions.Logging;
using Moq;

namespace GrantTracker.Core.Tests.Common.Behaviours;

public sealed class LoggingBehaviourTests
{
    private sealed record Result(string Id);
    private sealed record Query : IQuery<Result>;
    private sealed record PlainCommand : ICommand<Result>;
    private sealed record KeyedCommand(string? IdempotencyKey) : ICommand<Result>, IIdempotentCommand;

    private sealed class Harness<TRequest> where TRequest : IRequest<Result>
    {
        public Harness(Guid? userId)
        {
            var currentUser = new Mock<ICurrentUser>();
            currentUser.SetupGet(x => x.UserId).Returns(userId);
            Behaviour = new LoggingBehaviour<TRequest, Result>(Logger, currentUser.Object);
        }

        public CapturingLogger<LoggingBehaviour<TRequest, Result>> Logger { get; } = new();
        public LoggingBehaviour<TRequest, Result> Behaviour { get; }
        public int NextCalls { get; private set; }

        public Task<Result> Succeed()
        {
            NextCalls++;
            return Task.FromResult(new Result("ok"));
        }

        public Task<Result> Throw(Exception exception)
        {
            NextCalls++;
            throw exception;
        }

        public Task<Result> Run(TRequest request, RequestHandlerDelegate<Result> next) =>
            Behaviour.Handle(request, next, TestContext.Current.CancellationToken);

        public Dictionary<string, object?> Scope() =>
            Assert.IsType<Dictionary<string, object?>>(Assert.Single(Logger.Scopes).State);
    }

    [Fact]
    public async Task ReturnsTheNextResultAndLogsHandledAtInformation()
    {
        var harness = new Harness<Query>(Guid.NewGuid());

        var result = await harness.Run(new Query(), harness.Succeed);

        Assert.Equal(new Result("ok"), result);
        var entry = Assert.Single(harness.Logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Matches(@"^Handled Query in \d+ ms$", entry.Message);
    }

    [Fact]
    public async Task ScopeCarriesTheRequestNameUserAndIdempotencyKey()
    {
        var userId = Guid.NewGuid();
        var harness = new Harness<KeyedCommand>(userId);

        await harness.Run(new KeyedCommand("key-1"), harness.Succeed);

        var scope = harness.Scope();
        Assert.Equal("KeyedCommand", scope["Request"]);
        Assert.Equal(userId, scope["UserId"]);
        Assert.Equal("key-1", scope["IdempotencyKey"]);
    }

    [Fact]
    public async Task ScopeIdempotencyKeyIsNullForRequestsWithoutAKey()
    {
        var harness = new Harness<PlainCommand>(Guid.NewGuid());

        await harness.Run(new PlainCommand(), harness.Succeed);

        Assert.Null(harness.Scope()["IdempotencyKey"]);
    }

    [Fact]
    public async Task WorksWithoutAnIdentifiedUser()
    {
        var harness = new Harness<Query>(null);

        var result = await harness.Run(new Query(), harness.Succeed);

        Assert.Equal(new Result("ok"), result);
        Assert.Null(harness.Scope()["UserId"]);
    }

    [Fact]
    public async Task AppExceptionIsLoggedAsAWarningWithTheErrorCodeAndRethrown()
    {
        var harness = new Harness<PlainCommand>(Guid.NewGuid());
        var failure = new NotFoundException("Task not found.");

        var thrown = await Assert.ThrowsAsync<NotFoundException>(() =>
            harness.Run(new PlainCommand(), () => harness.Throw(failure)));

        Assert.Same(failure, thrown);
        var entry = Assert.Single(harness.Logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Rejected PlainCommand with NOT_FOUND", entry.Message);
    }

    [Fact]
    public async Task UnexpectedExceptionIsRethrownWithoutALogLine()
    {
        // The API's exception handler owns logging for unhandled errors.
        var harness = new Harness<PlainCommand>(Guid.NewGuid());
        var failure = new InvalidOperationException("boom");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Run(new PlainCommand(), () => harness.Throw(failure)));

        Assert.Same(failure, thrown);
        Assert.Empty(harness.Logger.Entries);
    }

    [Fact]
    public async Task ScopeIsDisposedAfterASuccessfulCall()
    {
        var harness = new Harness<Query>(Guid.NewGuid());

        await harness.Run(new Query(), harness.Succeed);

        Assert.True(Assert.Single(harness.Logger.Scopes).Disposed);
    }

    [Fact]
    public async Task ScopeIsDisposedAfterAFailedCall()
    {
        var harness = new Harness<PlainCommand>(Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            harness.Run(new PlainCommand(), () => harness.Throw(new NotFoundException("Not found."))));

        Assert.True(Assert.Single(harness.Logger.Scopes).Disposed);
    }

    [Fact]
    public async Task NextIsInvokedExactlyOnce()
    {
        var harness = new Harness<Query>(Guid.NewGuid());

        await harness.Run(new Query(), harness.Succeed);

        Assert.Equal(1, harness.NextCalls);
    }
}
