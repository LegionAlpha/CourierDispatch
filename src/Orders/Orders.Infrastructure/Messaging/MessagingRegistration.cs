using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orders.Infrastructure.Outbox;
using Npgsql;
using Shared.Contracts;

namespace Orders.Infrastructure.Messaging;

public static class MessagingRegistration
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection("RabbitMq"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host) && options.Port > 0
                && !string.IsNullOrWhiteSpace(options.VirtualHost) && !string.IsNullOrWhiteSpace(options.Username)
                && !string.IsNullOrWhiteSpace(options.Password), "RabbitMq connection settings are required.")
            .Validate(options => options.PublishTimeoutSeconds is >= 1 and <= 60,
                "RabbitMq:PublishTimeoutSeconds must be between 1 and 60 seconds.")
            .ValidateOnStart();

        services.Configure<MassTransitHostOptions>(options =>
        {
            // Orders can accept writes while RabbitMQ is offline; the outbox retains events.
            options.WaitUntilStarted = false;
            // Keep reconnecting even if the broker is unavailable for a long time at startup.
            options.StartTimeout = null;
            options.StopTimeout = TimeSpan.FromSeconds(10);
        });
        services.AddMassTransit(registration =>
        {
            registration.AddConsumer<CourierAssignedConsumer>();
            registration.UsingRabbitMq((context, bus) =>
            {
                var options = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                bus.Host(options.Host, options.Port, options.VirtualHost, host =>
                {
                    host.Username(options.Username);
                    host.Password(options.Password);
                });
                bus.ReceiveEndpoint(QueueNames.OrdersCourierAssigned, endpoint =>
                {
                    endpoint.UseMessageRetry(retry =>
                    {
                        retry.Handle<NpgsqlException>(exception => exception.IsTransient);
                        retry.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5));
                    });
                    endpoint.ConfigureConsumer<CourierAssignedConsumer>(context);
                });
            });
        });
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection("Outbox"))
            .Validate(options => options.PollIntervalSeconds is >= 1 and <= 60,
                "Outbox:PollIntervalSeconds must be between 1 and 60 seconds.")
            .Validate(options => options.BatchSize is >= 1 and <= 1000,
                "Outbox:BatchSize must be between 1 and 1000.")
            .Validate(options => options.RetryDelaySeconds >= 1
                && options.MaxRetryDelaySeconds >= options.RetryDelaySeconds
                && options.MaxRetryDelaySeconds <= 3600,
                "Outbox retry delays must be positive, increasing, and at most 3600 seconds.")
            .ValidateOnStart();
        services.AddScoped<IOutboxMessagePublisher, MassTransitOutboxPublisher>();
        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxPublisherWorker>();

        return services;
    }
}
