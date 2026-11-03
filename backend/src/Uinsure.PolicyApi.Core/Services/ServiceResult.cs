namespace Uinsure.PolicyApi.Core.Services;

public enum ServiceResultStatus
{
    Success,
    ValidationFailed,
    Conflict,
    NotFound,
}

/// <summary>
/// Outcome of an application operation. Expected business outcomes (invalid input,
/// duplicates, missing policies) are modelled as results rather than exceptions so the
/// HTTP layer can map them to status codes explicitly.
/// </summary>
public sealed class ServiceResult<T>
{
    private ServiceResult(
        ServiceResultStatus status,
        T? value = default,
        string? error = null,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
    {
        Status = status;
        Value = value;
        Error = error;
        ValidationErrors = validationErrors ?? new Dictionary<string, string[]>();
    }

    public ServiceResultStatus Status { get; }
    public T? Value { get; }
    public string? Error { get; }
    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }

    public static ServiceResult<T> Success(T value) => new(ServiceResultStatus.Success, value);

    public static ServiceResult<T> ValidationFailed(IReadOnlyDictionary<string, string[]> errors) =>
        new(ServiceResultStatus.ValidationFailed, validationErrors: errors);

    public static ServiceResult<T> Conflict(string error) => new(ServiceResultStatus.Conflict, error: error);

    public static ServiceResult<T> NotFound(string error) => new(ServiceResultStatus.NotFound, error: error);
}
