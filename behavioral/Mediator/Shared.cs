namespace DesignPatterns.Behavioral.Mediator;

public sealed record RegisterCustomerCommand(string Name, string Email);

public sealed record CustomerRegistered(Guid CustomerId, string Name, string Email);

public sealed record GetCustomerQuery(Guid CustomerId);

public sealed record CustomerDetails(Guid CustomerId, string Name, string Email, bool Active);

public interface ICustomerStore
{
    void Save(CustomerDetails customer);

    CustomerDetails? Find(Guid customerId);

    bool EmailTaken(string email);
}

public sealed class InMemoryCustomerStore : ICustomerStore
{
    private readonly Dictionary<Guid, CustomerDetails> _customers = [];

    public void Save(CustomerDetails customer) => _customers[customer.CustomerId] = customer;

    public CustomerDetails? Find(Guid customerId) => _customers.GetValueOrDefault(customerId);

    public bool EmailTaken(string email) =>
        _customers.Values.Any(customer => customer.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
}
