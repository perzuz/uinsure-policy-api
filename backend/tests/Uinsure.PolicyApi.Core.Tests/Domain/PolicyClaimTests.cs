using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests.Domain;

public sealed class PolicyClaimTests
{
    [Fact]
    public void GIVEN_an_active_policy_WHEN_recording_a_claim_THEN_the_policy_has_claims()
    {
        // Arrange
        var policy = TestPolicies.Sold();

        // Act
        policy.RecordClaim().IsSuccess.ShouldBeTrue();

        // Assert
        policy.HasClaims.ShouldBeTrue();
        policy.Version.ShouldBe(1);
    }

    [Fact]
    public void GIVEN_a_policy_with_claims_WHEN_recording_another_claim_THEN_nothing_changes()
    {
        // Arrange
        var policy = TestPolicies.Sold();
        policy.RecordClaim();

        // Act
        policy.RecordClaim().IsSuccess.ShouldBeTrue();

        // Assert
        policy.HasClaims.ShouldBeTrue();
        policy.Version.ShouldBe(1);
    }

    [Fact]
    public void GIVEN_a_cancelled_policy_WHEN_recording_a_claim_THEN_it_is_rejected()
    {
        // Arrange
        var policy = TestPolicies.Sold();
        policy.Cancel(new DateOnly(2026, 6, 20), TestPolicies.References("REF"));

        // Act
        var result = policy.RecordClaim();

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Field.ShouldBeNull();
        result.Error.ShouldBe("A claim cannot be recorded on a cancelled policy.");
        policy.HasClaims.ShouldBeFalse();
    }

    [Theory]
    [InlineData("2026-06-20")] // before the start date: normally a full refund
    [InlineData("2026-10-09")] // mid-term: normally pro rata
    public void GIVEN_a_policy_with_claims_WHEN_cancelling_THEN_no_refund_is_given(string date)
    {
        // Arrange
        var policy = TestPolicies.Sold();
        policy.RecordClaim();

        // Act
        policy.QuoteCancellation(DateOnly.Parse(date)).Value.ShouldBeEmpty();
        policy.Cancel(DateOnly.Parse(date), TestPolicies.References("REF")).IsSuccess.ShouldBeTrue();

        // Assert
        policy.Refunds.ShouldBeEmpty();
        policy.Status.ShouldBe(PolicyStatus.Cancelled);
    }

    [Fact]
    public void GIVEN_a_renewed_policy_with_claims_WHEN_cancelling_THEN_no_term_is_refunded()
    {
        // Arrange
        var policy = TestPolicies.Sold(autoRenew: true);
        policy.Renew(new DateOnly(2027, 6, 15), 400.00m, PaymentType.Card, TestPolicies.References("PAY-R"));
        policy.RecordClaim();

        // Act
        policy.Cancel(new DateOnly(2027, 6, 21), TestPolicies.References("REF"));

        // Assert
        policy.Refunds.ShouldBeEmpty();
    }
}
