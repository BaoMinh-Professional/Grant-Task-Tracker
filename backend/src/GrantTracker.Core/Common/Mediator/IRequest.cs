namespace GrantTracker.Core.Common.Mediator;

public interface IRequest<TResponse>;
public interface IQuery<TResponse> : IRequest<TResponse>;
public interface ICommand;
public interface ICommand<TResponse> : IRequest<TResponse>, ICommand;
