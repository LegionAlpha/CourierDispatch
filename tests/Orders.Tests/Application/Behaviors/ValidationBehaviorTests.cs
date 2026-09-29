using FluentValidation;
using Orders.Application.Behaviors;

namespace Orders.Tests.Application.Behaviors;

public class ValidationBehaviorTests
{
    private readonly InlineValidator<TestRequest> _validator = new();

    public ValidationBehaviorTests()
    {
        _validator.RuleFor(request => request.Value).GreaterThan(0);
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsAndDoesNotCallNext()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([_validator]);
        var nextCalled = false;

        await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new TestRequest(0),
            _ =>
            {
                nextCalled = true;
                return Task.FromResult("handled");
            },
            CancellationToken.None));

        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsResultOfNext()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([_validator]);

        var result = await behavior.Handle(new TestRequest(1), _ => Task.FromResult("handled"), CancellationToken.None);

        Assert.Equal("handled", result);
    }

    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([]);

        var result = await behavior.Handle(new TestRequest(0), _ => Task.FromResult("handled"), CancellationToken.None);

        Assert.Equal("handled", result);
    }

    public sealed record TestRequest(int Value);
}
