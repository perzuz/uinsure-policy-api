namespace Uinsure.PolicyApi.Core.Domain;

/// <summary>
/// A refund issued against a policy. Part of the stored policy information from the
/// specification; refunds are created when cancellation is implemented.
/// </summary>
public sealed class Refund
{
    public Refund(string refundReference, PaymentType type, decimal amount)
    {
        RefundReference = refundReference;
        Type = type;
        Amount = amount;
    }

    public string RefundReference { get; private set; }
    public PaymentType Type { get; private set; }
    public decimal Amount { get; private set; }
}
