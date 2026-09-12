namespace DesignPatterns.Behavioral.Mediator.GoodExample;

// -----------------------------------------------------------------------------
// Requests
// -----------------------------------------------------------------------------

public sealed record RegisterCustomer(string Name, string Email) : IRequest<CustomerRegistered>;

public sealed record GetCustomer(Guid CustomerId) : IRequest<CustomerDetails?>;

// -----------------------------------------------------------------------------
// Handlers: one per use case, with only the dependencies that use case needs
// -----------------------------------------------------------------------------

public sealed class RegisterCustomerHandler : IRequestHandler<RegisterCustomer, CustomerRegistered>
{
    private readonly ICustomerStore _store;
    private readonly IWelcomeEmailSender _email;

    public RegisterCustomerHandler(ICustomerStore store, IWelcomeEmailSender email)
    {
        _store = store;
        _email = email;
    }

    public CustomerRegistered Handle(RegisterCustomer request)
    {
        if (_store.EmailTaken(request.Email))
        {
            throw new InvalidOperationException($"Email {request.Email} is already registered.");
        }

        var customer = new CustomerDetails(Guid.NewGuid(), request.Name, request.Email, Active: true);

        _store.Save(customer);
        _email.SendWelcome(customer.Email, customer.Name);

        return new CustomerRegistered(customer.CustomerId, customer.Name, customer.Email);
    }
}

public sealed class GetCustomerHandler : IRequestHandler<GetCustomer, CustomerDetails?>
{
    private readonly ICustomerStore _store;

    public GetCustomerHandler(ICustomerStore store)
    {
        _store = store;
    }

    public CustomerDetails? Handle(GetCustomer request) => _store.Find(request.CustomerId);
}

// -----------------------------------------------------------------------------
// Behaviours: cross cutting concerns, written once
// -----------------------------------------------------------------------------

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IRequestLog _log;

    public LoggingBehavior(IRequestLog log)
    {
        _log = log;
    }

    public TResponse Handle(TRequest request, Func<TResponse> next)
    {
        _log.Write($"{typeof(TRequest).Name} started.");

        try
        {
            var response = next();

            _log.Write($"{typeof(TRequest).Name} succeeded.");

            return response;
        }
        catch (Exception exception)
        {
            _log.Write($"{typeof(TRequest).Name} failed: {exception.Message}");
            throw;
        }
    }
}

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IRequestValidator<TRequest> _validator;

    public ValidationBehavior(IRequestValidator<TRequest> validator)
    {
        _validator = validator;
    }

    public TResponse Handle(TRequest request, Func<TResponse> next)
    {
        var errors = _validator.Validate(request);

        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors));
        }

        return next();
    }
}

// -----------------------------------------------------------------------------
// Collaborators
// -----------------------------------------------------------------------------

public interface IWelcomeEmailSender
{
    void SendWelcome(string email, string name);
}

public interface IRequestLog
{
    void Write(string message);
}

public interface IRequestValidator<in TRequest>
{
    IReadOnlyCollection<string> Validate(TRequest request);
}

public sealed class RegisterCustomerValidator : IRequestValidator<RegisterCustomer>
{
    public IReadOnlyCollection<string> Validate(RegisterCustomer request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            errors.Add("A valid email is required.");
        }

        return errors;
    }
}

// -----------------------------------------------------------------------------
// The controller, reduced to translating HTTP into a request
// -----------------------------------------------------------------------------

public sealed class CustomerController
{
    private readonly IMediator _mediator;

    public CustomerController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public CustomerRegistered Register(RegisterCustomerCommand command) =>
        _mediator.Send(new RegisterCustomer(command.Name, command.Email));

    public CustomerDetails? Get(GetCustomerQuery query) =>
        _mediator.Send(new GetCustomer(query.CustomerId));
}
