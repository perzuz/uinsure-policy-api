using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests.Domain;

public sealed class PolicyCancellationTests
{
    [Fact]
    public void Cancelling_marks_the_policy_cancelled_from_the_given_date()
    {
        var policy = TestPolicies.Sold();

        var result = policy.Cancel(new DateOnly(2026, 6, 20), TestPolicies.References("REF"));

        result.IsSuccess.ShouldBeTrue();
        policy.Status.ShouldBe(PolicyStatus.Cancelled);
        policy.CancellationDate.ShouldBe(new DateOnly(2026, 6, 20));
        policy.Version.ShouldBe(1);
    }

    [Theory]
    [InlineData(PaymentType.Card)]
    [InlineData(PaymentType.DirectDebit)]
    [InlineData(PaymentType.Cheque)]
    public void The_refund_is_made_to_the_same_method_as_the_original_payment(PaymentType paymentType)
    {
        // Cheque can only pay for a policy that doesn't auto renew.
        var policy = TestPolicies.Sold(autoRenew: paymentType != PaymentType.Cheque, paymentType: paymentType);

        policy.Cancel(new DateOnly(2026, 6, 20), TestPolicies.References("REF"));

        var refund = policy.Refunds.ShouldHaveSingleItem();
        refund.Type.ShouldBe(paymentType);
        refund.Amount.ShouldBe(365.00m);
        refund.TermNumber.ShouldBe(1);
        refund.RefundReference.ShouldBe("REF-1");
    }

    [Fact]
    public void Cancelling_mid_term_refunds_pro_rata()
    {
        var policy = TestPolicies.Sold();

        policy.Cancel(new DateOnly(2026, 10, 9), TestPolicies.References("REF")); // 100 days used

        policy.Refunds.ShouldHaveSingleItem().Amount.ShouldBe(265.00m);
    }

    [Fact]
    public void Cancelling_on_the_end_date_is_allowed_but_refunds_nothing()
    {
        var policy = TestPolicies.Sold();

        policy.Cancel(TestPolicies.EndDate, TestPolicies.References("REF")).IsSuccess.ShouldBeTrue();

        policy.Refunds.ShouldBeEmpty();
        policy.Status.ShouldBe(PolicyStatus.Cancelled);
    }

    [Fact]
    public void Cancelling_after_the_end_date_is_rejected_as_invalid()
    {
        var policy = TestPolicies.Sold();

        var result = policy.Cancel(TestPolicies.EndDate.AddDays(1), TestPolicies.References("REF"));

        result.IsSuccess.ShouldBeFalse();
        result.Field.ShouldBe("CancellationDate");
        result.Error.ShouldBe("'Cancellation Date' cannot be after the policy end date (2027-07-01).");
        policy.Status.ShouldBe(PolicyStatus.Active);
    }

    [Fact]
    public void Cancelling_an_already_cancelled_policy_is_rejected()
    {
        var policy = TestPolicies.Sold();
        policy.Cancel(new DateOnly(2026, 6, 20), TestPolicies.References("REF"));

        var result = policy.Cancel(new DateOnly(2026, 6, 25), TestPolicies.References("REF"));

        result.IsSuccess.ShouldBeFalse();
        result.Field.ShouldBeNull();
        result.Error.ShouldBe("The policy has already been cancelled from 2026-06-20.");
        policy.Refunds.Count.ShouldBe(1);
        policy.CancellationDate.ShouldBe(new DateOnly(2026, 6, 20));
    }

    [Fact]
    public void Cancelling_a_renewed_policy_before_the_new_term_refunds_both_terms()
    {
        var policy = RenewedPolicy(autoRenew: true);

        // 10 days before term 1 ends: term 1 is refunded pro rata, term 2 has not started.
        policy.Cancel(new DateOnly(2027, 6, 21), TestPolicies.References("REF"));

        policy.Refunds.Select(r => (r.TermNumber, r.Amount, r.Type)).ShouldBe(
        [
            (1, 10.00m, PaymentType.DirectDebit),
            (2, 400.00m, PaymentType.Card),
        ]);
    }

    [Fact]
    public void Cancelling_during_the_renewed_term_refunds_only_that_term()
    {
        var policy = RenewedPolicy(autoRenew: true);

        // Term 1 has ended; term 2 (366 days, includes 29 Feb 2028) is past cooling off.
        policy.Cancel(new DateOnly(2027, 8, 1), TestPolicies.References("REF"));

        var refund = policy.Refunds.ShouldHaveSingleItem();
        refund.TermNumber.ShouldBe(2);
        refund.Amount.ShouldBe(366.12m); // 400 x 335 / 366 = 366.1202...
        refund.Type.ShouldBe(PaymentType.Card);
    }

    [Fact]
    public void A_renewed_term_without_a_payment_is_not_refunded()
    {
        var policy = RenewedPolicy(autoRenew: false);

        policy.Cancel(new DateOnly(2027, 6, 21), TestPolicies.References("REF"));

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
