using Microsoft.Extensions.DependencyInjection;

namespace DesignPatterns.Behavioral.Strategy.GoodExample;

/// <summary>
/// How this is wired in a real application. Registering the strategies as a
/// collection lets the container inject every implementation, so the processor
/// never references a concrete type.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentStrategies(this IServiceCollection services)
    {
        services.AddScoped<IPaymentStrategy, PixPaymentStrategy>();
        services.AddScoped<IPaymentStrategy, CreditCardPaymentStrategy>();
        services.AddScoped<IPaymentStrategy, BoletoPaymentStrategy>();
        services.AddScoped<IPaymentStrategy, BankTransferPaymentStrategy>();
        services.AddScoped<PaymentProcessor>();

        return services;
    }
}
