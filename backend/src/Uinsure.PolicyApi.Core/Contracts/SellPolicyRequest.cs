using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Contracts;

// Value-type fields are nullable so that a missing field produces a clear validation
// error instead of silently defaulting (e.g. to 0001-01-01 or the first enum value).

public sealed record SellPolicyRequest
{
    public string? Reference { get; init; }
    public InsuranceType? InsuranceType { get; init; }
    public DateOnly? StartDate { get; init; }
    public decimal? Amount { get; init; }
    public bool AutoRenew { get; init; }
    public PaymentType? PaymentType { get; init; }
    public PropertyRequest? Property { get; init; }
    public IReadOnlyList<PolicyholderRequest>? Policyholders { get; init; }
}

public sealed record PolicyholderRequest
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public DateOnly? DateOfBirth { get; init; }
}

public sealed record PropertyRequest
{
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? AddressLine3 { get; init; }
    public string? Postcode { get; init; }
}
