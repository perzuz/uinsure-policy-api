using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests.Domain;

public sealed class PolicyCancellationQuoteTests
{
    [Theory]
    [InlineData("2026-06-20")] // before the start date
    [InlineData("2026-07-10")] // cooling off
    [InlineData("2026-10-09")] // pro rata
    [InlineData("2027-07-01")] // the end date: nothing to refund
    public void GIVEN_a_sold_policy_WHEN_quoting_a_cancellation_THEN_the_quote_matches_the_refunds_cancelling_on_that_date_gives(string date)
    {
        // Arrange
        var cancellationDate = DateOnly.Parse(date);
        var quoted = TestPolicies.Sold();
        var cancelled = TestPolicies.Sold();

        // Act
        var quote = quoted.QuoteCancellation(cancellationDate);
        cancelled.Cancel(cancellationDate, TestPolicies.References("REF"));

        // Assert
        quote.IsSuccess.ShouldBeTrue();
        quote.Value.ShouldBe(cancelled.Refunds.Select(r => new RefundQuote(r.TermNumber, r.Type, r.Amount)));
    }

    [Fact]
    public void GIVEN_a_renewed_policy_WHEN_quoting_a_cancellation_THEN_each_paid_term_is_listed()
    {
        // Arrange
        var policy = TestPolicies.Sold(autoRenew: true);
        policy.Renew(new DateOnly(2027, 6, 15), 400.00m, PaymentType.Card, TestPolicies.References("PAY-R"));

        // Act
        var quote = policy.QuoteCancellation(new DateOnly(2027, 6, 21));

        // Assert
        quote.Value.ShouldBe(
        [
            new RefundQuote(1, PaymentType.DirectDebit, 10.00m),
            new RefundQuote(2, PaymentType.Card, 400.00m),
        ]);
    }

    [Fact]
    public void GIVEN_a_sold_policy_WHEN_quoting_a_cancellation_THEN_the_policy_is_unchanged()
    {
        // Arrange
        var policy = TestPolicies.Sold();

        // Act
        policy.QuoteCancellation(new DateOnly(2026, 10, 9));

        // Assert
        policy.Status.ShouldBe(PolicyStatus.Active);
        policy.CancellationDate.ShouldBeNull();
        policy.Refunds.ShouldBeEmpty();
        policy.Version.ShouldBe(0);
    }

    [Fact]
    public void GIVEN_a_sold_policy_WHEN_quoting_for_a_date_after_the_end_date_THEN_it_is_rejected_as_invalid()
    {
        // Act
        var quote = TestPolicies.Sold().QuoteCancellation(TestPolicies.EndDate.AddDays(1));

        // Assert
        quote.IsSuccess.ShouldBeFalse();
        quote.Field.ShouldBe("CancellationDate");
        quote.Value.ShouldBeNull();
    }

    [Fact]
    public void GIVEN_a_cancelled_policy_WHEN_quoting_a_cancellation_THEN_it_is_rejected()
    {
        // Arrange
        var policy = TestPolicies.Sold();
        policy.Cancel(new DateOnly(2026, 6, 20), TestPolicies.References("REF"));

        // Act
        var quote = policy.QuoteCancellation(new DateOnly(2026, 6, 25));

        // Assert
        quote.IsSuccess.ShouldBeFalse();
        quote.Field.ShouldBeNull();
        quote.Error.ShouldBe("The policy has already been cancelled from 2026-06-20.");
    }
}
