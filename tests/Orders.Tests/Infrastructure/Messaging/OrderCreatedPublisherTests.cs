using System.Text.Json;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orders.Infrastructure.Messaging;
using Orders.Infrastructure.Outbox;
using Shared.Contracts.Orders;

namespace Orders.Tests.Infrastructure.Messaging;

public class OrderCreatedPublisherTests
{
    [Fact]
    public async Task PublishAsync_RetryRetainsMessageIdAndOrderCorrelation()
    {
        await using var services = new ServiceCollection()
            .AddMassTransitTestHarness(registration => registration.SetTestTimeouts(testTimeout: TimeSpan.FromSeconds(10)))
            .BuildServiceProvider(true);
        var harness = services.GetRequiredService<ITestHarness>();
        await harness.Start();
        var observer = new PublishObserver();
        using var connection = harness.Bus.ConnectPublishObserver(observer);
        await using var scope = services.CreateAsyncScope();
        var publisher = new MassTransitOutboxPublisher(
            scope.ServiceProvider.GetRequiredService<IPublishEndpoint>(), Options.Create(new RabbitMqOptions()));
        var message = new OrderCreated(Guid.NewGuid(), 25.2, 55.2, 25.3, 55.3,
            12345, 12, DateTimeOffset.UtcNow);

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = OutboxWriter.OrderCreatedType,
            Payload = JsonSerializer.Serialize(message, OutboxWriter.SerializerOptions)
        };
        await publisher.PublishAsync(outboxMessage, CancellationToken.None);
        await publisher.PublishAsync(outboxMessage, CancellationToken.None);

        Assert.True(await harness.Published.Any<OrderCreated>());
        Assert.Equal(2, observer.Messages.Count);
        Assert.All(observer.Messages, entry =>
        {
            Assert.Equal(message, entry.Message);
            Assert.Equal(message.OrderId, entry.CorrelationId);
            Assert.Equal(outboxMessage.Id, entry.MessageId);
        });
    }

    private sealed class PublishObserver : IPublishObserver
    {
        public List<(OrderCreated Message, Guid? MessageId, Guid? CorrelationId)> Messages { get; } = [];

        public Task PrePublish<T>(PublishContext<T> context) where T : class => Task.CompletedTask;

        public Task PostPublish<T>(PublishContext<T> context) where T : class
        {
            if (context.Message is OrderCreated message)
            {
                Messages.Add((message, context.MessageId, context.CorrelationId));
            }

            return Task.CompletedTask;
        }

        public Task PublishFault<T>(PublishContext<T> context, Exception exception) where T : class => Task.CompletedTask;
    }
}
