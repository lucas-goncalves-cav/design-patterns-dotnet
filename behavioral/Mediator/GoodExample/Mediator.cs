using System.Reflection;
using System.Runtime.ExceptionServices;

namespace DesignPatterns.Behavioral.Mediator.GoodExample;

/// <summary>
/// A request that produces a response. The marker interface is what lets the
/// mediator resolve the right handler from the request type alone.
/// </summary>
public interface IRequest<TResponse>;

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    TResponse Handle(TRequest request);
}

/// <summary>
/// Runs around a handler. This is where logging, metrics, validation and
/// transactions go, written once instead of copied into every endpoint.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    TResponse Handle(TRequest request, Func<TResponse> next);
}

public interface IMediator
{
    TResponse Send<TResponse>(IRequest<TResponse> request);
}

/// <summary>
/// A deliberately small mediator, written out so the mechanism is visible.
/// In production you would use MediatR rather than this.
///
/// Resolution is by request type: the sender never names a handler, and the
/// handler never names a sender.
/// </summary>
public sealed class Mediator : IMediator
{
    private readonly IServiceLocator _services;

    public Mediator(IServiceLocator services)
    {
        _services = services;
    }

    public TResponse Send<TResponse>(IRequest<TResponse> request)
    {
        var requestType = request.GetType();

        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handler = _services.Resolve(handlerType)
            ?? throw new InvalidOperationException($"No handler registered for {requestType.Name}.");

        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviors = _services.ResolveAll(behaviorType).ToList();

        Func<TResponse> pipeline = () => InvokeHandler<TResponse>(handler, requestType, request);

        // Built in reverse so the first registered behaviour ends up outermost.
        for (var index = behaviors.Count - 1; index >= 0; index--)
        {
            var behavior = behaviors[index];
            var next = pipeline;

            pipeline = () => InvokeBehavior<TResponse>(behavior, behaviorType, request, next);
        }

        return pipeline();
    }

    private static TResponse InvokeHandler<TResponse>(object handler, Type requestType, object request)
    {
        var method = handler.GetType().GetMethod("Handle", [requestType])
            ?? throw new InvalidOperationException($"Handler {handler.GetType().Name} has no Handle method.");

        return Invoke<TResponse>(method, handler, [request]);
    }

    private static TResponse InvokeBehavior<TResponse>(
        object behavior,
        Type behaviorType,
        object request,
        Func<TResponse> next)
    {
        var method = behaviorType.GetMethod("Handle")
            ?? throw new InvalidOperationException("IPipelineBehavior has no Handle method.");

        return Invoke<TResponse>(method, behavior, [request, next]);
    }

    /// <summary>
    /// Reflection wraps anything a handler throws in a TargetInvocationException,
    /// which would force every caller to unwrap it to find out what actually
    /// went wrong. Rethrowing the inner exception through ExceptionDispatchInfo
    /// preserves both the original type and its stack trace.
    /// </summary>
    private static TResponse Invoke<TResponse>(MethodInfo method, object target, object?[] arguments)
    {
        try
        {
            return (TResponse)method.Invoke(target, arguments)!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();

            throw; // Unreachable, the line above always throws.
        }
    }
}

/// <summary>
/// A minimal stand in for a DI container, so the example does not need one.
/// </summary>
public interface IServiceLocator
{
    object? Resolve(Type serviceType);

    IEnumerable<object> ResolveAll(Type serviceType);
}

public sealed class ServiceLocator : IServiceLocator
{
    private readonly Dictionary<Type, List<object>> _services = [];

    public ServiceLocator Register(Type serviceType, object instance)
    {
        if (!_services.TryGetValue(serviceType, out var instances))
        {
            instances = [];
            _services[serviceType] = instances;
        }

        instances.Add(instance);

        return this;
    }

    public object? Resolve(Type serviceType) =>
        _services.TryGetValue(serviceType, out var instances) ? instances.FirstOrDefault() : null;

    public IEnumerable<object> ResolveAll(Type serviceType) =>
        _services.TryGetValue(serviceType, out var instances) ? instances : [];
}
