using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Contracts;

public sealed record PolicyResponse(
    string Reference,
    InsuranceType InsuranceType,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal Amount,
    bool HasClaims,
    bool AutoRenew,
    PropertyResponse Property,
    IReadOnlyList<PolicyholderResponse> Policyholders,
    IReadOnlyList<PaymentResponse> Payments,
    IReadOnlyList<RefundResponse> Refunds)
{
    public static PolicyResponse From(Policy policy) => new(
        policy.Reference,
        policy.InsuranceType,
        policy.StartDate,
        policy.EndDate,
        policy.Amount,
        policy.HasClaims,
        policy.AutoRenew,
        new PropertyResponse(
            policy.Property.AddressLine1,
            policy.Property.AddressLine2,
            policy.Property.AddressLine3,
            policy.Property.Postcode),
        [.. policy.Policyholders.Select(h => new PolicyholderResponse(h.FirstName, h.LastName, h.DateOfBirth))],
        [.. policy.Payments.Select(p => new PaymentResponse(p.PaymentReference, p.Type, p.Amount))],
        [.. policy.Refunds.Select(r => new RefundResponse(r.RefundReference, r.Type, r.Amount))]);
}

public sealed record PropertyResponse(string AddressLine1, string? AddressLine2, string? AddressLine3, string Postcode);

public sealed record PolicyholderResponse(string FirstName, string LastName, DateOnly DateOfBirth);

public sealed record PaymentResponse(string PaymentReference, PaymentType Type, decimal Amount);

public sealed record RefundResponse(string RefundReference, PaymentType Type, decimal Amount);
