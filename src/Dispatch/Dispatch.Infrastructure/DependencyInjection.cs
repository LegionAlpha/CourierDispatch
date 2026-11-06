using Dispatch.Application.Abstractions;
using Dispatch.Infrastructure.Delivery;
using Dispatch.Infrastructure.Inbox;
using Dispatch.Infrastructure.Messaging;
using Dispatch.Infrastructure.Persistence;
using Dispatch.Infrastructure.Sagas;
using Dispatch.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatch.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Dispatch")
            ?? throw new InvalidOperationException("Connection string 'Dispatch' is not configured.");
        services.AddDbContext<DispatchDbContext>(options => options
            .UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IInboxProcessor, InboxProcessor>();
        services.AddScoped<IOrderCreatedHandler, OrderCreatedHandler>();
        services.AddSingleton<IDeliveryEstimator, DistanceBasedDeliveryEstimator>();
        services.AddSingleton<ICourierSelector, StubCourierSelector>();
        services.AddScoped<AssignCourierActivity>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddHealthChecks()
            .AddDbContextCheck<DispatchDbContext>(name: "postgres", tags: ["ready"]);

        return services;
    }
}
