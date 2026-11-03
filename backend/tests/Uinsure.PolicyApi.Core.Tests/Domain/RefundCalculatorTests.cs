using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests.Domain;

public sealed class RefundCalculatorTests
{
    // 2026-07-01 to 2027-07-01 is 365 days, so with £365.00 each day of cover is worth £1.00.
    private static readonly PolicyTerm Term = new(1, new DateOnly(2026, 7, 1), 365.00m);

    [Theory]
    [InlineData("2026-06-01")]
    [InlineData("2026-06-30")]
    public void Cancelling_before_the_term_starts_refunds_in_full(string cancellationDate)
    {
        Refund(cancellationDate).ShouldBe(365.00m);
    }

    [Theory]
    [InlineData("2026-07-01")] // start date
    [InlineData("2026-07-08")]
    [InlineData("2026-07-14")] // 14th day of cover, the last day of cooling off
    public void Cancelling_within_the_14_day_cooling_off_period_refunds_in_full(string cancellationDate)
    {
        Refund(cancellationDate).ShouldBe(365.00m);
    }

    [Theory]
    [InlineData("2026-07-15", 351.00)] // first day after cooling off
    [InlineData("2026-10-09", 265.00)] // 100 days used
    [InlineData("2027-06-30", 1.00)] // last day of cover
    public void Cancelling_after_cooling_off_refunds_the_unused_days_pro_rata(string cancellationDate, decimal expected)
    {
        Refund(cancellationDate).ShouldBe(expected);
    }

    [Theory]
    [InlineData("2027-07-01")] // end date
    [InlineData("2027-08-01")]
    public void Cancelling_on_or_after_the_end_date_refunds_nothing(string cancellationDate)
    {
        Refund(cancellationDate).ShouldBe(0m);
    }

    [Fact]
    public void Pro_rata_uses_the_actual_length_of_a_leap_year_term()
    {
        // 2027-07-01 to 2028-07-01 includes 29 February 2028, so it is 366 days.
        var leapTerm = new PolicyTerm(1, new DateOnly(2027, 7, 1), 366.00m);

        leapTerm.LengthInDays.ShouldBe(366);
        RefundCalculator.Calculate(leapTerm, 366.00m, new DateOnly(2027, 10, 9)).ShouldBe(266.00m);
    }

    [Theory]
    [InlineData("2026-07-15", 96.16)] // 100 x 351 / 365 = 96.1643...
    [InlineData("2026-12-12", 55.07)] // 100 x 201 / 365 = 55.0684...
    public void Pro_rata_refunds_are_rounded_to_the_nearest_penny(string cancellationDate, decimal expected)
    {
        RefundCalculator.Calculate(Term, 100.00m, DateOnly.Parse(cancellationDate)).ShouldBe(expected);
    }

    private static decimal Refund(string cancellationDate) =>
        RefundCalculator.Calculate(Term, 365.00m, DateOnly.Parse(cancellationDate));
}
