namespace Uinsure.PolicyApi.Functions;

/// <summary>
/// Local demo aid: reports a fixed date (keeping the real time of day) so date-dependent rules,
/// such as the renewal window, can be tried without waiting. Enabled only when the
/// <c>TimeOverride__Today</c> setting is present; see Program.cs.
/// </summary>
public sealed class FixedDateTimeProvider(DateOnly today) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() =>
        new(today.ToDateTime(TimeOnly.FromTimeSpan(DateTime.UtcNow.TimeOfDay)), TimeSpan.Zero);
}
