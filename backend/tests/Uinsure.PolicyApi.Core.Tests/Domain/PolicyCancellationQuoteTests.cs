using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests.Domain;

public sealed class PolicyCancellationQuoteTests
{
    [Theory]
    [InlineData("2026-06-20")] // before the start date
    [InlineData("2026-07-10")] // cooling off
    [InlineData("2026-10-09")] // pro rata
    [InlineData("2027-07-01")] // the end date: nothing to refund
    public void A_quote_matches_the_refunds_cancelling_on_that_date_gives(string date)
    {
        var cancellationDate = DateOnly.Parse(date);
        var quoted = TestPolicies.Sold();
        var cancelled = TestPolicies.Sold();

        var quote = quoted.QuoteCancellation(cancellationDate);
        cancelled.Cancel(cancellationDate, TestPolicies.References("REF"));

        quote.IsSuccess.ShouldBeTrue();
        quote.Value.ShouldBe(cancelled.Refunds.Select(r => new RefundQuote(r.TermNumber, r.Type, r.Amount)));
    }

    [Fact]
    public void A_quote_for_a_renewed_policy_lists_each_paid_term()
    {
        var policy = TestPolicies.Sold(autoRenew: true);
        policy.Renew(new DateOnly(2027, 6, 15), 400.00m, PaymentType.Card, TestPolicies.References("PAY-R"));

        var quote = policy.QuoteCancellation(new DateOnly(2027, 6, 21));

        quote.Value.ShouldBe(
        [
            new RefundQuote(1, PaymentType.DirectDebit, 10.00m),
            new RefundQuote(2, PaymentType.Card, 400.00m),
        ]);
    }

    [Fact]
    public void Quoting_does_not_change_the_policy()
    {
        var policy = TestPolicies.Sold();

        policy.QuoteCancellation(new DateOnly(2026, 10, 9));

        policy.Status.ShouldBe(PolicyStatus.Active);
        policy.CancellationDate.ShouldBeNull();
        policy.Refunds.ShouldBeEmpty();
        policy.Version.ShouldBe(0);
    }

    [Fact]
    public void Quoting_for_a_date_after_the_end_date_is_rejected_as_invalid()
    {
        var quote = TestPolicies.Sold().QuoteCancellation(TestPolicies.EndDate.AddDays(1));

        quote.IsSuccess.ShouldBeFalse();
        quote.Field.ShouldBe("CancellationDate");
        quote.Value.ShouldBeNull();
    }

    [Fact]
    public void Quoting_for_a_cancelled_policy_is_rejected()
    {
        var policy = TestPolicies.Sold();
        policy.Cancel(new DateOnly(2026, 6, 20), TestPolicies.References("REF"));

        var quote = policy.QuoteCancellation(new DateOnly(2026, 6, 25));

        quote.IsSuccess.ShouldBeFalse();
        quote.Field.ShouldBeNull();
        quote.Error.ShouldBe("The policy has already been cancelled from 2026-06-20.");
    }
}
