namespace DesignPatterns.Behavioral.ChainOfResponsibility.BadExample;

/// <summary>
/// Every validation rule, in order, in one method, with the nesting that
/// inevitably comes with it.
///
/// The individual checks are all reasonable. The problems are structural:
///
///   - Reordering the checks means moving code, and the nesting makes that
///     error prone
///   - Making one check conditional adds another level of nesting
///   - Testing "blocked customer is rejected" needs stock, payment and coupon
///     doubles that have nothing to do with the test
///   - The class depends on everything any rule might ever need
///   - Adding a rule means editing a method that is already hard to read
/// </summary>
public sealed class OrderValidator
{
    private readonly ICustomerRepository _customers;
    private readonly IStockChecker _stock;
    private readonly IPaymentAuthorizer _payment;
    private readonly ICouponService _coupons;

    public OrderValidator(
        ICustomerRepository customers,
        IStockChecker stock,
        IPaymentAuthorizer payment,
        ICouponService coupons)
    {
        _customers = customers;
        _stock = stock;
        _payment = payment;
        _coupons = coupons;
    }

    public OrderValidationResult Validate(OrderRequest request)
    {
        if (request.Items.Count == 0)
        {
            return OrderValidationResult.Failure("basket", "An order must contain at least one item.");
        }

        if (request.Items.Any(item => item.Quantity <= 0))
        {
            return OrderValidationResult.Failure("basket", "Every item must have a positive quantity.");
        }

        if (!_customers.Exists(request.CustomerId))
        {
            return OrderValidationResult.Failure("customer", $"Customer {request.CustomerId} was not found.");
        }

        if (_customers.IsBlocked(request.CustomerId))
        {
            return OrderValidationResult.Failure("customer", "Customer is blocked.");
        }

        foreach (var item in request.Items)
        {
            var available = _stock.AvailableQuantity(item.Sku);

            if (available < item.Quantity)
            {
                return OrderValidationResult.Failure(
                    "stock",
                    $"Only {available} unit(s) of {item.Sku} available, {item.Quantity} requested.");
            }
        }

        var total = request.Items.Sum(item => item.LineTotal);

        if (request.CouponCode is not null)
        {
            if (!_coupons.IsValid(request.CouponCode))
            {
                return OrderValidationResult.Failure("coupon", $"Coupon {request.CouponCode} is not valid.");
            }

            total *= 0.9m;
        }

        if (total > _customers.CreditLimit(request.CustomerId))
        {
            return OrderValidationResult.Failure(
                "credit",
                $"Order total {total:C} exceeds the credit limit.");
        }

        if (!_payment.Authorize(request.PaymentToken, total))
        {
            return OrderValidationResult.Failure("payment", "Payment was declined.");
        }

        return OrderValidationResult.Success();
    }
}
