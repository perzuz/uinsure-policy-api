using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests.Domain;

public sealed class PolicyCancellationTests
{
    [Fact]
    public void GIVEN_an_active_policy_WHEN_cancelling_THEN_it_is_cancelled_from_the_given_date()
    {
        // Arrange
        var policy = TestPolicies.Sold();

        // Act
        var result = policy.Cancel(new DateOnly(2026, 6, 20), TestPolicies.References("REF"));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        policy.Status.ShouldBe(PolicyStatus.Cancelled);
        policy.CancellationDate.ShouldBe(new DateOnly(2026, 6, 20));
        policy.Version.ShouldBe(1);
    }

    [Theory]
    [InlineData(PaymentType.Card)]
    [InlineData(PaymentType.DirectDebit)]
    [InlineData(PaymentType.Cheque)]
    public void GIVEN_a_policy_paid_by_any_method_WHEN_cancelling_THEN_the_refund_is_made_to_the_same_method(PaymentType paymentType)
    {
        // Arrange
        // Cheque can only pay for a policy that doesn't auto renew.
        var policy = TestPolicies.Sold(autoRenew: paymentType != PaymentType.Cheque, paymentType: paymentType);

        // Act
        policy.Cancel(new DateOnly(2026, 6, 20), TestPolicies.References("REF"));

        // Assert
        var refund = policy.Refunds.ShouldHaveSingleItem();
        refund.Type.ShouldBe(paymentType);
        refund.Amount.ShouldBe(365.00m);
        refund.TermNumber.ShouldBe(1);
        refund.RefundReference.ShouldBe("REF-1");
    }

    [Fact]
    public void GIVEN_a_policy_past_cooling_off_WHEN_cancelling_mid_term_THEN_the_refund_is_pro_rata()
    {
        // Arrange
        var policy = TestPolicies.Sold();

        // Act
        policy.Cancel(new DateOnly(2026, 10, 9), TestPolicies.References("REF")); // 100 days used

        // Assert
        policy.Refunds.ShouldHaveSingleItem().Amount.ShouldBe(265.00m);
    }

    [Fact]
    public void GIVEN_an_active_policy_WHEN_cancelling_on_the_end_date_THEN_it_is_cancelled_with_no_refund()
    {
        // Arrange
        var policy = TestPolicies.Sold();

        // Act
        policy.Cancel(TestPolicies.EndDate, TestPolicies.References("REF")).IsSuccess.ShouldBeTrue();

        // Assert
        policy.Refunds.ShouldBeEmpty();
        policy.Status.ShouldBe(PolicyStatus.Cancelled);
    }

    [Fact]
    public void GIVEN_an_active_policy_WHEN_cancelling_after_the_end_date_THEN_it_is_rejected_as_invalid()
    {
        // Arrange
        var policy = TestPolicies.Sold();

        // Act
        var result = policy.Cancel(TestPolicies.EndDate.AddDays(1), TestPolicies.References("REF"));

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Field.ShouldBe("CancellationDate");
        result.Error.ShouldBe("'Cancellation Date' cannot be after the policy end date (2027-07-01).");
        policy.Status.ShouldBe(PolicyStatus.Active);
    }

    [Fact]
    public void GIVEN_a_cancelled_policy_WHEN_cancelling_again_THEN_it_is_rejected()
    {
        // Arrange
        var policy = TestPolicies.Sold();
        policy.Cancel(new DateOnly(2026, 6, 20), TestPolicies.References("REF"));

        // Act
        var result = policy.Cancel(new DateOnly(2026, 6, 25), TestPolicies.References("REF"));

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Field.ShouldBeNull();
        result.Error.ShouldBe("The policy has already been cancelled from 2026-06-20.");
        policy.Refunds.Count.ShouldBe(1);
        policy.CancellationDate.ShouldBe(new DateOnly(2026, 6, 20));
    }

    [Fact]
    public void GIVEN_a_renewed_policy_WHEN_cancelling_before_the_new_term_starts_THEN_both_terms_are_refunded()
    {
        // Arrange
        var policy = RenewedPolicy(autoRenew: true);

        // Act
        // 10 days before term 1 ends: term 1 is refunded pro rata, term 2 has not started.
        policy.Cancel(new DateOnly(2027, 6, 21), TestPolicies.References("REF"));

        // Assert
        policy.Refunds.Select(r => (r.TermNumber, r.Amount, r.Type)).ShouldBe(
        [
            (1, 10.00m, PaymentType.DirectDebit),
            (2, 400.00m, PaymentType.Card),
        ]);
    }

    [Fact]
    public void GIVEN_a_renewed_policy_WHEN_cancelling_during_the_new_term_THEN_only_that_term_is_refunded()
    {
        // Arrange
        var policy = RenewedPolicy(autoRenew: true);

        // Act
        // Term 1 has ended; term 2 (366 days, includes 29 Feb 2028) is past cooling off.
        policy.Cancel(new DateOnly(2027, 8, 1), TestPolicies.References("REF"));

        // Assert
        var refund = policy.Refunds.ShouldHaveSingleItem();
        refund.TermNumber.ShouldBe(2);
        refund.Amount.ShouldBe(366.12m); // 400 x 335 / 366 = 366.1202...
        refund.Type.ShouldBe(PaymentType.Card);
    }

    [Fact]
    public void GIVEN_a_renewed_term_without_a_payment_WHEN_cancelling_THEN_that_term_is_not_refunded()
    {
        // Arrange
        var policy = RenewedPolicy(autoRenew: false);

        // Act
        policy.Cancel(new DateOnly(2027, 6, 21), TestPolicies.References("REF"));

        // Assert
        policy.Refunds.ShouldHaveSingleItem().TermNumber.ShouldBe(1);
    }

    private static Policy RenewedPolicy(bool autoRenew)
    {
        var policy = TestPolicies.Sold(autoRenew: autoRenew);
        policy.Renew(new DateOnly(2027, 6, 15), 400.00m, PaymentType.Card, TestPolicies.References("PAY-R"))
            .IsSuccess.ShouldBeTrue();
        return policy;
    }
}
