using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orders.Infrastructure;
using Orders.Infrastructure.Delivery;

namespace Orders.Tests.Infrastructure.Delivery;

public class DispatchGrpcOptionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("dispatch:5200")]
    [InlineData("/dispatch")]
    [InlineData("ftp://localhost:5200")]
    public void InvalidAddress_FailsValidation(string address)
    {
        using var services = CreateServices(address, timeoutSeconds: 5);

        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<DispatchGrpcOptions>>().Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(61)]
    public void InvalidTimeout_FailsValidation(int timeoutSeconds)
    {
        using var services = CreateServices("http://localhost:5200", timeoutSeconds);

        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<DispatchGrpcOptions>>().Value);
    }

    [Theory]
    [InlineData("http://localhost:5200")]
    [InlineData("https://dispatch.example:7240")]
    public void ValidConfiguration_BindsOptions(string address)
    {
        using var services = CreateServices(address, timeoutSeconds: 5);

        var options = services.GetRequiredService<IOptions<DispatchGrpcOptions>>().Value;

        Assert.Equal(address, options.Address);
        Assert.Equal(5, options.TimeoutSeconds);
    }

    private static ServiceProvider CreateServices(string address, int timeoutSeconds)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Orders"] = "Host=localhost;Database=orders",
            ["Dispatch:Address"] = address,
            ["Dispatch:TimeoutSeconds"] = timeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }).Build();

        return new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
    }
}
