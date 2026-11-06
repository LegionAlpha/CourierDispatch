namespace Orders.Application.Abstractions;

public sealed class DeliveryEstimationException : Exception
{
    public DeliveryEstimationFailure Failure { get; }

    public DeliveryEstimationException(DeliveryEstimationFailure failure, Exception? innerException = null)
        : base(failure switch
        {
            DeliveryEstimationFailure.Unavailable => "Delivery estimation is temporarily unavailable.",
            DeliveryEstimationFailure.TimedOut => "Delivery estimation timed out.",
            _ => "Delivery estimation failed."
        }, innerException)
    {
        Failure = failure;
    }
}
