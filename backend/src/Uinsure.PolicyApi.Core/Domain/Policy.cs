namespace Uinsure.PolicyApi.Core.Domain;

public sealed class Policy
{
    private readonly List<Policyholder> _policyholders = [];
    private readonly List<Payment> _payments = [];
    private readonly List<Refund> _refunds = [];

    // Required by EF Core.
    private Policy()
    {
    }

    public string Reference { get; private set; } = null!;
    public InsuranceType InsuranceType { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public decimal Amount { get; private set; }
    public bool HasClaims { get; private set; }
    public bool AutoRenew { get; private set; }
    public InsuredProperty Property { get; private set; } = null!;

    public IReadOnlyCollection<Policyholder> Policyholders => _policyholders.AsReadOnly();
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    /// <summary>
    /// Creates a newly sold policy. Input is expected to have passed
    /// <c>SellPolicyRequestValidator</c>; this method owns the derived values
    /// (end date, normalised reference, no claims) so they are set in one place.
    /// </summary>
    public static Policy Sell(
        string reference,
        InsuranceType insuranceType,
        DateOnly startDate,
        decimal amount,
        bool autoRenew,
        InsuredProperty property,
        IEnumerable<Policyholder> policyholders,
        Payment payment)
    {
        var policy = new Policy
        {
            Reference = NormaliseReference(reference),
            InsuranceType = insuranceType,
            StartDate = startDate,
            EndDate = PolicyRules.CalculateEndDate(startDate),
            Amount = amount,
            HasClaims = false,
            AutoRenew = autoRenew,
            Property = property,
        };

        policy._policyholders.AddRange(policyholders);
        policy._payments.Add(payment);

        return policy;
    }

    /// <summary>Policy references are treated as case-insensitive and stored upper-case.</summary>
    public static string NormaliseReference(string reference) => reference.Trim().ToUpperInvariant();
}
