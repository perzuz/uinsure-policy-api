using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Contracts;

/// <summary>The refunds cancelling a policy on <see cref="CancellationDate"/> would give. Nothing is changed or stored.</summary>
/// <param name="HasClaims">True when the policy has claims, in which case no refund is given.</param>
/// <param name="TotalRefund">The sum of <see cref="Refunds"/>.</param>
public sealed record CancellationQuoteResponse(
    string Reference,
    DateOnly CancellationDate,
    bool HasClaims,
    decimal TotalRefund,
    IReadOnlyList<RefundQuoteResponse> Refunds)
{
    public static CancellationQuoteResponse From(Policy policy, DateOnly cancellationDate, IReadOnlyList<RefundQuote> refunds) => new(
        policy.Reference,
        cancellationDate,
        policy.HasClaims,
        refunds.Sum(r => r.Amount),
        [.. refunds.Select(r => new RefundQuoteResponse(r.TermNumber, r.Type, r.Amount))]);
}

public sealed record RefundQuoteResponse(int TermNumber, PaymentType Type, decimal Amount);
