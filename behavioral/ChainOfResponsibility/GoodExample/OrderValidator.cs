namespace DesignPatterns.Behavioral.ChainOfResponsibility.GoodExample;

/// <summary>
/// Builds the chain and runs it. The order of the steps is now a list you can
/// read, rather than nesting you have to trace.
/// </summary>
public sealed class OrderValidator
{
    private readonly OrderValidationHandler _first;

    public OrderValidator(IEnumerable<OrderValidationHandler> handlers)
    {
        var ordered = handlers.ToList();

        if (ordered.Count == 0)
        {
            throw new ArgumentException("At least one handler is required.", nameof(handlers));
        }

        _first = ordered[0];

        for (var index = 0; index < ordered.Count - 1; index++)
        {
            ordered[index].SetNext(ordered[index + 1]);
        }
    }

    public OrderValidationResult Validate(OrderRequest request) =>
        _first.Handle(new OrderValidationContext(request));

    /// <summary>
    /// The default pipeline. Cheap checks run before expensive ones, so an
    /// empty basket never reaches the payment provider.
    /// </summary>
    public static OrderValidator CreateDefault(
        ICustomerRepository customers,
        IStockChecker stock,
        IPaymentAuthorizer payment,
        ICouponService coupons) =>
        new(
        [
            new BasketHandler(),
            new CustomerHandler(customers),
            new StockHandler(stock),
            new CouponHandler(coupons),
            new CreditLimitHandler(customers),
            new PaymentHandler(payment)
        ]);
}
