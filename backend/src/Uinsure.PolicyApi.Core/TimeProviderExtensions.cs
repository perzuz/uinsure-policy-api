namespace Uinsure.PolicyApi.Core;

public static class TimeProviderExtensions
{
    /// <summary>The current date in UTC, which the API treats as "today".</summary>
    public static DateOnly GetUtcToday(this TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
