using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Functions.Tests;

public sealed class RecordClaimFunctionTests : IAsyncDisposable
{
    private readonly PolicyApiHarness _api = new();

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    [Fact]
    public async Task GIVEN_an_active_policy_WHEN_recording_a_claim_THEN_the_policy_has_claims_and_is_persisted()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await _api.RecordClaimAsync("pol-0001");

        // Assert
        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>().HasClaims.ShouldBeTrue();
        var stored = (await _api.GetAsync("POL-0001")).ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();
        stored.HasClaims.ShouldBeTrue();
    }

    [Fact]
    public async Task GIVEN_a_policy_with_claims_WHEN_cancelling_THEN_no_refund_is_given()
    {
        // Arrange
        await SellAsync();
        await _api.RecordClaimAsync("POL-0001");

        // Act
        // Before the start date, which would normally be a full refund.
        var result = await _api.CancelAsync("POL-0001", new { cancellationDate = "2026-06-20" });

        // Assert
        var policy = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();
        policy.Status.ShouldBe(PolicyStatus.Cancelled);
        policy.Refunds.ShouldBeEmpty();
    }

    [Fact]
    public async Task GIVEN_an_unknown_reference_WHEN_recording_a_claim_THEN_404_is_returned()
    {
        // Act
        var result = await _api.RecordClaimAsync("NOPE");

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GIVEN_a_cancelled_policy_WHEN_recording_a_claim_THEN_409_is_returned()
    {
        // Arrange
        await SellAsync();
        await _api.CancelAsync("POL-0001", new { cancellationDate = "2026-06-20" });

        // Act
        var result = await _api.RecordClaimAsync("POL-0001");

        // Assert
        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        var problem = conflict.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Claim cannot be recorded");
        problem.Detail.ShouldBe("A claim cannot be recorded on a cancelled policy.");
    }

    private async Task SellAsync() =>
        (await _api.SellAsync(PolicyApiHarness.ValidBody("POL-0001"))).ShouldBeOfType<CreatedResult>();
}
