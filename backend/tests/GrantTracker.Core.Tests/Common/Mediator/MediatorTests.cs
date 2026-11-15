using GrantTracker.Core.Common.Mediator;
using Moq;
using MediatorService = GrantTracker.Core.Common.Mediator.Mediator;

namespace GrantTracker.Core.Tests.Common.Mediator;

public sealed class MediatorTests
{
    // Public so Moq's proxy can implement IRequestHandler<Query, Result>.
    public sealed record Result(string Value);
    public sealed record Query(string Input) : IQuery<Result>;

    // Logs "<name>:before" and "<name>:after" around next(); "after" is in a finally so it also shows on failures.
    private sealed class RecordingBehaviour(string name, List<string> calls) : IPipelineBehaviour<Query, Result>
    {
        public Query? SeenRequest { get; private set; }
        public CancellationToken SeenToken { get; private set; }

        public async Task<Result> Handle(Query request, RequestHandlerDelegate<Result> next, CancellationToken cancellationToken)
        {
            SeenRequest = request;
            SeenToken = cancellationToken;
            calls.Add($"{name}:before");
            try { return await next(); }
            finally { calls.Add($"{name}:after"); }
        }
    }

    private sealed class ShortCircuitBehaviour(Result result) : IPipelineBehaviour<Query, Result>
    {
        public Task<Result> Handle(Query request, RequestHandlerDelegate<Result> next, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    // A null behaviours argument leaves the behaviour list unregistered, so the provider returns null for it.
    private static IServiceProvider Services(IRequestHandler<Query, Result>? handler, IEnumerable<IPipelineBehaviour<Query, Result>>? behaviours = null)
    {
        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IRequestHandler<Query, Result>))).Returns((object?)handler);
        if (behaviours is not null)
            services.Setup(x => x.GetService(typeof(IEnumerable<IPipelineBehaviour<Query, Result>>))).Returns(behaviours);
        return services.Object;
    }

    private static Mock<IRequestHandler<Query, Result>> Handler(List<string> calls, string value = "done")
    {
        var handler = new Mock<IRequestHandler<Query, Result>>();
        handler.Setup(x => x.Handle(It.IsAny<Query>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("handler"))
            .ReturnsAsync(new Result(value));
        return handler;
    }

    [Fact]
    public async Task BehavioursWrapTheHandlerInRegistrationOrder()
    {
        var calls = new List<string>();
        var mediator = new MediatorService(Services(Handler(calls).Object,
            [new RecordingBehaviour("logging", calls), new RecordingBehaviour("audit", calls), new RecordingBehaviour("idempotency", calls)]));

        var result = await mediator.Send<Query, Result>(new Query("x"), TestContext.Current.CancellationToken);

        Assert.Equal(new Result("done"), result);
        Assert.Equal(
            ["logging:before", "audit:before", "idempotency:before", "handler", "idempotency:after", "audit:after", "logging:after"],
            calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WithNoBehavioursTheHandlerIsCalledDirectly(bool registeredEmptyList)
    {
        var calls = new List<string>();
        var services = Services(Handler(calls).Object, registeredEmptyList ? [] : null);

        var result = await new MediatorService(services).Send<Query, Result>(new Query("x"), TestContext.Current.CancellationToken);

        Assert.Equal(new Result("done"), result);
        Assert.Equal(["handler"], calls);
    }

    [Fact]
    public async Task MissingHandlerThrowsAnInformativeError()
    {
        var mediator = new MediatorService(Services(handler: null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mediator.Send<Query, Result>(new Query("x"), TestContext.Current.CancellationToken));

        Assert.Contains("No handler registered for Query.", exception.Message);
    }

    [Fact]
    public async Task RequestAndCancellationTokenReachTheBehavioursAndTheHandler()
    {
        using var source = new CancellationTokenSource();
        var request = new Query("x");
        var calls = new List<string>();
        var behaviour = new RecordingBehaviour("outer", calls);
        var handler = Handler(calls);

        await new MediatorService(Services(handler.Object, [behaviour])).Send<Query, Result>(request, source.Token);

        Assert.Same(request, behaviour.SeenRequest);
        Assert.Equal(source.Token, behaviour.SeenToken);
        handler.Verify(x => x.Handle(request, source.Token), Times.Once);
    }

    [Fact]
    public async Task BehaviourCanShortCircuitWithoutCallingTheHandler()
    {
        var calls = new List<string>();
        var handler = Handler(calls);
        var mediator = new MediatorService(Services(handler.Object,
            [new RecordingBehaviour("outer", calls), new ShortCircuitBehaviour(new Result("replayed"))]));

        var result = await mediator.Send<Query, Result>(new Query("x"), TestContext.Current.CancellationToken);

        Assert.Equal(new Result("replayed"), result);
        Assert.Equal(["outer:before", "outer:after"], calls);
        handler.Verify(x => x.Handle(It.IsAny<Query>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandlerExceptionsPropagateThroughEveryBehaviourFromInnerToOuter()
    {
        var calls = new List<string>();
        var failure = new InvalidOperationException("boom");
        var handler = new Mock<IRequestHandler<Query, Result>>();
        handler.Setup(x => x.Handle(It.IsAny<Query>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var mediator = new MediatorService(Services(handler.Object,
            [new RecordingBehaviour("outer", calls), new RecordingBehaviour("inner", calls)]));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mediator.Send<Query, Result>(new Query("x"), TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
        Assert.Equal(["outer:before", "inner:before", "inner:after", "outer:after"], calls);
    }

    [Fact]
    public async Task EachSendBuildsItsOwnChain()
    {
        var calls = new List<string>();
        var handler = Handler(calls);
        var behaviour = new RecordingBehaviour("b", calls);
        var mediator = new MediatorService(Services(handler.Object, [behaviour]));
        var first = new Query("first");
        var second = new Query("second");

        await mediator.Send<Query, Result>(first, TestContext.Current.CancellationToken);
        await mediator.Send<Query, Result>(second, TestContext.Current.CancellationToken);

        Assert.Equal(["b:before", "handler", "b:after", "b:before", "handler", "b:after"], calls);
        handler.Verify(x => x.Handle(first, It.IsAny<CancellationToken>()), Times.Once);
        handler.Verify(x => x.Handle(second, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Same(second, behaviour.SeenRequest);
    }
}
