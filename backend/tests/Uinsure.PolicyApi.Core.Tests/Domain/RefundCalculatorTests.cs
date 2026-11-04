using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests.Domain;

public sealed class RefundCalculatorTests
{
    // 2026-07-01 to 2027-07-01 is 365 days, so with £365.00 each day of cover is worth £1.00.
    private static readonly PolicyTerm Term = new(1, new DateOnly(2026, 7, 1), 365.00m);

    [Theory]
    [InlineData("2026-06-01")]
    [InlineData("2026-06-30")]
    public void GIVEN_a_date_before_the_term_starts_WHEN_calculating_the_refund_THEN_it_is_in_full(string cancellationDate)
    {
        // Act & Assert
        Refund(cancellationDate).ShouldBe(365.00m);
    }

    [Theory]
    [InlineData("2026-07-01")] // start date
    [InlineData("2026-07-08")]
    [InlineData("2026-07-14")] // 14th day of cover, the last day of cooling off
    public void GIVEN_a_date_within_the_14_day_cooling_off_period_WHEN_calculating_the_refund_THEN_it_is_in_full(string cancellationDate)
    {
        // Act & Assert
        Refund(cancellationDate).ShouldBe(365.00m);
    }

    [Theory]
    [InlineData("2026-07-15", 351.00)] // first day after cooling off
    [InlineData("2026-10-09", 265.00)] // 100 days used
    [InlineData("2027-06-30", 1.00)] // last day of cover
    public void GIVEN_a_date_after_cooling_off_WHEN_calculating_the_refund_THEN_the_unused_days_are_refunded_pro_rata(string cancellationDate, decimal expected)
    {
        // Act & Assert
        Refund(cancellationDate).ShouldBe(expected);
    }

    [Theory]
    [InlineData("2027-07-01")] // end date
    [InlineData("2027-08-01")]
    public void GIVEN_a_date_on_or_after_the_end_date_WHEN_calculating_the_refund_THEN_it_is_zero(string cancellationDate)
    {
        // Act & Assert
        Refund(cancellationDate).ShouldBe(0m);
    }

    [Fact]
    public void GIVEN_a_leap_year_term_WHEN_calculating_a_pro_rata_refund_THEN_366_days_are_used()
    {
        // Arrange
        // 2027-07-01 to 2028-07-01 includes 29 February 2028, so it is 366 days.
        var leapTerm = new PolicyTerm(1, new DateOnly(2027, 7, 1), 366.00m);

        // Act & Assert
        leapTerm.LengthInDays.ShouldBe(366);
        RefundCalculator.Calculate(leapTerm, 366.00m, new DateOnly(2027, 10, 9)).ShouldBe(266.00m);
    }

    [Theory]
    [InlineData("2026-07-15", 96.16)] // 100 x 351 / 365 = 96.1643...
    [InlineData("2026-12-12", 55.07)] // 100 x 201 / 365 = 55.0684...
    public void GIVEN_an_uneven_pro_rata_amount_WHEN_calculating_the_refund_THEN_it_is_rounded_to_the_nearest_penny(string cancellationDate, decimal expected)
    {
        // Act & Assert
        RefundCalculator.Calculate(Term, 100.00m, DateOnly.Parse(cancellationDate)).ShouldBe(expected);
    }

    private static decimal Refund(string cancellationDate) =>
        RefundCalculator.Calculate(Term, 365.00m, DateOnly.Parse(cancellationDate));
}
