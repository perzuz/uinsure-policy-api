using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Contracts;

public sealed record RenewPolicyRequest
{
    /// <summary>The premium for the new term.</summary>
    public decimal? Amount { get; init; }

    /// <summary>Required for auto-renewing policies, which take a payment on renewal.</summary>
    public PaymentType? PaymentType { get; init; }
}
