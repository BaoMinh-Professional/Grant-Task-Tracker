using System.Text.Json;
using GrantTracker.Core.Auditing;
using GrantTracker.Core.Common.Behaviours;
using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Common.Persistence;
using GrantTracker.Core.Idempotency;
using GrantTracker.Core.Tests.Common;
using GrantTracker.Core.Tests.Common.Time;
using Moq;

namespace GrantTracker.Core.Tests.Idempotency;

public sealed class IdempotencyBehaviourTests
{
    private sealed record Result(string Id);
    private sealed record Command(string Value, string? IdempotencyKey) : ICommand<Result>, IIdempotentCommand;

    private static readonly FakeBusinessClock Clock = new(TestGrants.Now, TestGrants.Today);

    [Fact]
    public async Task ReservesTheKeyBeforeTheHandlerRunsAndStoresTheResult()
    {
        IdempotencyRecord? reserved = null;
        var store = new Mock<IIdempotencyStore>();
        store.Setup(x => x.Add(It.IsAny<IdempotencyRecord>())).Callback<IdempotencyRecord>(record => reserved = record);
        var unitOfWork = new Mock<IUnitOfWork>();
        var behaviour = new IdempotencyBehaviour<Command, Result>(store.Object, unitOfWork.Object, Clock, new AuditContext());

        var result = await behaviour.Handle(new Command("x", "key-1"), () =>
        {
            Assert.NotNull(reserved);
            unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            return Task.FromResult(new Result("task-1"));
        }, TestContext.Current.CancellationToken);

        Assert.Equal(new Result("task-1"), result);
        Assert.Equal("task-1", JsonSerializer.Deserialize<Result>(reserved!.ResultJson)!.Id);
    }

    [Fact]
    public async Task ReplaysTheCommittedResultWhenTheKeyIsAlreadyTaken()
    {
        var command = new Command("x", "key-2");
        var stored = new IdempotencyRecord("key-2", nameof(Command), RequestHasher.Hash(command),
            JsonSerializer.Serialize(new Result("task-2")), Clock.UtcNow);
        var store = new Mock<IIdempotencyStore>();
        store.Setup(x => x.FindAsync("key-2", It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UniqueConstraintViolationException("duplicate key"));
        var auditContext = new AuditContext();
        var behaviour = new IdempotencyBehaviour<Command, Result>(store.Object, unitOfWork.Object, Clock, auditContext);

        var result = await behaviour.Handle(command, () => throw new InvalidOperationException("The handler must not run."),
            TestContext.Current.CancellationToken);

        Assert.Equal(new Result("task-2"), result);
        Assert.Equal(AuditOutcome.Replayed, auditContext.Outcome);
        unitOfWork.Verify(x => x.DiscardChanges(), Times.Once);
    }

    [Fact]
    public async Task RejectsAKeyReusedForADifferentRequest()
    {
        var stored = new IdempotencyRecord("key-3", nameof(Command), "another-request-hash",
            JsonSerializer.Serialize(new Result("task-3")), Clock.UtcNow);
        var store = new Mock<IIdempotencyStore>();
        store.Setup(x => x.FindAsync("key-3", It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UniqueConstraintViolationException("duplicate key"));
        var behaviour = new IdempotencyBehaviour<Command, Result>(store.Object, unitOfWork.Object, Clock, new AuditContext());

        await Assert.ThrowsAsync<IdempotencyKeyMismatchException>(() =>
            behaviour.Handle(new Command("y", "key-3"), () => throw new InvalidOperationException("The handler must not run."),
                TestContext.Current.CancellationToken));
    }
}
