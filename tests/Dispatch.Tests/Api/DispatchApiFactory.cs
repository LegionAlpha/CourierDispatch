using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Dispatch.Infrastructure.Outbox;

namespace Dispatch.Tests.Api;

public sealed class DispatchApiFactory : WebApplicationFactory<Program>
{
    public DispatchApiFactory()
    {
        // Exercise real HTTP/2 on an available port, without requiring a local TLS certificate.
        UseKestrel(0);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Dispatch", "Host=127.0.0.1;Database=dispatch_tests");
        builder.ConfigureServices(services =>
        {
            services.AddMassTransitTestHarness();
            var worker = services.Single(descriptor => descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(OutboxPublisherWorker));
            services.Remove(worker);
        });
    }
}
