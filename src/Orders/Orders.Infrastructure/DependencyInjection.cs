using Dispatch.Grpc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orders.Application.Abstractions;
using Orders.Infrastructure.Delivery;
using Orders.Infrastructure.Health;
using Orders.Infrastructure.Inbox;
using Orders.Infrastructure.Messaging;
using Orders.Infrastructure.Outbox;
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
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IInboxProcessor, InboxProcessor>();
        services.AddScoped<ICourierAssignedHandler, CourierAssignedHandler>();
        services.AddOptions<DispatchGrpcOptions>()
            .Bind(configuration.GetSection(DispatchGrpcOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.Address, UriKind.Absolute, out var address)
                && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps),
                "Dispatch:Address must be an absolute HTTP or HTTPS URL.")
            .Validate(options => options.TimeoutSeconds is >= 1 and <= 60,
                "Dispatch:TimeoutSeconds must be between 1 and 60 seconds.")
            .ValidateOnStart();

        services.AddGrpcClient<DispatchService.DispatchServiceClient>((serviceProvider, options) =>
        {
            var dispatch = serviceProvider.GetRequiredService<IOptions<DispatchGrpcOptions>>().Value;
            options.Address = new Uri(dispatch.Address);
        });
        services.AddTransient<IDeliveryEstimator, GrpcDeliveryEstimator>();
        services.AddSingleton<TimeProvider>(new MicrosecondPrecisionTimeProvider(TimeProvider.System));

        services.AddHealthChecks()
            .AddDbContextCheck<OrdersDbContext>(name: "postgres", tags: [HealthCheckTags.Ready]);

        return services;
    }
}
