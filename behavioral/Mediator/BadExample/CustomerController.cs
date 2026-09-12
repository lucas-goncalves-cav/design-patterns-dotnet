namespace DesignPatterns.Behavioral.Mediator.BadExample;

/// <summary>
/// A controller that knows every service it needs, and every service those
/// services need to be given.
///
/// This is the shape a controller drifts into. Each individual step was a
/// reasonable addition; the accumulation is the problem:
///
///   - Six constructor parameters, and the next feature makes it seven
///   - Validation, orchestration and cross cutting concerns are all inline
///   - Testing one endpoint requires constructing six test doubles
///   - Every controller in the application repeats the logging and validation
///     wrapping, slightly differently
/// </summary>
public sealed class CustomerController
{
    private readonly ICustomerStore _store;
    private readonly IEmailService _email;
    private readonly IAuditService _audit;
    private readonly IMetricsRecorder _metrics;
    private readonly IValidator _validator;
    private readonly IAppLogger _logger;

    public CustomerController(
        ICustomerStore store,
        IEmailService email,
        IAuditService audit,
        IMetricsRecorder metrics,
        IValidator validator,
        IAppLogger logger)
    {
        _store = store;
        _email = email;
        _audit = audit;
        _metrics = metrics;
        _validator = validator;
        _logger = logger;
    }

    public CustomerRegistered Register(RegisterCustomerCommand command)
    {
        _logger.Log($"Register started for {command.Email}.");
        _metrics.Increment("customer.register.attempt");

        try
        {
            // Validation, inline, in the controller.
            var errors = _validator.Validate(command);

            if (errors.Count > 0)
            {
                throw new ArgumentException(string.Join(" ", errors));
            }

            if (_store.EmailTaken(command.Email))
            {
                throw new InvalidOperationException($"Email {command.Email} is already registered.");
            }

            // Orchestration, also in the controller.
            var customer = new CustomerDetails(Guid.NewGuid(), command.Name, command.Email, Active: true);
            _store.Save(customer);

            _email.SendWelcome(customer.Email, customer.Name);
            _audit.Record($"Customer {customer.CustomerId} registered.");
            _metrics.Increment("customer.register.success");

            return new CustomerRegistered(customer.CustomerId, customer.Name, customer.Email);
        }
        catch (Exception exception)
        {
            _logger.Log($"Register failed for {command.Email}: {exception.Message}");
            _metrics.Increment("customer.register.failure");
            throw;
        }
    }

    public CustomerDetails? Get(GetCustomerQuery query)
    {
        // And the same logging and metrics wrapping, copied.
        _logger.Log($"Get started for {query.CustomerId}.");
        _metrics.Increment("customer.get.attempt");

        return _store.Find(query.CustomerId);
    }
}

public interface IEmailService
{
    void SendWelcome(string email, string name);
}

public interface IAuditService
{
    void Record(string message);
}

public interface IMetricsRecorder
{
    void Increment(string counter);
}

public interface IValidator
{
    IReadOnlyCollection<string> Validate(RegisterCustomerCommand command);
}

public interface IAppLogger
{
    void Log(string message);
}
