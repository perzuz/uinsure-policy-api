namespace Uinsure.PolicyApi.Core.Domain;

/// <summary>A refund that cancelling would issue for one paid term, to the same method as its payment.</summary>
public sealed record RefundQuote(int TermNumber, PaymentType Type, decimal Amount);
