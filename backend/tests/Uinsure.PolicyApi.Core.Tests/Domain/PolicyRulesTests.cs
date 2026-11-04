using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests.Domain;

public sealed class PolicyRulesTests
{
    [Theory]
    [InlineData("2026-06-15", "2027-06-15")]
    [InlineData("2026-01-01", "2027-01-01")]
    [InlineData("2026-12-31", "2027-12-31")]
    [InlineData("2028-02-29", "2029-02-28")]
    public void GIVEN_a_start_date_WHEN_calculating_the_end_date_THEN_it_is_one_calendar_year_later(string start, string expectedEnd)
    {
        // Act & Assert
        PolicyRules.CalculateEndDate(DateOnly.Parse(start)).ShouldBe(DateOnly.Parse(expectedEnd));
    }

    [Theory]
    [InlineData("2010-06-15", "2026-06-15", 16)] // 16th birthday
    [InlineData("2010-06-16", "2026-06-15", 15)] // day before 16th birthday
    [InlineData("2010-06-14", "2026-06-15", 16)]
    [InlineData("1980-01-01", "2026-06-15", 46)]
    [InlineData("2026-06-15", "2026-06-15", 0)]
    public void GIVEN_a_date_of_birth_WHEN_calculating_age_THEN_it_is_whole_years_on_the_given_date(string dateOfBirth, string onDate, int expectedAge)
    {
        // Act & Assert
        PolicyRules.AgeOn(DateOnly.Parse(dateOfBirth), DateOnly.Parse(onDate)).ShouldBe(expectedAge);
    }

    [Theory]
    [InlineData("2024-02-28", 15)] // leap year: day before 16th birthday
    [InlineData("2024-02-29", 16)] // leap year: 16th birthday
    [InlineData("2025-02-28", 16)] // non-leap year: not yet 17 on 28 Feb
    [InlineData("2025-03-01", 17)] // non-leap year: birthday treated as 1 March
    public void GIVEN_a_29_february_birthday_WHEN_calculating_age_in_a_non_leap_year_THEN_the_birthday_is_1_march(string onDate, int expectedAge)
    {
        // Arrange
        var bornOnLeapDay = new DateOnly(2008, 2, 29);

        // Act & Assert
        PolicyRules.AgeOn(bornOnLeapDay, DateOnly.Parse(onDate)).ShouldBe(expectedAge);
    }
}
