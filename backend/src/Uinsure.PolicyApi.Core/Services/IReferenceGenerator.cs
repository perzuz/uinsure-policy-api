namespace Uinsure.PolicyApi.Core.Services;

public interface IReferenceGenerator
{
    string NewPaymentReference();
}

public sealed class ReferenceGenerator : IReferenceGenerator
{
    public string NewPaymentReference() => $"PAY-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
}
