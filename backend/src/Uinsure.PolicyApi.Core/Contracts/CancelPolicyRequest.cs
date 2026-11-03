namespace Uinsure.PolicyApi.Core.Contracts;

public sealed record CancelPolicyRequest
{
    /// <summary>The date cover ends. Today or later, and no later than the policy end date.</summary>
    public DateOnly? CancellationDate { get; init; }
}
