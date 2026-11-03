using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Contracts;

/// <param name="StartDate">Start of the latest term.</param>
/// <param name="EndDate">End of the latest term.</param>
/// <param name="Amount">Premium for the latest term.</param>
public sealed record PolicyResponse(
    string Reference,
    InsuranceType InsuranceType,
    PolicyStatus Status,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal Amount,
    bool HasClaims,
    bool AutoRenew,
    DateOnly? CancellationDate,
    PropertyResponse Property,
    IReadOnlyList<PolicyholderResponse> Policyholders,
    IReadOnlyList<PolicyTermResponse> Terms,
    IReadOnlyList<PaymentResponse> Payments,
    IReadOnlyList<RefundResponse> Refunds)
{
    public static PolicyResponse From(Policy policy) => new(
        policy.Reference,
        policy.InsuranceType,
        policy.Status,
        policy.StartDate,
        policy.EndDate,
        policy.Amount,
        policy.HasClaims,
        policy.AutoRenew,
        policy.CancellationDate,
        new PropertyResponse(
            policy.Property.AddressLine1,
            policy.Property.AddressLine2,
            policy.Property.AddressLine3,
            policy.Property.Postcode),
        [.. policy.Policyholders.Select(h => new PolicyholderResponse(h.FirstName, h.LastName, h.DateOfBirth))],
        [.. policy.Terms.OrderBy(t => t.TermNumber).Select(t => new PolicyTermResponse(t.TermNumber, t.StartDate, t.EndDate, t.Amount))],
        [.. policy.Payments.OrderBy(p => p.TermNumber).Select(p => new PaymentResponse(p.PaymentReference, p.Type, p.Amount, p.TermNumber))],
        [.. policy.Refunds.OrderBy(r => r.TermNumber).Select(r => new RefundResponse(r.RefundReference, r.Type, r.Amount, r.TermNumber))]);
}

public sealed record PropertyResponse(string AddressLine1, string? AddressLine2, string? AddressLine3, string Postcode);

public sealed record PolicyholderResponse(string FirstName, string LastName, DateOnly DateOfBirth);

public sealed record PolicyTermResponse(int TermNumber, DateOnly StartDate, DateOnly EndDate, decimal Amount);

public sealed record PaymentResponse(string PaymentReference, PaymentType Type, decimal Amount, int TermNumber);

public sealed record RefundResponse(string RefundReference, PaymentType Type, decimal Amount, int TermNumber);
