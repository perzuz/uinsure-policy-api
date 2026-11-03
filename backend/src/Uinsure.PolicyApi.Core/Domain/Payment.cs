namespace Uinsure.PolicyApi.Core.Domain;

public sealed class Payment
{
    public Payment(string paymentReference, PaymentType type, decimal amount)
    {
        PaymentReference = paymentReference;
        Type = type;
        Amount = amount;
    }

    public string PaymentReference { get; private set; }
    public PaymentType Type { get; private set; }
    public decimal Amount { get; private set; }
}
