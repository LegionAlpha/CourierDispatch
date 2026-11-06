using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Dispatch.Infrastructure.Sagas;
using Dispatch.Infrastructure.Outbox;
using Shared.Contracts;
using Shared.Contracts.Dispatch;

namespace Dispatch.Infrastructure.Messaging;

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
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(30);
            options.StopTimeout = TimeSpan.FromSeconds(10);
        });
        services.AddMassTransit(registration =>
        {
            registration.AddConsumer<OrderCreatedConsumer>();
            registration.AddAssignCourierSaga();
            registration.UsingRabbitMq((context, bus) =>
            {
                var options = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                bus.Host(options.Host, options.Port, options.VirtualHost, host =>
                {
                    host.Username(options.Username);
                    host.Password(options.Password);
                });
                bus.ReceiveEndpoint(OrderCreatedConsumer.ConsumerName, endpoint =>
                {
                    endpoint.UseMessageRetry(retry =>
                    {
                        retry.Handle<NpgsqlException>(exception => exception.IsTransient);
                        retry.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5));
                    });
                    endpoint.ConfigureConsumer<OrderCreatedConsumer>(context);
                });
                bus.ReceiveEndpoint(AssignCourierSagaRegistration.QueueName,
                    endpoint => endpoint.ConfigureAssignCourierSaga(context));
                // Declare a durable destination even on a first start before Orders has connected.
                bus.Publish<CourierAssigned>(topology => topology.BindQueue(
                    bus.MessageTopology.GetMessageTopology<CourierAssigned>().EntityName,
                    QueueNames.OrdersCourierAssigned, binding =>
                    {
                        binding.Durable = true;
                        binding.AutoDelete = false;
                    }));
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
