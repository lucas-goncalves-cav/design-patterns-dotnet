namespace DesignPatterns.Creational.Factory.GoodExample;

/// <summary>
/// One place in the codebase knows how to build a gateway. Everywhere else
/// asks for one.
/// </summary>
public interface IPaymentGatewayFactory
{
    IPaymentGateway Create(GatewayProvider provider);

    IPaymentGateway CreateDefault();
}
