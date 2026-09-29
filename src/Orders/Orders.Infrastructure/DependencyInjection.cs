using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Application.Abstractions;
using Orders.Infrastructure.Delivery;
using Orders.Infrastructure.Persistence;
using Orders.Infrastructure.Time;

namespace Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Orders")
            ?? throw new InvalidOperationException("Connection string 'Orders' is not configured.");

        services.AddDbContext<OrdersDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<OrdersDbContext>());
        services.AddSingleton<IDeliveryEstimator, StubDeliveryEstimator>();
        services.AddSingleton<TimeProvider>(new MicrosecondPrecisionTimeProvider(TimeProvider.System));

        return services;
    }
}
