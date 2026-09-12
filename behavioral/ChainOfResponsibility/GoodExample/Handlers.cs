namespace DesignPatterns.Behavioral.ChainOfResponsibility.GoodExample;

/// <summary>
/// Carries the request plus anything a handler computes for the ones after it.
///
/// Most Chain of Responsibility examples pass the request alone, which works
/// until one step needs a value produced by an earlier step. Here the coupon
/// handler changes the total that the credit and payment handlers check, so
/// the context is where that value lives.
/// </summary>
public sealed class OrderValidationContext
{
    public OrderValidationContext(OrderRequest request)
    {
        Request = request;
        Total = request.Items.Sum(item => item.LineTotal);
    }

    public OrderRequest Request { get; }

    public decimal Total { get; set; }
}

/// <summary>
/// One link in the chain. Each handler either rejects the request or passes it
/// to the next one.
/// </summary>
public abstract class OrderValidationHandler
{
    private OrderValidationHandler? _next;

    public abstract string Step { get; }

    /// <summary>
    /// Returns the handler passed in, so chains read in execution order:
    /// <c>a.SetNext(b).SetNext(c)</c>.
    /// </summary>
    public OrderValidationHandler SetNext(OrderValidationHandler next)
    {
        _next = next;

        return next;
    }

    public OrderValidationResult Handle(OrderValidationContext context)
    {
        var result = Check(context);

        if (!result.Succeeded)
        {
            return result;
        }

        return _next?.Handle(context) ?? OrderValidationResult.Success();
    }

    protected abstract OrderValidationResult Check(OrderValidationContext context);

    protected OrderValidationResult Reject(string reason) => OrderValidationResult.Failure(Step, reason);

    protected static OrderValidationResult Accept() => OrderValidationResult.Success();
}

public sealed class BasketHandler : OrderValidationHandler
{
    public override string Step => "basket";

    protected override OrderValidationResult Check(OrderValidationContext context)
    {
        if (context.Request.Items.Count == 0)
        {
            return Reject("An order must contain at least one item.");
        }

        if (context.Request.Items.Any(item => item.Quantity <= 0))
        {
            return Reject("Every item must have a positive quantity.");
        }

        return Accept();
    }
}

public sealed class CustomerHandler : OrderValidationHandler
{
    private readonly ICustomerRepository _customers;

    public CustomerHandler(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public override string Step => "customer";

    protected override OrderValidationResult Check(OrderValidationContext context)
    {
        if (!_customers.Exists(context.Request.CustomerId))
        {
            return Reject($"Customer {context.Request.CustomerId} was not found.");
        }

        if (_customers.IsBlocked(context.Request.CustomerId))
        {
            return Reject("Customer is blocked.");
        }

        return Accept();
    }
}

public sealed class StockHandler : OrderValidationHandler
{
    private readonly IStockChecker _stock;

    public StockHandler(IStockChecker stock)
    {
        _stock = stock;
    }

    public override string Step => "stock";

    protected override OrderValidationResult Check(OrderValidationContext context)
    {
        foreach (var item in context.Request.Items)
        {
            var available = _stock.AvailableQuantity(item.Sku);

            if (available < item.Quantity)
            {
                return Reject($"Only {available} unit(s) of {item.Sku} available, {item.Quantity} requested.");
            }
        }

        return Accept();
    }
}

/// <summary>
/// The one handler that writes to the context. Everything after it sees the
/// discounted total.
/// </summary>
public sealed class CouponHandler : OrderValidationHandler
{
    private const decimal DiscountRate = 0.9m;

    private readonly ICouponService _coupons;

    public CouponHandler(ICouponService coupons)
    {
        _coupons = coupons;
    }

    public override string Step => "coupon";

    protected override OrderValidationResult Check(OrderValidationContext context)
    {
        if (context.Request.CouponCode is null)
        {
            return Accept();
        }

        if (!_coupons.IsValid(context.Request.CouponCode))
        {
            return Reject($"Coupon {context.Request.CouponCode} is not valid.");
        }

        context.Total *= DiscountRate;

        return Accept();
    }
}

public sealed class CreditLimitHandler : OrderValidationHandler
{
    private readonly ICustomerRepository _customers;

    public CreditLimitHandler(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public override string Step => "credit";

    protected override OrderValidationResult Check(OrderValidationContext context)
    {
        var limit = _customers.CreditLimit(context.Request.CustomerId);

        return context.Total > limit
            ? Reject($"Order total {context.Total:C} exceeds the credit limit.")
            : Accept();
    }
}

public sealed class PaymentHandler : OrderValidationHandler
{
    private readonly IPaymentAuthorizer _payment;

    public PaymentHandler(IPaymentAuthorizer payment)
    {
        _payment = payment;
    }

    public override string Step => "payment";

    protected override OrderValidationResult Check(OrderValidationContext context) =>
        _payment.Authorize(context.Request.PaymentToken, context.Total)
            ? Accept()
            : Reject("Payment was declined.");
}
