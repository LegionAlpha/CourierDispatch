using FluentValidation;

namespace Orders.Application.Orders.CreateOrder;

internal sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.FromLatitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.FromLongitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.ToLatitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.ToLongitude).InclusiveBetween(-180, 180);
    }
}
