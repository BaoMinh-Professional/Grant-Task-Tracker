using System.Diagnostics;
using GrantTracker.Core.Auditing;
using GrantTracker.Core.Common.Behaviours;
using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Identity;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Common.Persistence;
using GrantTracker.Core.Idempotency;
using GrantTracker.Core.Tests.Common.Time;
using Microsoft.Extensions.Logging;
using Moq;

namespace GrantTracker.Core.Tests.Common.Behaviours;

public sealed class AuditBehaviourTests
{
    private sealed record Result(string Id);
    private sealed record Query : IQuery<Result>;
    private sealed record AddThingCommand : ICommand<Result>;
    private sealed record Thing : ICommand<Result>;
    private sealed record KeyedCommand(string? IdempotencyKey) : ICommand<Result>, IIdempotentCommand;

    private static readonly FakeBusinessClock Clock = new(TestGrants.Now, TestGrants.Today);

    // Wires the behaviour to mocks that record the order of every call into Calls.
    private sealed class Harness<TRequest> where TRequest : IRequest<Result>
    {
        private int saveCalls;

        public Harness()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(() => UserId);
            UnitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Callback(() => Calls.Add("begin"))
                .ReturnsAsync(Transaction.Object);
            UnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    Calls.Add("save");
                    var failure = SaveFailure(++saveCalls);
                    return failure is null ? Task.CompletedTask : Task.FromException(failure);
                });
            UnitOfWork.Setup(x => x.DiscardChanges()).Callback(() => Calls.Add("discard"));
            Transaction.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
                .Callback(() => Calls.Add("commit"))
                .Returns(Task.CompletedTask);
            Transaction.Setup(x => x.DisposeAsync()).Callback(() => Calls.Add("dispose")).Returns(ValueTask.CompletedTask);
            AuditLog.Setup(x => x.Add(It.IsAny<AuditEntry>())).Callback<AuditEntry>(entry =>
            {
                Calls.Add("audit");
                Entries.Add(entry);
            });
        }

        public Guid? UserId { get; init; } = Guid.NewGuid();
        // Receives the 1-based number of the SaveChangesAsync call and returns the exception it should throw, if any.
        public Func<int, Exception?> SaveFailure { get; init; } = _ => null;

        public List<string> Calls { get; } = [];
        public List<AuditEntry> Entries { get; } = [];
        public AuditContext Context { get; } = new();
        public CapturingLogger<AuditBehaviour<TRequest, Result>> Logger { get; } = new();
        public Mock<ICurrentUser> CurrentUser { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IUnitOfWorkTransaction> Transaction { get; } = new();
        public Mock<IAuditLog> AuditLog { get; } = new();

        public Task<Result> Succeed()
        {
            Calls.Add("handler");
            return Task.FromResult(new Result("ok"));
        }

        public Task<Result> Throw(Exception exception)
        {
            Calls.Add("handler");
            throw exception;
        }

        public Task<Result> Run(TRequest request, RequestHandlerDelegate<Result>? next = null) =>
            new AuditBehaviour<TRequest, Result>(CurrentUser.Object, UnitOfWork.Object, AuditLog.Object, Context, Clock, Logger)
                .Handle(request, next ?? Succeed, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task QueriesPassThroughWithoutTransactionOrAudit()
    {
        var harness = new Harness<Query> { UserId = null };

        var result = await harness.Run(new Query());

        Assert.Equal(new Result("ok"), result);
        Assert.Equal(["handler"], harness.Calls);
        harness.AuditLog.Verify(x => x.Add(It.IsAny<AuditEntry>()), Times.Never);
    }

    [Fact]
    public async Task SuccessfulCommandCommitsOnceWithTheAuditRow()
    {
        var harness = new Harness<AddThingCommand>();
        var targetId = Guid.NewGuid();

        var result = await harness.Run(new AddThingCommand(), () =>
        {
            harness.Context.SetTarget("Task", targetId);
            return harness.Succeed();
        });

        Assert.Equal(new Result("ok"), result);
        Assert.Equal(["begin", "handler", "audit", "save", "commit", "dispose"], harness.Calls);
        var entry = Assert.Single(harness.Entries);
        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(harness.UserId, entry.UserId);
        Assert.Equal("AddThing", entry.Action);
        Assert.Equal("Task", entry.EntityType);
        Assert.Equal(targetId, entry.EntityId);
        Assert.Equal(AuditOutcome.Changed, entry.Outcome);
        Assert.Null(entry.ErrorCode);
        Assert.Equal(TestGrants.Now, entry.OccurredAtUtc);
    }

    [Theory]
    [InlineData(AuditOutcome.NoChange)]
    [InlineData(AuditOutcome.Replayed)]
    public async Task OutcomeSetByTheHandlerOrAnEarlierLayerFlowsIntoTheEntry(AuditOutcome outcome)
    {
        var harness = new Harness<AddThingCommand>();

        await harness.Run(new AddThingCommand(), () =>
        {
            harness.Context.SetOutcome(outcome);
            return harness.Succeed();
        });

        Assert.Equal(outcome, Assert.Single(harness.Entries).Outcome);
    }

    [Fact]
    public async Task ActionNameDropsTheCommandSuffix()
    {
        var harness = new Harness<AddThingCommand>();
        await harness.Run(new AddThingCommand());
        Assert.Equal("AddThing", Assert.Single(harness.Entries).Action);
    }

    [Fact]
    public async Task ActionNameIsKeptWhenThereIsNoCommandSuffix()
    {
        var harness = new Harness<Thing>();
        await harness.Run(new Thing());
        Assert.Equal("Thing", Assert.Single(harness.Entries).Action);
    }

    [Theory]
    [InlineData("  key-1  ", "key-1")]
    [InlineData(null, null)]
    public async Task IdempotencyKeyIsTrimmedIntoTheEntry(string? key, string? expected)
    {
        var harness = new Harness<KeyedCommand>();
        await harness.Run(new KeyedCommand(key));
        Assert.Equal(expected, Assert.Single(harness.Entries).IdempotencyKey);
    }

    [Fact]
    public async Task CommandsWithoutAKeyHaveNoIdempotencyKeyInTheEntry()
    {
        var harness = new Harness<AddThingCommand>();
        await harness.Run(new AddThingCommand());
        Assert.Null(Assert.Single(harness.Entries).IdempotencyKey);
    }

    [Fact]
    public async Task TraceIdComesFromTheCurrentActivity()
    {
        using var source = new ActivitySource("GrantTracker.Core.Tests.AuditBehaviour");
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = source.StartActivity("test");
        Assert.NotNull(activity);
        var harness = new Harness<AddThingCommand>();

        await harness.Run(new AddThingCommand());

        Assert.Equal(activity.TraceId.ToHexString(), Assert.Single(harness.Entries).TraceId);
    }

    [Fact]
    public async Task TraceIdIsNullWithoutAnActivity()
    {
        Activity.Current = null;
        var harness = new Harness<AddThingCommand>();

        await harness.Run(new AddThingCommand());

        Assert.Null(Assert.Single(harness.Entries).TraceId);
    }

    [Fact]
    public async Task CommandWithoutAnIdentifiedUserThrowsBeforeStartingATransaction()
    {
        var harness = new Harness<AddThingCommand> { UserId = null };

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Run(new AddThingCommand()));

        Assert.Empty(harness.Calls);
        harness.AuditLog.Verify(x => x.Add(It.IsAny<AuditEntry>()), Times.Never);
    }

    [Fact]
    public async Task AppExceptionRollsBackThenWritesARejectedEntryAndRethrows()
    {
        var harness = new Harness<AddThingCommand>();
        var targetId = Guid.NewGuid();
        var failure = new ConflictException(ErrorCodes.CloseBlockedByHighPriorityTask, "Blocked.");

        var thrown = await Assert.ThrowsAsync<ConflictException>(() => harness.Run(new AddThingCommand(), () =>
        {
            harness.Context.SetTarget("Grant", targetId);
            return harness.Throw(failure);
        }));

        Assert.Same(failure, thrown);
        // The transaction is disposed without a commit (a rollback) before the rejection is written.
        Assert.Equal(["begin", "handler", "dispose", "discard", "audit", "save"], harness.Calls);
        harness.Transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        var entry = Assert.Single(harness.Entries);
        Assert.Equal(AuditOutcome.Rejected, entry.Outcome);
        Assert.Equal("BR-5", entry.ErrorCode);
        Assert.Equal("Grant", entry.EntityType);
        Assert.Equal(targetId, entry.EntityId);
        Assert.Equal("AddThing", entry.Action);
    }

    [Fact]
    public async Task ChangesAreDiscardedBeforeTheRejectionEntryIsAdded()
    {
        // Discarding after Add would clear the tracked audit row and silently lose the rejection.
        var harness = new Harness<AddThingCommand>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            harness.Run(new AddThingCommand(), () => harness.Throw(new NotFoundException("Not found."))));

        Assert.True(harness.Calls.IndexOf("discard") < harness.Calls.IndexOf("audit"));
        Assert.True(harness.Calls.IndexOf("audit") < harness.Calls.LastIndexOf("save"));
    }

    [Fact]
    public async Task FailureWritingTheRejectionEntryIsLoggedAndTheOriginalExceptionStillPropagates()
    {
        var saveFailure = new InvalidOperationException("db down");
        var harness = new Harness<AddThingCommand> { SaveFailure = call => call == 1 ? saveFailure : null };
        var failure = new NotFoundException("Not found.");

        var thrown = await Assert.ThrowsAsync<NotFoundException>(() =>
            harness.Run(new AddThingCommand(), () => harness.Throw(failure)));

        Assert.Same(failure, thrown);
        var logged = Assert.Single(harness.Logger.Entries);
        Assert.Equal(LogLevel.Error, logged.Level);
        Assert.Same(saveFailure, logged.Exception);
    }

    [Fact]
    public async Task UnexpectedExceptionIsNotAuditedAndIsRethrown()
    {
        var harness = new Harness<AddThingCommand>();
        var failure = new InvalidOperationException("boom");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Run(new AddThingCommand(), () => harness.Throw(failure)));

        Assert.Same(failure, thrown);
        Assert.Equal(["begin", "handler", "dispose"], harness.Calls);
        harness.AuditLog.Verify(x => x.Add(It.IsAny<AuditEntry>()), Times.Never);
        harness.Transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AVersionConflictWhileSavingIsTreatedLikeARejection()
    {
        var harness = new Harness<AddThingCommand> { SaveFailure = call => call == 1 ? new VersionConflictException() : null };

        await Assert.ThrowsAsync<VersionConflictException>(() => harness.Run(new AddThingCommand()));

        Assert.Equal(["begin", "handler", "audit", "save", "dispose", "discard", "audit", "save"], harness.Calls);
        harness.Transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        var rejection = harness.Entries[^1];
        Assert.Equal(AuditOutcome.Rejected, rejection.Outcome);
        Assert.Equal(ErrorCodes.VersionConflict, rejection.ErrorCode);
    }
}
