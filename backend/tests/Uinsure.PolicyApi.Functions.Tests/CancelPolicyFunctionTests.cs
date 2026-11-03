using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Functions.Tests;

public sealed class CancelPolicyFunctionTests : IAsyncDisposable
{
    // PolicyApiHarness.ValidBody(): £425.50 by direct debit, 2026-06-29 to 2027-06-29 (365 days).
    private static readonly DateOnly PolicyStart = new(2026, 6, 29);

    private readonly PolicyApiHarness _api = new();

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    [Fact]
    public async Task Cancelling_before_the_start_date_refunds_in_full_and_is_persisted()
    {
        await SellAsync();

        var result = await _api.CancelAsync("pol-0001", new { cancellationDate = "2026-06-20" });

        var policy = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();
        policy.Status.ShouldBe(PolicyStatus.Cancelled);
        policy.CancellationDate.ShouldBe(new DateOnly(2026, 6, 20));
        var refund = policy.Refunds.ShouldHaveSingleItem();
        refund.Amount.ShouldBe(425.50m);
        refund.Type.ShouldBe(PaymentType.DirectDebit);
        refund.RefundReference.ShouldStartWith("REF-");

        var stored = (await _api.GetAsync("POL-0001")).ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();
        stored.Status.ShouldBe(PolicyStatus.Cancelled);
        stored.CancellationDate.ShouldBe(new DateOnly(2026, 6, 20));
        stored.Refunds.ShouldBe(policy.Refunds);
    }

    [Fact]
    public async Task Cancelling_mid_term_refunds_the_unused_days()
    {
        await SellAsync();
        var cancellationDate = PolicyStart.AddDays(100);
        _api.SetToday(cancellationDate);

        var result = await _api.CancelAsync("POL-0001", new { cancellationDate = cancellationDate.ToString("yyyy-MM-dd") });

        // 425.50 x 265 unused days / 365 = 308.9246...
        var policy = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();
        policy.Refunds.ShouldHaveSingleItem().Amount.ShouldBe(308.92m);
    }

    [Fact]
    public async Task Cancelling_an_unknown_policy_returns_404()
    {
        var result = await _api.CancelAsync("NOPE", new { cancellationDate = "2026-06-20" });

        var notFound = result.ShouldBeOfType<ObjectResult>();
        notFound.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        notFound.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldBe("No policy found with reference 'NOPE'.");
    }

    [Fact]
    public async Task Cancelling_twice_returns_409()
    {
        await SellAsync();
        await _api.CancelAsync("POL-0001", new { cancellationDate = "2026-06-20" });

        var result = await _api.CancelAsync("POL-0001", new { cancellationDate = "2026-06-21" });

        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        var problem = conflict.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Policy cannot be cancelled");
        problem.Detail.ShouldBe("The policy has already been cancelled from 2026-06-20.");
    }

    [Theory]
    [InlineData("2026-06-14", "'Cancellation Date' cannot be in the past.")]
    [InlineData("2027-06-30", "'Cancellation Date' cannot be after the policy end date (2027-06-29).")]
    public async Task Cancelling_from_an_invalid_date_returns_400(string cancellationDate, string expectedError)
    {
        await SellAsync();

        var result = await _api.CancelAsync("POL-0001", new { cancellationDate });

        var problem = result.ShouldBeOfType<BadRequestObjectResult>().Value.ShouldBeOfType<ValidationProblemDetails>();
        problem.Errors["CancellationDate"].ShouldBe([expectedError]);
    }

    [Fact]
    public async Task Cancelling_without_a_date_returns_400()
    {
        await SellAsync();

        var result = await _api.CancelRawAsync("POL-0001", "{}");

        result.ShouldBeOfType<BadRequestObjectResult>().Value.ShouldBeOfType<ValidationProblemDetails>()
            .Errors.Keys.ShouldBe(["CancellationDate"]);
    }

    [Fact]
    public async Task Malformed_json_returns_400_naming_the_field()
    {
        var result = await _api.CancelRawAsync("POL-0001", "{\"cancellationDate\": \"20/06/2026\"}");

        var badRequest = result.ShouldBeOfType<ObjectResult>();
        badRequest.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        badRequest.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldNotBeNull().ShouldContain("'$.cancellationDate'");
    }

    [Fact]
    public async Task Non_json_content_type_returns_415()
    {
        var result = await _api.CancelRawAsync("POL-0001", "cancellationDate=2026-06-20", "text/plain");

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status415UnsupportedMediaType);
    }

    private async Task SellAsync() =>
        (await _api.SellAsync(PolicyApiHarness.ValidBody("POL-0001"))).ShouldBeOfType<CreatedResult>();
}
