namespace Uinsure.PolicyApi.Core.Domain;

public sealed class Payment
{
    public Payment(string paymentReference, PaymentType type, decimal amount, int termNumber)
    {
        PaymentReference = paymentReference;
        Type = type;
        Amount = amount;
        TermNumber = termNumber;
    }

    public string PaymentReference { get; private set; }
    public PaymentType Type { get; private set; }
    public decimal Amount { get; private set; }

    /// <summary>The policy term this payment paid for.</summary>
    public int TermNumber { get; private set; }
}
