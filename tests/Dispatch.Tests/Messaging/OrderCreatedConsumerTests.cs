using Dispatch.Infrastructure.Inbox;
using Dispatch.Infrastructure.Messaging;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Orders;

namespace Dispatch.Tests.Messaging;

public class OrderCreatedConsumerTests
{
    [Fact]
    public async Task OrderCreated_IsConsumedWithItsPayloadAndCorrelation()
    {
        var inbox = new RecordingInbox();
        var handler = new RecordingHandler();
        await using var services = new ServiceCollection()
            .AddSingleton<IInboxProcessor>(inbox)
            .AddSingleton<IOrderCreatedHandler>(handler)
            .AddMassTransitTestHarness(registration =>
            {
                registration.AddConsumer<OrderCreatedConsumer>();
                registration.SetTestTimeouts(testTimeout: TimeSpan.FromSeconds(10));
            })
            .BuildServiceProvider(true);
        var harness = services.GetRequiredService<ITestHarness>();
        await harness.Start();
        var message = new OrderCreated(Guid.NewGuid(), 25.2, 55.2, 25.3, 55.3,
            12345, 12, DateTimeOffset.UtcNow);

        await harness.Bus.Publish(message, context => context.CorrelationId = message.OrderId);

        var consumer = harness.GetConsumerHarness<OrderCreatedConsumer>();
        Assert.True(await consumer.Consumed.Any<OrderCreated>());
        var received = Assert.Single(consumer.Consumed.Select<OrderCreated>()).Context;
        Assert.Equal(message, received.Message);
        Assert.Equal(message.OrderId, received.CorrelationId);
        Assert.NotNull(received.MessageId);
        Assert.Equal(received.MessageId, inbox.MessageId);
        Assert.Equal(OrderCreatedConsumer.ConsumerName, inbox.Consumer);
        Assert.Equal(message, handler.Message);
    }

    [Fact]
    public async Task OrderCreated_EmptyMessageId_IsFaultedWithoutHandling()
    {
        var inbox = new RecordingInbox();
        var handler = new RecordingHandler();
        await using var services = new ServiceCollection()
            .AddSingleton<IInboxProcessor>(inbox)
            .AddSingleton<IOrderCreatedHandler>(handler)
            .AddMassTransitTestHarness(registration =>
            {
                registration.AddConsumer<OrderCreatedConsumer>();
                registration.SetTestTimeouts(testTimeout: TimeSpan.FromSeconds(10));
            })
            .BuildServiceProvider(true);
        var harness = services.GetRequiredService<ITestHarness>();
        await harness.Start();
        var message = new OrderCreated(Guid.NewGuid(), 25, 55, 26, 56, 12000, 8, DateTimeOffset.UtcNow);

        await harness.Bus.Publish(message, context => context.MessageId = Guid.Empty);

        Assert.True(await harness.Published.Any<Fault<OrderCreated>>());
        Assert.Equal(0, inbox.Calls);
        Assert.Null(handler.Message);
    }

    private sealed class RecordingInbox : IInboxProcessor
    {
        public int Calls { get; private set; }
        public Guid MessageId { get; private set; }
        public string? Consumer { get; private set; }

        public async Task<bool> ProcessAsync(Guid messageId, string consumer,
            Func<CancellationToken, Task> handle, CancellationToken cancellationToken)
        {
            Calls++;
            MessageId = messageId;
            Consumer = consumer;
            await handle(cancellationToken);
            return true;
        }
    }

    private sealed class RecordingHandler : IOrderCreatedHandler
    {
        public OrderCreated? Message { get; private set; }

        public Task HandleAsync(OrderCreated message, CancellationToken cancellationToken)
        {
            Message = message;
            return Task.CompletedTask;
        }
    }
}
