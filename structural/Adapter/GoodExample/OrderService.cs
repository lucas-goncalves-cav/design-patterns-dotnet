namespace DesignPatterns.Structural.Adapter.GoodExample;

/// <summary>
/// Business logic in the application's own vocabulary. There is no provider
/// name, no cents conversion and no status string anywhere in this class.
/// </summary>
public sealed class OrderService
{
    private readonly IPaymentGateway _gateway;

    public OrderService(IPaymentGateway gateway)
    {
        _gateway = gateway;
    }

    public Payment CreateCharge(CreatePaymentCommand command) => _gateway.Create(command);

    public bool IsPaid(string externalId) => _gateway.GetById(externalId).Status == PaymentStatus.Paid;
}
