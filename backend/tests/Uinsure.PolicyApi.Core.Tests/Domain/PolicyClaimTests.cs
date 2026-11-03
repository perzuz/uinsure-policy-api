using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests.Domain;

public sealed class PolicyClaimTests
{
    [Fact]
    public void Recording_a_claim_marks_the_policy_as_having_claims()
    {
        var policy = TestPolicies.Sold();

        policy.RecordClaim().IsSuccess.ShouldBeTrue();

        policy.HasClaims.ShouldBeTrue();
        policy.Version.ShouldBe(1);
    }

    [Fact]
    public void Recording_a_further_claim_changes_nothing()
    {
        var policy = TestPolicies.Sold();
        policy.RecordClaim();

        policy.RecordClaim().IsSuccess.ShouldBeTrue();

        policy.HasClaims.ShouldBeTrue();
        policy.Version.ShouldBe(1);
    }

    [Fact]
    public void A_claim_cannot_be_recorded_on_a_cancelled_policy()
    {
        var policy = TestPolicies.Sold();
        policy.Cancel(new DateOnly(2026, 6, 20), TestPolicies.References("REF"));

        var result = policy.RecordClaim();

        result.IsSuccess.ShouldBeFalse();
        result.Field.ShouldBeNull();
        result.Error.ShouldBe("A claim cannot be recorded on a cancelled policy.");
        policy.HasClaims.ShouldBeFalse();
    }

    [Theory]
    [InlineData("2026-06-20")] // before the start date: normally a full refund
    [InlineData("2026-10-09")] // mid-term: normally pro rata
    public void Cancelling_a_policy_with_claims_gives_no_refund(string date)
    {
        var policy = TestPolicies.Sold();
        policy.RecordClaim();

        policy.QuoteCancellation(DateOnly.Parse(date)).Value.ShouldBeEmpty();
        policy.Cancel(DateOnly.Parse(date), TestPolicies.References("REF")).IsSuccess.ShouldBeTrue();

        policy.Refunds.ShouldBeEmpty();
        policy.Status.ShouldBe(PolicyStatus.Cancelled);
    }

    [Fact]
    public void Claims_block_refunds_on_every_term_of_a_renewed_policy()
    {
        var policy = TestPolicies.Sold(autoRenew: true);
        policy.Renew(new DateOnly(2027, 6, 15), 400.00m, PaymentType.Card, TestPolicies.References("PAY-R"));
        policy.RecordClaim();

        policy.Cancel(new DateOnly(2027, 6, 21), TestPolicies.References("REF"));

        policy.Refunds.ShouldBeEmpty();
    }
}
