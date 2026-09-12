using DesignPatterns.Behavioral.Observer;
using DesignPatterns.Behavioral.Observer.GoodExample;
using FluentAssertions;
using BadOrderService = DesignPatterns.Behavioral.Observer.BadExample.OrderService;
using GoodOrderService = DesignPatterns.Behavioral.Observer.GoodExample.OrderService;

namespace DesignPatterns.Tests.Behavioral;

public class ObserverTests
{
    private static OrderConfirmed SampleOrder() =>
        new(
            OrderId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CustomerEmail: "customer@example.com",
            Total: 349.90m,
            Lines: [new OrderLine("SKU-1", 2, 100m), new OrderLine("SKU-2", 1, 149.90m)],
            ConfirmedAt: new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void BothVersionsProduceTheSameSideEffects()
    {
        var order = SampleOrder();

        var badEmail = new FakeEmailSender();
        var badStock = new FakeStockLedger();
        var badAudit = new FakeAuditLog();
        var badPush = new FakePushNotifier();

        new BadOrderService(badEmail, badStock, badAudit, badPush).Confirm(order);

        var goodEmail = new FakeEmailSender();
        var goodStock = new FakeStockLedger();
        var goodAudit = new FakeAuditLog();
        var goodPush = new FakePushNotifier();

        new GoodOrderService(new OrderConfirmedPublisher(
        [
            new AuditOrderConfirmedHandler(goodAudit),
            new SendConfirmationEmailHandler(goodEmail),
            new DecreaseStockHandler(goodStock),
            new PushNotificationHandler(goodPush)
        ])).Confirm(order);

        goodEmail.Sent.Should().BeEquivalentTo(badEmail.Sent);
        goodStock.Movements.Should().BeEquivalentTo(badStock.Movements);
        goodAudit.Entries.Should().BeEquivalentTo(badAudit.Entries);
        goodPush.Notifications.Should().BeEquivalentTo(badPush.Notifications);
    }

    [Fact]
    public void StockIsDecreasedPerLine()
    {
        var stock = new FakeStockLedger();
        var publisher = new OrderConfirmedPublisher([new DecreaseStockHandler(stock)]);

        publisher.Publish(SampleOrder());

        stock.Movements.Should().BeEquivalentTo([("SKU-1", 2), ("SKU-2", 1)]);
    }

    /// <summary>
    /// The behaviour the bad version cannot offer: a broken email provider does
    /// not stop the stock from being updated.
    /// </summary>
    [Fact]
    public void AFailingHandlerDoesNotPreventTheOthers()
    {
        var stock = new FakeStockLedger();
        var audit = new FakeAuditLog();

        var publisher = new OrderConfirmedPublisher(
        [
            new SendConfirmationEmailHandler(new ThrowingEmailSender()),
            new DecreaseStockHandler(stock),
            new AuditOrderConfirmedHandler(audit)
        ]);

        var failures = publisher.Publish(SampleOrder());

        stock.Movements.Should().HaveCount(2);
        audit.Entries.Should().ContainSingle();

        failures.Should().ContainSingle();
        failures.Single().HandlerName.Should().Be("email");
        failures.Single().Exception.Should().BeOfType<InvalidOperationException>();
    }

    /// <summary>
    /// Failures are reported, not swallowed. Silently ignoring them would trade
    /// one problem for a worse one.
    /// </summary>
    [Fact]
    public void FailuresAreReportedToTheCaller()
    {
        var publisher = new OrderConfirmedPublisher(
        [
            new SendConfirmationEmailHandler(new ThrowingEmailSender())
        ]);

        var failures = publisher.Publish(SampleOrder());

        failures.Should().ContainSingle();
        failures.Single().Exception.Message.Should().Be("SMTP unavailable.");
    }

    [Fact]
    public void NoHandlersMeansNoFailuresAndNoSideEffects()
    {
        var publisher = new OrderConfirmedPublisher([]);

        var failures = publisher.Publish(SampleOrder());

        failures.Should().BeEmpty();
    }

    /// <summary>
    /// Adding a reaction is adding a class. The service is untouched.
    /// </summary>
    [Fact]
    public void ANewReactionRequiresNoChangeToTheService()
    {
        var warehouse = new RecordingHandler("warehouse");
        var service = new GoodOrderService(new OrderConfirmedPublisher([warehouse]));

        service.Confirm(SampleOrder());

        warehouse.Received.Should().ContainSingle();
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];

        public void Send(string to, string subject, string body) => Sent.Add((to, subject, body));
    }

    private sealed class ThrowingEmailSender : IEmailSender
    {
        public void Send(string to, string subject, string body) =>
            throw new InvalidOperationException("SMTP unavailable.");
    }

    private sealed class FakeStockLedger : IStockLedger
    {
        public List<(string Sku, int Quantity)> Movements { get; } = [];

        public void Decrease(string sku, int quantity) => Movements.Add((sku, quantity));
    }

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<string> Entries { get; } = [];

        public void Record(string message) => Entries.Add(message);
    }

    private sealed class FakePushNotifier : IPushNotifier
    {
        public List<(Guid OrderId, string Message)> Notifications { get; } = [];

        public void Notify(Guid orderId, string message) => Notifications.Add((orderId, message));
    }

    private sealed class RecordingHandler : IOrderConfirmedHandler
    {
        public RecordingHandler(string name) => Name = name;

        public string Name { get; }

        public List<OrderConfirmed> Received { get; } = [];

        public void Handle(OrderConfirmed order) => Received.Add(order);
    }
}
