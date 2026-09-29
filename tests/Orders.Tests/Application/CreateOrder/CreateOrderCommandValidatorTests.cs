using FluentValidation.TestHelper;
using Orders.Application.Orders.CreateOrder;

namespace Orders.Tests.Application.CreateOrder;

public class CreateOrderCommandValidatorTests
{
    private static readonly CreateOrderCommand _validCommand = new(55.75, 37.62, 55.76, 37.63);

    private readonly CreateOrderCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_HasNoErrors()
    {
        var result = _validator.TestValidate(_validCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_BoundaryCoordinates_HasNoErrors()
    {
        var command = new CreateOrderCommand(90, 180, -90, -180);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(-90.1)]
    [InlineData(90.1)]
    public void Validate_FromLatitudeOutOfRange_HasError(double latitude)
    {
        var result = _validator.TestValidate(_validCommand with { FromLatitude = latitude });

        result.ShouldHaveValidationErrorFor(command => command.FromLatitude);
    }

    [Theory]
    [InlineData(-180.1)]
    [InlineData(180.1)]
    public void Validate_ToLongitudeOutOfRange_HasError(double longitude)
    {
        var result = _validator.TestValidate(_validCommand with { ToLongitude = longitude });

        result.ShouldHaveValidationErrorFor(command => command.ToLongitude);
    }
}
