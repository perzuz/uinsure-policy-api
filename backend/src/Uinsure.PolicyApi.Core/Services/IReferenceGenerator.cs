namespace Uinsure.PolicyApi.Core.Services;

public interface IReferenceGenerator
{
    string NewPaymentReference();

    string NewRefundReference();
}

public sealed class ReferenceGenerator : IReferenceGenerator
{
    public string NewPaymentReference() => NewReference("PAY");

    public string NewRefundReference() => NewReference("REF");

    private static string NewReference(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
}
