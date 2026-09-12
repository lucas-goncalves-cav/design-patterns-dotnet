namespace DesignPatterns.Behavioral.ChainOfResponsibility;

public sealed record OrderRequest(
    Guid CustomerId,
    IReadOnlyCollection<OrderItem> Items,
    string PaymentToken,
    string? CouponCode = null);

public sealed record OrderItem(string Sku, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

public sealed record OrderValidationResult(bool Succeeded, string? FailedStep, string? Reason)
{
    public static OrderValidationResult Success() => new(true, null, null);

    public static OrderValidationResult Failure(string step, string reason) => new(false, step, reason);
}

public interface ICustomerRepository
{
    bool Exists(Guid customerId);

    bool IsBlocked(Guid customerId);

    decimal CreditLimit(Guid customerId);
}

public interface IStockChecker
{
    int AvailableQuantity(string sku);
}

public interface IPaymentAuthorizer
{
    bool Authorize(string paymentToken, decimal amount);
}

public interface ICouponService
{
    bool IsValid(string couponCode);
}
