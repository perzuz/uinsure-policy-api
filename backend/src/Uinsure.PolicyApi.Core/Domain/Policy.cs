namespace Uinsure.PolicyApi.Core.Domain;

public sealed class Policy
{
    private readonly List<Policyholder> _policyholders = [];
    private readonly List<PolicyTerm> _terms = [];
    private readonly List<Payment> _payments = [];
    private readonly List<Refund> _refunds = [];

    // Required by EF Core.
    private Policy()
    {
    }

    public string Reference { get; private set; } = null!;
    public InsuranceType InsuranceType { get; private set; }
    public PolicyStatus Status { get; private set; }
    public bool HasClaims { get; private set; }
    public bool AutoRenew { get; private set; }
    public DateOnly? CancellationDate { get; private set; }
    public InsuredProperty Property { get; private set; } = null!;

    /// <summary>Incremented on every change; used to detect concurrent updates.</summary>
    public int Version { get; private set; }

    public IReadOnlyCollection<Policyholder> Policyholders => _policyholders.AsReadOnly();
    public IReadOnlyCollection<PolicyTerm> Terms => _terms.AsReadOnly();
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    /// <summary>The most recent term: the one sold, or the latest renewal.</summary>
    public PolicyTerm LatestTerm => _terms.MaxBy(t => t.TermNumber)!;

    public DateOnly StartDate => LatestTerm.StartDate;
    public DateOnly EndDate => LatestTerm.EndDate;
    public decimal Amount => LatestTerm.Amount;

    /// <summary>
    /// Creates a newly sold policy with its first term and payment. Input is expected to have
    /// passed <c>SellPolicyRequestValidator</c>; this method owns the derived values (end date,
    /// normalised reference, no claims) so they are set in one place.
    /// </summary>
    public static Policy Sell(
        string reference,
        InsuranceType insuranceType,
        DateOnly startDate,
        decimal amount,
        bool autoRenew,
        InsuredProperty property,
        IEnumerable<Policyholder> policyholders,
        string paymentReference,
        PaymentType paymentType)
    {
        var policy = new Policy
        {
            Reference = NormaliseReference(reference),
            InsuranceType = insuranceType,
            Status = PolicyStatus.Active,
            HasClaims = false,
            AutoRenew = autoRenew,
            Property = property,
        };

        var term = new PolicyTerm(1, startDate, amount);
        policy._terms.Add(term);
        policy._policyholders.AddRange(policyholders);
        policy._payments.Add(new Payment(paymentReference, paymentType, amount, term.TermNumber));

        return policy;
    }

    /// <summary>
    /// Adds the next one-year term. Renewal is allowed from 30 days before the end date up to and
    /// including the end date. A payment is taken only when the policy is set to auto renew, and
    /// it can't be by cheque.
    /// </summary>
    public DomainResult Renew(DateOnly today, decimal amount, PaymentType? paymentType, Func<string> newPaymentReference)
    {
        if (Status == PolicyStatus.Cancelled)
        {
            return DomainResult.Conflict("A cancelled policy cannot be renewed.");
        }

        var latest = LatestTerm;
        if (latest.TermNumber > 1 && latest.StartDate > today)
        {
            return DomainResult.Conflict($"The policy has already been renewed; the next term starts on {latest.StartDate:yyyy-MM-dd}.");
        }

        var windowOpens = latest.EndDate.AddDays(-PolicyRules.RenewalWindowDays);
        if (today < windowOpens)
        {
            return DomainResult.Conflict(
                $"A policy can only be renewed in the {PolicyRules.RenewalWindowDays} days before its end date. " +
                $"This policy can be renewed from {windowOpens:yyyy-MM-dd}.");
        }

        if (today > latest.EndDate)
        {
            return DomainResult.Conflict($"The policy ended on {latest.EndDate:yyyy-MM-dd} and can no longer be renewed.");
        }

        if (AutoRenew && paymentType is null)
        {
            return DomainResult.Invalid(
                "PaymentType",
                "'Payment Type' is required to renew an auto-renewing policy, because a payment is taken.");
        }

        if (paymentType is { } type && !PolicyRules.IsAllowedPaymentType(AutoRenew, type))
        {
            return DomainResult.Invalid("PaymentType", PolicyRules.AutoRenewByChequeError);
        }

        var term = new PolicyTerm(latest.TermNumber + 1, latest.EndDate, amount);
        _terms.Add(term);

        if (AutoRenew)
        {
            _payments.Add(new Payment(newPaymentReference(), paymentType!.Value, amount, term.TermNumber));
        }

        Version++;
        return DomainResult.Success;
    }

    /// <summary>
    /// Cancels the policy from <paramref name="cancellationDate"/>, refunding each paid term
    /// according to <see cref="RefundCalculator"/>, to the same method as that term's payment.
    /// </summary>
    public DomainResult Cancel(DateOnly cancellationDate, Func<string> newRefundReference)
    {
        var quote = QuoteCancellation(cancellationDate);
        if (!quote.IsSuccess)
        {
            return quote.WithoutValue();
        }

        foreach (var refund in quote.Value!)
        {
            _refunds.Add(new Refund(newRefundReference(), refund.Type, refund.Amount, refund.TermNumber));
        }

        Status = PolicyStatus.Cancelled;
        CancellationDate = cancellationDate;
        Version++;

        return DomainResult.Success;
    }

    /// <summary>
    /// The refunds that cancelling from <paramref name="cancellationDate"/> would issue, without
    /// changing the policy. <see cref="Cancel"/> uses the same calculation, so a quote always
    /// matches the cancellation it describes. A policy with claims gets no refund.
    /// </summary>
    public DomainResult<IReadOnlyList<RefundQuote>> QuoteCancellation(DateOnly cancellationDate)
    {
        if (Status == PolicyStatus.Cancelled)
        {
            return DomainResult.Conflict($"The policy has already been cancelled from {CancellationDate:yyyy-MM-dd}.");
        }

        if (cancellationDate > EndDate)
        {
            return DomainResult.Invalid(
                "CancellationDate",
                $"'Cancellation Date' cannot be after the policy end date ({EndDate:yyyy-MM-dd}).");
        }

        if (HasClaims)
        {
            return DomainResult<IReadOnlyList<RefundQuote>>.Success([]);
        }

        // Terms renewed without auto renew have no payment, so there is nothing to refund for them.
        var refunds = new List<RefundQuote>();
        foreach (var payment in _payments.OrderBy(p => p.TermNumber))
        {
            var term = _terms.Single(t => t.TermNumber == payment.TermNumber);
            var refundAmount = RefundCalculator.Calculate(term, payment.Amount, cancellationDate);

            if (refundAmount > 0)
            {
                refunds.Add(new RefundQuote(term.TermNumber, payment.Type, refundAmount));
            }
        }

        return DomainResult<IReadOnlyList<RefundQuote>>.Success(refunds);
    }

    /// <summary>
    /// Records that a claim has been made on the policy. From then on, cancelling it gives no refund.
    /// Recording a further claim changes nothing.
    /// </summary>
    public DomainResult RecordClaim()
    {
        if (Status == PolicyStatus.Cancelled)
        {
            return DomainResult.Conflict("A claim cannot be recorded on a cancelled policy.");
        }

        if (!HasClaims)
        {
            HasClaims = true;
            Version++;
        }

        return DomainResult.Success;
    }

    /// <summary>Policy references are treated as case-insensitive and stored upper-case.</summary>
    public static string NormaliseReference(string reference) => reference.Trim().ToUpperInvariant();
}
