using DesignPatterns.Behavioral.ChainOfResponsibility;
using DesignPatterns.Behavioral.ChainOfResponsibility.GoodExample;
using DesignPatterns.Behavioral.Observer;
using DesignPatterns.Behavioral.Observer.GoodExample;
using DesignPatterns.Behavioral.Strategy;
using DesignPatterns.Behavioral.Strategy.GoodExample;

namespace DesignPatterns.RealWorld.OrderCheckout;

public sealed record CheckoutRequest(
    Guid CustomerId,
    IReadOnlyCollection<OrderItem> Items,
    PaymentMethod PaymentMethod,
    string PaymentToken,
    string CustomerEmail,
    string? CouponCode = null,
    int Installments = 1);

public sealed record CheckoutOutcome(
    bool Succeeded,
    Guid? OrderId,
    string? RejectedStep,
    string? RejectionReason,
    PaymentResult? Payment,
    IReadOnlyCollection<HandlerFailure> SideEffectFailures)
{
    public static CheckoutOutcome Rejected(OrderValidationResult validation) =>
        new(false, null, validation.FailedStep, validation.Reason, null, []);

    public static CheckoutOutcome Completed(
        Guid orderId,
        PaymentResult payment,
        IReadOnlyCollection<HandlerFailure> failures) =>
        new(true, orderId, null, null, payment, failures);
}

/// <summary>
/// A checkout built from four of the patterns in this repository, each doing
/// the job it is actually good at:
///
///   Chain of Responsibility  validate the order, stopping at the first problem
///   Strategy                 charge using the selected payment method
///   Observer                 announce the confirmed order to whoever cares
///   Factory                  is used one level up, choosing the gateway
///
/// The point of this file is that no pattern is doing another one's job. The
/// validation chain does not know about payment methods, the payment strategy
/// does not know about stock, and the side effects do not know about either.
/// </summary>
public sealed class CheckoutPipeline
{
    private readonly OrderValidator _validator;
    private readonly PaymentProcessor _payments;
    private readonly OrderConfirmedPublisher _publisher;

    public CheckoutPipeline(
        OrderValidator validator,
        PaymentProcessor payments,
        OrderConfirmedPublisher publisher)
    {
        _validator = validator;
        _payments = payments;
        _publisher = publisher;
    }

    public CheckoutOutcome Checkout(CheckoutRequest request)
    {
        var validation = _validator.Validate(new OrderRequest(
            request.CustomerId,
            request.Items,
            request.PaymentToken,
            request.CouponCode));

        if (!validation.Succeeded)
        {
            return CheckoutOutcome.Rejected(validation);
        }

        var total = request.Items.Sum(item => item.LineTotal);

        if (request.CouponCode is not null)
        {
            total *= 0.9m;
        }

        var payment = _payments.Process(new PaymentRequest(
            request.PaymentMethod,
            total,
            request.CustomerId.ToString(),
            request.Installments));

        var orderId = Guid.NewGuid();

        // Side effects are announced, not called. A failing welcome email must
        // not undo a paid order, so failures are reported rather than thrown.
        var failures = _publisher.Publish(new OrderConfirmed(
            orderId,
            request.CustomerEmail,
            payment.AmountCharged,
            request.Items
                .Select(item => new DesignPatterns.Behavioral.Observer.OrderLine(
                    item.Sku,
                    item.Quantity,
                    item.UnitPrice))
                .ToList(),
            DateTime.UtcNow));

        return CheckoutOutcome.Completed(orderId, payment, failures);
    }
}
