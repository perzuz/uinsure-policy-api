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
    public async Task Recording_a_claim_returns_the_policy_with_claims_and_is_persisted()
    {
        await SellAsync();

        var result = await _api.RecordClaimAsync("pol-0001");

        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>().HasClaims.ShouldBeTrue();
        var stored = (await _api.GetAsync("POL-0001")).ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();
        stored.HasClaims.ShouldBeTrue();
    }

    [Fact]
    public async Task Cancelling_a_policy_with_claims_gives_no_refund()
    {
        await SellAsync();
        await _api.RecordClaimAsync("POL-0001");

        // Before the start date, which would normally be a full refund.
        var result = await _api.CancelAsync("POL-0001", new { cancellationDate = "2026-06-20" });

        var policy = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();
        policy.Status.ShouldBe(PolicyStatus.Cancelled);
        policy.Refunds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Recording_a_claim_on_an_unknown_policy_returns_404()
    {
        var result = await _api.RecordClaimAsync("NOPE");

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Recording_a_claim_on_a_cancelled_policy_returns_409()
    {
        await SellAsync();
        await _api.CancelAsync("POL-0001", new { cancellationDate = "2026-06-20" });

        var result = await _api.RecordClaimAsync("POL-0001");

        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        var problem = conflict.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Claim cannot be recorded");
        problem.Detail.ShouldBe("A claim cannot be recorded on a cancelled policy.");
    }

    private async Task SellAsync() =>
        (await _api.SellAsync(PolicyApiHarness.ValidBody("POL-0001"))).ShouldBeOfType<CreatedResult>();
}
