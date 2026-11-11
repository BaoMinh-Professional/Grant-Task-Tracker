namespace GrantTracker.Core.Common.Mediator;

public sealed class Mediator(IServiceProvider services) : IMediator
{
    public Task<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>
    {
        var handler = services.GetService(typeof(IRequestHandler<TRequest, TResponse>)) as IRequestHandler<TRequest, TResponse>
            ?? throw new InvalidOperationException($"No handler registered for {typeof(TRequest).Name}.");
        var behaviours = services.GetService(typeof(IEnumerable<IPipelineBehaviour<TRequest, TResponse>>))
            as IEnumerable<IPipelineBehaviour<TRequest, TResponse>> ?? [];

        RequestHandlerDelegate<TResponse> next = () => handler.Handle(request, cancellationToken);
        foreach (var behaviour in behaviours.Reverse())
        {
            var inner = next;
            next = () => behaviour.Handle(request, inner, cancellationToken);
        }

        return next();
    }
}
