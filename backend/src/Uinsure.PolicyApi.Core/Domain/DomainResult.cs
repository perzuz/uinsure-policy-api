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

/// <summary>A <see cref="DomainResult"/> that carries a value on success.</summary>
public sealed class DomainResult<T>
{
    private readonly DomainResult _outcome;

    private DomainResult(DomainResult outcome, T? value)
    {
        _outcome = outcome;
        Value = value;
    }

    public bool IsSuccess => _outcome.IsSuccess;
    public string? Error => _outcome.Error;
    public string? Field => _outcome.Field;
    public T? Value { get; }

    public static DomainResult<T> Success(T value) => new(DomainResult.Success, value);

    /// <summary>Lets a method return <c>DomainResult.Conflict(...)</c> or <c>DomainResult.Invalid(...)</c> directly.</summary>
    public static implicit operator DomainResult<T>(DomainResult failure) =>
        failure.IsSuccess
            ? throw new InvalidOperationException("A successful result must carry a value; use DomainResult<T>.Success.")
            : new(failure, default);

    /// <summary>The outcome without its value, for callers that only need to know whether it worked.</summary>
    public DomainResult WithoutValue() => _outcome;
}
