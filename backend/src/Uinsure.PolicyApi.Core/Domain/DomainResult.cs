namespace Uinsure.PolicyApi.Core.Domain;

/// <summary>
/// Outcome of an operation on a policy. A failure either conflicts with the policy's current
/// state (e.g. already cancelled) or is an input that is invalid for this policy, in which case
/// <see cref="Field"/> names the request field at fault.
/// </summary>
public sealed class DomainResult
{
    public static readonly DomainResult Success = new(null, null);

    private DomainResult(string? error, string? field)
    {
        Error = error;
        Field = field;
    }

    public bool IsSuccess => Error is null;
    public string? Error { get; }
    public string? Field { get; }

    public static DomainResult Conflict(string error) => new(error, null);

    public static DomainResult Invalid(string field, string error) => new(error, field);
}
