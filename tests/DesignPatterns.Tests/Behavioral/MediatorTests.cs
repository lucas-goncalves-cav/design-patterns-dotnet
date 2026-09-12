using DesignPatterns.Behavioral.Mediator;
using DesignPatterns.Behavioral.Mediator.GoodExample;
using FluentAssertions;
using GoodController = DesignPatterns.Behavioral.Mediator.GoodExample.CustomerController;
using Mediator = DesignPatterns.Behavioral.Mediator.GoodExample.Mediator;

namespace DesignPatterns.Tests.Behavioral;

public class MediatorTests
{
    private sealed record Harness(
        GoodController Controller,
        IMediator Mediator,
        ICustomerStore Store,
        FakeLog Log,
        FakeEmailSender Email);

    private static Harness Build(bool withValidation = true, bool withLogging = true)
    {
        var store = new InMemoryCustomerStore();
        var email = new FakeEmailSender();
        var log = new FakeLog();

        var locator = new ServiceLocator()
            .Register(
                typeof(IRequestHandler<RegisterCustomer, CustomerRegistered>),
                new RegisterCustomerHandler(store, email))
            .Register(
                typeof(IRequestHandler<GetCustomer, CustomerDetails?>),
                new GetCustomerHandler(store));

        if (withLogging)
        {
            locator.Register(
                typeof(IPipelineBehavior<RegisterCustomer, CustomerRegistered>),
                new LoggingBehavior<RegisterCustomer, CustomerRegistered>(log));
            locator.Register(
                typeof(IPipelineBehavior<GetCustomer, CustomerDetails?>),
                new LoggingBehavior<GetCustomer, CustomerDetails?>(log));
        }

        if (withValidation)
        {
            locator.Register(
                typeof(IPipelineBehavior<RegisterCustomer, CustomerRegistered>),
                new ValidationBehavior<RegisterCustomer, CustomerRegistered>(new RegisterCustomerValidator()));
        }

        var mediator = new Mediator(locator);

        return new Harness(new GoodController(mediator), mediator, store, log, email);
    }

    [Fact]
    public void RegisteringACustomerStoresItAndSendsAWelcomeEmail()
    {
        var harness = Build();

        var result = harness.Controller.Register(new RegisterCustomerCommand("Ana Souza", "ana@example.com"));

        result.Name.Should().Be("Ana Souza");
        harness.Store.Find(result.CustomerId).Should().NotBeNull();
        harness.Email.Sent.Should().ContainSingle();
        harness.Email.Sent.Single().Email.Should().Be("ana@example.com");
    }

    [Fact]
    public void TheSameEmailCannotBeRegisteredTwice()
    {
        var harness = Build();
        harness.Controller.Register(new RegisterCustomerCommand("Ana", "ana@example.com"));

        var act = () => harness.Controller.Register(new RegisterCustomerCommand("Other", "ana@example.com"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*already registered*");
    }

    [Fact]
    public void AQueryRoutesToItsOwnHandler()
    {
        var harness = Build();
        var registered = harness.Controller.Register(new RegisterCustomerCommand("Bruno", "bruno@example.com"));

        var found = harness.Controller.Get(new GetCustomerQuery(registered.CustomerId));

        found.Should().NotBeNull();
        found!.Email.Should().Be("bruno@example.com");
    }

    [Fact]
    public void AnUnknownCustomerReturnsNullRatherThanThrowing()
    {
        var harness = Build();

        harness.Controller.Get(new GetCustomerQuery(Guid.NewGuid())).Should().BeNull();
    }

    /// <summary>
    /// Validation is a behaviour, so it runs without the handler knowing it
    /// exists and without the controller calling it.
    /// </summary>
    [Theory]
    [InlineData("", "ana@example.com")]
    [InlineData("Ana", "not-an-email")]
    [InlineData("", "")]
    public void ValidationRunsBeforeTheHandler(string name, string email)
    {
        var harness = Build();

        var act = () => harness.Controller.Register(new RegisterCustomerCommand(name, email));

        act.Should().Throw<ArgumentException>();
        harness.Email.Sent.Should().BeEmpty();
    }

    [Fact]
    public void WithoutTheValidationBehaviourTheHandlerRunsUnvalidated()
    {
        var harness = Build(withValidation: false);

        var result = harness.Controller.Register(new RegisterCustomerCommand("", "not-an-email"));

        // The point is not that this is desirable, but that the behaviour is
        // what enforces validation, and it is composed in rather than baked in.
        result.Email.Should().Be("not-an-email");
    }

    [Fact]
    public void LoggingWrapsEverySuccessAndFailureWithoutHandlersKnowing()
    {
        var harness = Build();

        harness.Controller.Register(new RegisterCustomerCommand("Ana", "ana@example.com"));
        var act = () => harness.Controller.Register(new RegisterCustomerCommand("Other", "ana@example.com"));
        act.Should().Throw<InvalidOperationException>();

        harness.Log.Messages.Should().Contain("RegisterCustomer started.");
        harness.Log.Messages.Should().Contain("RegisterCustomer succeeded.");
        harness.Log.Messages.Should().Contain(message => message.StartsWith("RegisterCustomer failed"));
    }

    [Fact]
    public void BehavioursRunOutsideInAndTheHandlerRunsLast()
    {
        var order = new List<string>();
        var store = new InMemoryCustomerStore();

        var locator = new ServiceLocator()
            .Register(
                typeof(IRequestHandler<RegisterCustomer, CustomerRegistered>),
                new RecordingHandler(store, order))
            .Register(
                typeof(IPipelineBehavior<RegisterCustomer, CustomerRegistered>),
                new RecordingBehavior("outer", order))
            .Register(
                typeof(IPipelineBehavior<RegisterCustomer, CustomerRegistered>),
                new RecordingBehavior("inner", order));

        new Mediator(locator).Send(new RegisterCustomer("Ana", "ana@example.com"));

        order.Should().Equal(
            "outer:before", "inner:before", "handler", "inner:after", "outer:after");
    }

    [Fact]
    public void AnUnregisteredRequestFailsWithAUsefulMessage()
    {
        var mediator = new Mediator(new ServiceLocator());

        var act = () => mediator.Send(new GetCustomer(Guid.NewGuid()));

        act.Should().Throw<InvalidOperationException>().WithMessage("*No handler registered for GetCustomer*");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed class FakeEmailSender : IWelcomeEmailSender
    {
        public List<(string Email, string Name)> Sent { get; } = [];

        public void SendWelcome(string email, string name) => Sent.Add((email, name));
    }

    private sealed class FakeLog : IRequestLog
    {
        public List<string> Messages { get; } = [];

        public void Write(string message) => Messages.Add(message);
    }

    private sealed class RecordingHandler : IRequestHandler<RegisterCustomer, CustomerRegistered>
    {
        private readonly ICustomerStore _store;
        private readonly List<string> _order;

        public RecordingHandler(ICustomerStore store, List<string> order)
        {
            _store = store;
            _order = order;
        }

        public CustomerRegistered Handle(RegisterCustomer request)
        {
            _order.Add("handler");

            var customer = new CustomerDetails(Guid.NewGuid(), request.Name, request.Email, true);
            _store.Save(customer);

            return new CustomerRegistered(customer.CustomerId, customer.Name, customer.Email);
        }
    }

    private sealed class RecordingBehavior : IPipelineBehavior<RegisterCustomer, CustomerRegistered>
    {
        private readonly string _name;
        private readonly List<string> _order;

        public RecordingBehavior(string name, List<string> order)
        {
            _name = name;
            _order = order;
        }

        public CustomerRegistered Handle(RegisterCustomer request, Func<CustomerRegistered> next)
        {
            _order.Add($"{_name}:before");

            var response = next();

            _order.Add($"{_name}:after");

            return response;
        }
    }
}
