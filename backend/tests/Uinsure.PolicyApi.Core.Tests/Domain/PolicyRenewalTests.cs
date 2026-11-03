using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests.Domain;

public sealed class PolicyRenewalTests
{
    [Theory]
    [InlineData("2027-06-01")] // 30 days before the end date: the window opens
    [InlineData("2027-06-15")]
    [InlineData("2027-07-01")] // the end date itself
    public void A_policy_can_be_renewed_from_30_days_before_its_end_date_up_to_the_end_date(string today)
    {
        var policy = TestPolicies.Sold();

        Renew(policy, today).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Renewing_more_than_30_days_before_the_end_date_is_rejected()
    {
        var policy = TestPolicies.Sold();

        var result = Renew(policy, "2027-05-31");

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(
            "A policy can only be renewed in the 30 days before its end date. This policy can be renewed from 2027-06-01.");
        policy.Terms.Count.ShouldBe(1);
    }

    [Fact]
    public void Renewing_after_the_end_date_is_rejected()
    {
        var policy = TestPolicies.Sold();

        var result = Renew(policy, "2027-07-02");

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("The policy ended on 2027-07-01 and can no longer be renewed.");
    }

    [Fact]
    public void Renewing_adds_a_one_year_term_starting_on_the_current_end_date()
    {
        var policy = TestPolicies.Sold();

        Renew(policy, "2027-06-15", amount: 400.00m);

        var term = policy.Terms.Single(t => t.TermNumber == 2);
        term.StartDate.ShouldBe(TestPolicies.EndDate);
        term.EndDate.ShouldBe(new DateOnly(2028, 7, 1));
        term.Amount.ShouldBe(400.00m);
        policy.LatestTerm.ShouldBe(term);
        policy.StartDate.ShouldBe(TestPolicies.EndDate);
        policy.EndDate.ShouldBe(new DateOnly(2028, 7, 1));
        policy.Amount.ShouldBe(400.00m);
        policy.Version.ShouldBe(1);
    }

    [Fact]
    public void Renewing_an_auto_renewing_policy_creates_a_payment()
    {
        var policy = TestPolicies.Sold(autoRenew: true);

        Renew(policy, "2027-06-15", amount: 400.00m, paymentType: PaymentType.Cheque);

        var payment = policy.Payments.Single(p => p.TermNumber == 2);
        payment.PaymentReference.ShouldBe("PAY-R-1");
        payment.Type.ShouldBe(PaymentType.Cheque);
        payment.Amount.ShouldBe(400.00m);
    }

    [Fact]
    public void Renewing_a_policy_that_does_not_auto_renew_creates_no_payment()
    {
        var policy = TestPolicies.Sold(autoRenew: false);

        Renew(policy, "2027-06-15", paymentType: null).IsSuccess.ShouldBeTrue();

        policy.Terms.Count.ShouldBe(2);
        policy.Payments.ShouldHaveSingleItem().TermNumber.ShouldBe(1);
    }

    [Fact]
    public void Renewing_an_auto_renewing_policy_without_a_payment_type_is_rejected_as_invalid()
    {
        var policy = TestPolicies.Sold(autoRenew: true);

        var result = Renew(policy, "2027-06-15", paymentType: null);

        result.IsSuccess.ShouldBeFalse();
        result.Field.ShouldBe("PaymentType");
        policy.Terms.Count.ShouldBe(1);
        policy.Payments.Count.ShouldBe(1);
    }

    [Fact]
    public void Renewing_an_already_renewed_policy_is_rejected()
    {
        var policy = TestPolicies.Sold();
        Renew(policy, "2027-06-15");

        var result = Renew(policy, "2027-06-16");

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("The policy has already been renewed; the next term starts on 2027-07-01.");
        policy.Terms.Count.ShouldBe(2);
    }

    [Fact]
    public void A_renewed_policy_can_be_renewed_again_in_the_next_window()
    {
        var policy = TestPolicies.Sold();
        Renew(policy, "2027-06-15");

        Renew(policy, "2028-06-15").IsSuccess.ShouldBeTrue();

        policy.Terms.Select(t => t.TermNumber).ShouldBe([1, 2, 3], ignoreOrder: true);
        policy.EndDate.ShouldBe(new DateOnly(2029, 7, 1));
    }

    [Fact]
    public void A_cancelled_policy_cannot_be_renewed()
    {
        var policy = TestPolicies.Sold();
        policy.Cancel(new DateOnly(2027, 6, 10), TestPolicies.References("REF"));

        var result = Renew(policy, "2027-06-15");

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("A cancelled policy cannot be renewed.");
    }

    private static DomainResult Renew(
        Policy policy,
        string today,
        decimal amount = 400.00m,
        PaymentType? paymentType = PaymentType.Card) =>
        policy.Renew(DateOnly.Parse(today), amount, paymentType, TestPolicies.References("PAY-R"));
}
