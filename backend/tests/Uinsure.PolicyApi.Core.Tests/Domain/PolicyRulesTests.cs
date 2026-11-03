using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests.Domain;

public sealed class PolicyRulesTests
{
    [Theory]
    [InlineData("2026-06-15", "2027-06-15")]
    [InlineData("2026-01-01", "2027-01-01")]
    [InlineData("2026-12-31", "2027-12-31")]
    [InlineData("2028-02-29", "2029-02-28")]
    public void End_date_is_one_calendar_year_after_start_date(string start, string expectedEnd)
    {
        PolicyRules.CalculateEndDate(DateOnly.Parse(start)).ShouldBe(DateOnly.Parse(expectedEnd));
    }

    [Theory]
    [InlineData("2010-06-15", "2026-06-15", 16)] // 16th birthday
    [InlineData("2010-06-16", "2026-06-15", 15)] // day before 16th birthday
    [InlineData("2010-06-14", "2026-06-15", 16)]
    [InlineData("1980-01-01", "2026-06-15", 46)]
    [InlineData("2026-06-15", "2026-06-15", 0)]
    public void Age_is_whole_years_on_the_given_date(string dateOfBirth, string onDate, int expectedAge)
    {
        PolicyRules.AgeOn(DateOnly.Parse(dateOfBirth), DateOnly.Parse(onDate)).ShouldBe(expectedAge);
    }

    [Theory]
    [InlineData("2024-02-28", 15)] // leap year: day before 16th birthday
    [InlineData("2024-02-29", 16)] // leap year: 16th birthday
    [InlineData("2025-02-28", 16)] // non-leap year: not yet 17 on 28 Feb
    [InlineData("2025-03-01", 17)] // non-leap year: birthday treated as 1 March
    public void Leap_day_birthdays_age_on_1_march_in_non_leap_years(string onDate, int expectedAge)
    {
        var bornOnLeapDay = new DateOnly(2008, 2, 29);

        PolicyRules.AgeOn(bornOnLeapDay, DateOnly.Parse(onDate)).ShouldBe(expectedAge);
    }
}
