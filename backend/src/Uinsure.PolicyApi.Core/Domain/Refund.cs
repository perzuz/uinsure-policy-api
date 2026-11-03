namespace Uinsure.PolicyApi.Core.Domain;

/// <summary>A refund issued when a policy is cancelled, made to the same method as the payment it refunds.</summary>
public sealed class Refund
{
    public Refund(string refundReference, PaymentType type, decimal amount, int termNumber)
    {
        RefundReference = refundReference;
        Type = type;
        Amount = amount;
        TermNumber = termNumber;
    }

    public string RefundReference { get; private set; }
    public PaymentType Type { get; private set; }
    public decimal Amount { get; private set; }

    /// <summary>The policy term this refund is for.</summary>
    public int TermNumber { get; private set; }
}
