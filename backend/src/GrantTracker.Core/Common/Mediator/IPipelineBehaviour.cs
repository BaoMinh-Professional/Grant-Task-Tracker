namespace GrantTracker.Core.Common.Mediator;

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

public interface IPipelineBehaviour<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
