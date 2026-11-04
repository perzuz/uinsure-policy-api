using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests.Domain;

public sealed class PolicyRenewalTests
{
    [Theory]
    [InlineData("2027-06-01")] // 30 days before the end date: the window opens
    [InlineData("2027-06-15")]
    [InlineData("2027-07-01")] // the end date itself
    public void GIVEN_a_date_from_30_days_before_up_to_the_end_date_WHEN_renewing_THEN_it_succeeds(string today)
    {
        // Arrange
        var policy = TestPolicies.Sold();

        // Act & Assert
        Renew(policy, today).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void GIVEN_a_date_more_than_30_days_before_the_end_date_WHEN_renewing_THEN_it_is_rejected()
    {
        // Arrange
        var policy = TestPolicies.Sold();

        // Act
        var result = Renew(policy, "2027-05-31");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(
            "A policy can only be renewed in the 30 days before its end date. This policy can be renewed from 2027-06-01.");
        policy.Terms.Count.ShouldBe(1);
    }

    [Fact]
    public void GIVEN_a_date_after_the_end_date_WHEN_renewing_THEN_it_is_rejected()
    {
        // Arrange
        var policy = TestPolicies.Sold();

        // Act
        var result = Renew(policy, "2027-07-02");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("The policy ended on 2027-07-01 and can no longer be renewed.");
    }

    [Fact]
    public void GIVEN_a_policy_in_its_renewal_window_WHEN_renewing_THEN_a_one_year_term_starting_on_the_current_end_date_is_added()
    {
        // Arrange
        var policy = TestPolicies.Sold();

        // Act
        Renew(policy, "2027-06-15", amount: 400.00m);

        // Assert
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
    public void GIVEN_an_auto_renewing_policy_WHEN_renewing_THEN_a_payment_is_created()
    {
        // Arrange
        var policy = TestPolicies.Sold(autoRenew: true);

        // Act
        Renew(policy, "2027-06-15", amount: 400.00m, paymentType: PaymentType.DirectDebit);

        // Assert
        var payment = policy.Payments.Single(p => p.TermNumber == 2);
        payment.PaymentReference.ShouldBe("PAY-R-1");
        payment.Type.ShouldBe(PaymentType.DirectDebit);
        payment.Amount.ShouldBe(400.00m);
    }

    [Fact]
    public void GIVEN_a_policy_that_does_not_auto_renew_WHEN_renewing_THEN_no_payment_is_created()
    {
        // Arrange
        var policy = TestPolicies.Sold(autoRenew: false);

        // Act
        Renew(policy, "2027-06-15", paymentType: null).IsSuccess.ShouldBeTrue();

        // Assert
        policy.Terms.Count.ShouldBe(2);
        policy.Payments.ShouldHaveSingleItem().TermNumber.ShouldBe(1);
    }

    [Fact]
    public void GIVEN_an_auto_renewing_policy_WHEN_renewing_without_a_payment_type_THEN_it_is_rejected_as_invalid()
    {
        // Arrange
        var policy = TestPolicies.Sold(autoRenew: true);

        // Act
        var result = Renew(policy, "2027-06-15", paymentType: null);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Field.ShouldBe("PaymentType");
        policy.Terms.Count.ShouldBe(1);
        policy.Payments.Count.ShouldBe(1);
    }

    [Fact]
    public void GIVEN_an_auto_renewing_policy_WHEN_renewing_by_cheque_THEN_it_is_rejected_as_invalid()
    {
        // Arrange
        var policy = TestPolicies.Sold(autoRenew: true);

        // Act
        var result = Renew(policy, "2027-06-15", paymentType: PaymentType.Cheque);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Field.ShouldBe("PaymentType");
        result.Error.ShouldBe("Auto-renewing policies cannot be paid by cheque.");
        policy.Terms.Count.ShouldBe(1);
        policy.Version.ShouldBe(0);
    }

    [Fact]
    public void GIVEN_a_policy_that_does_not_auto_renew_WHEN_renewing_by_cheque_THEN_the_payment_type_is_ignored()
    {
        // Arrange
        // No payment is taken, so the payment type doesn't matter.
        var policy = TestPolicies.Sold(autoRenew: false, paymentType: PaymentType.Cheque);

        // Act
        Renew(policy, "2027-06-15", paymentType: PaymentType.Cheque).IsSuccess.ShouldBeTrue();

        // Assert
        policy.Payments.ShouldHaveSingleItem().TermNumber.ShouldBe(1);
    }

    [Fact]
    public void GIVEN_an_already_renewed_policy_WHEN_renewing_again_THEN_it_is_rejected()
    {
        // Arrange
        var policy = TestPolicies.Sold();
        Renew(policy, "2027-06-15");

        // Act
        var result = Renew(policy, "2027-06-16");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("The policy has already been renewed; the next term starts on 2027-07-01.");
        policy.Terms.Count.ShouldBe(2);
    }

    [Fact]
    public void GIVEN_a_renewed_policy_WHEN_renewing_in_the_next_window_THEN_it_succeeds()
    {
        // Arrange
        var policy = TestPolicies.Sold();
        Renew(policy, "2027-06-15");

        // Act
        Renew(policy, "2028-06-15").IsSuccess.ShouldBeTrue();

        // Assert
        policy.Terms.Select(t => t.TermNumber).ShouldBe([1, 2, 3], ignoreOrder: true);
        policy.EndDate.ShouldBe(new DateOnly(2029, 7, 1));
    }

    [Fact]
    public void GIVEN_a_cancelled_policy_WHEN_renewing_THEN_it_is_rejected()
    {
        // Arrange
        var policy = TestPolicies.Sold();
        policy.Cancel(new DateOnly(2027, 6, 10), TestPolicies.References("REF"));

        // Act
        var result = Renew(policy, "2027-06-15");

        // Assert
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
