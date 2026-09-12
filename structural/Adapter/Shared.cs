namespace DesignPatterns.Structural.Adapter;

/// <summary>
/// The vocabulary of this application, chosen by this application. External
/// providers are translated into it, never the other way around.
/// </summary>
public enum PaymentStatus
{
    Pending,
    Paid,
    Overdue,
    Cancelled,
    Refunded,
    Failed
}

public enum PaymentMethod
{
    Pix,
    Boleto,
    CreditCard
}

public sealed record CreatePaymentCommand(
    decimal Amount,
    PaymentMethod Method,
    string CustomerReference,
    DateOnly DueDate);

public sealed record Payment(
    string ExternalId,
    PaymentStatus Status,
    decimal Amount,
    PaymentMethod Method);

/// <summary>
/// The target interface. Every gateway is adapted to this shape.
/// </summary>
public interface IPaymentGateway
{
    string Name { get; }

    Payment Create(CreatePaymentCommand command);

    Payment GetById(string externalId);
}
