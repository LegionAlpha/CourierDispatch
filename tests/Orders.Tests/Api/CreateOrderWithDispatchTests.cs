extern alias DispatchServer;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grpc.Core;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Orders.Api.Controllers;
using Orders.Application.Abstractions;
using Orders.Infrastructure.Outbox;
using Orders.Tests.Application.Fakes;
using Shared.Contracts.Orders;
using DispatchEstimate = Dispatch.Application.Abstractions.DeliveryEstimate;
using DispatchEstimator = Dispatch.Application.Abstractions.IDeliveryEstimator;
using DispatchGeoPoint = Dispatch.Domain.Delivery.GeoPoint;
using DispatchProgram = DispatchServer::Program;

namespace Orders.Tests.Api;

public class CreateOrderWithDispatchTests
{
    private static readonly object _request = new
    {
        FromLatitude = 0,
        FromLongitude = 0,
        ToLatitude = 0,
        ToLongitude = 1
    };

    [Fact]
    public async Task Create_RealDispatch_SavesCalculatedPriceAndEta()
    {
        await using var dispatch = CreateDispatch();
        using var dispatchClient = dispatch.CreateClient();
        var repository = new FakeOrderRepository();
        var unitOfWork = new SpyUnitOfWork();
        await using var orders = CreateOrders(dispatchClient.BaseAddress!, repository, unitOfWork);
        using var client = orders.CreateClient();

        using var response = await client.PostAsJsonAsync("/orders", _request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var saved = Assert.Single(repository.Orders);
        Assert.Equal(343586, saved.PriceMinor);
        Assert.Equal(339, saved.EtaMinutes);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(saved.Id, json.GetProperty("id").GetGuid());
        Assert.Equal(saved.PriceMinor, json.GetProperty("priceMinor").GetInt64());
        Assert.Equal(saved.EtaMinutes, json.GetProperty("etaMinutes").GetInt32());
        var staged = Assert.Single(orders.Services.GetRequiredService<FakeOutboxWriter>().Messages);
        Assert.Equal(saved.Id, staged.OrderId);
        Assert.Empty(orders.Services.GetRequiredService<ITestHarness>().Published.Select<OrderCreated>());
    }

    [Theory]
    [InlineData(StatusCode.Unavailable, HttpStatusCode.ServiceUnavailable)]
    [InlineData(StatusCode.Internal, HttpStatusCode.BadGateway)]
    [InlineData(StatusCode.InvalidArgument, HttpStatusCode.BadGateway)]
    public async Task Create_DispatchError_ReturnsProblemDetailsWithoutSaving(StatusCode status, HttpStatusCode expectedStatus)
    {
        var estimator = new ControlledEstimator(_ => Task.FromException<DispatchEstimate>(
            new RpcException(new Status(status, "private-dispatch-detail"))));

        await AssertFailureAsync(estimator, expectedStatus);
    }

    [Fact]
    public async Task Create_DispatchTooSlow_ReturnsGatewayTimeoutWithoutSaving()
    {
        var estimator = new ControlledEstimator(async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new DispatchEstimate(10000, 5, 0);
        });

        await AssertFailureAsync(estimator, HttpStatusCode.GatewayTimeout);
    }

    [Fact]
    public async Task Create_DispatchReturnsInvalidEstimate_ReturnsBadGatewayWithoutSaving()
    {
        var estimator = new ControlledEstimator(_ => Task.FromResult(new DispatchEstimate(-1, 0, 0)));

        await AssertFailureAsync(estimator, HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task Create_WithoutPublishing_ReturnsCreatedAndStagesEvent()
    {
        await using var dispatch = CreateDispatch();
        using var dispatchClient = dispatch.CreateClient();
        var repository = new FakeOrderRepository();
        var unitOfWork = new SpyUnitOfWork();
        await using var orders = CreateOrders(dispatchClient.BaseAddress!, repository, unitOfWork);
        using var client = orders.CreateClient();

        using var response = await client.PostAsJsonAsync("/orders", _request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(Assert.Single(repository.Orders).Id, json.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        Assert.Single(orders.Services.GetRequiredService<FakeOutboxWriter>().Messages);
        Assert.Empty(orders.Services.GetRequiredService<ITestHarness>().Published.Select<OrderCreated>());
    }

    private static async Task AssertFailureAsync(DispatchEstimator estimator, HttpStatusCode expectedStatus)
    {
        await using var dispatch = CreateDispatch(estimator);
        using var dispatchClient = dispatch.CreateClient();
        var repository = new FakeOrderRepository();
        var unitOfWork = new SpyUnitOfWork();
        await using var orders = CreateOrders(dispatchClient.BaseAddress!, repository, unitOfWork);
        using var client = orders.CreateClient();

        using var response = await client.PostAsJsonAsync("/orders", _request);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-dispatch-detail", body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal((int)expectedStatus, json.RootElement.GetProperty("status").GetInt32());
        Assert.Empty(repository.Orders);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static WebApplicationFactory<DispatchProgram> CreateDispatch(DispatchEstimator? estimator = null)
    {
        var factory = new WebApplicationFactory<DispatchProgram>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Dispatch", "Host=127.0.0.1;Database=dispatch_tests");
            builder.ConfigureServices(services =>
            {
                services.AddMassTransitTestHarness();
                var worker = services.Single(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType?.FullName == "Dispatch.Infrastructure.Outbox.OutboxPublisherWorker");
                services.Remove(worker);
            });
            if (estimator is not null)
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DispatchEstimator>();
                    services.AddSingleton(estimator);
                });
            }
        });
        factory.UseKestrel(0);
        return factory;
    }

    private static WebApplicationFactory<OrdersController> CreateOrders(
        Uri dispatchAddress, FakeOrderRepository repository, SpyUnitOfWork unitOfWork) =>
        new WebApplicationFactory<OrdersController>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Orders", "Host=localhost;Database=orders");
            builder.UseSetting("Dispatch:Address", dispatchAddress.ToString());
            builder.UseSetting("Dispatch:TimeoutSeconds", "1");
            builder.ConfigureServices(services =>
            {
                services.AddMassTransitTestHarness();
                services.RemoveAll<IOrderRepository>();
                services.RemoveAll<IUnitOfWork>();
                services.AddSingleton<IOrderRepository>(repository);
                services.AddSingleton<IUnitOfWork>(unitOfWork);
                services.RemoveAll<IOutboxWriter>();
                services.AddSingleton<FakeOutboxWriter>();
                services.AddSingleton<IOutboxWriter>(provider => provider.GetRequiredService<FakeOutboxWriter>());
                var worker = services.Single(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType == typeof(OutboxPublisherWorker));
                services.Remove(worker);
            });
        });

    private sealed class ControlledEstimator(Func<CancellationToken, Task<DispatchEstimate>> estimate) : DispatchEstimator
    {
        public Task<DispatchEstimate> EstimateAsync(DispatchGeoPoint from, DispatchGeoPoint to, CancellationToken cancellationToken) =>
            estimate(cancellationToken);
    }
}
