using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Functions.Tests;

public sealed class GetCancellationQuoteFunctionTests : IAsyncDisposable
{
    // PolicyApiHarness.ValidBody(): £425.50 by direct debit, 2026-06-29 to 2027-06-29 (365 days).
    private static readonly DateOnly PolicyStart = new(2026, 6, 29);

    private readonly PolicyApiHarness _api = new();

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    [Fact]
    public async Task Quoting_without_a_date_quotes_for_today_and_leaves_the_policy_active()
    {
        await SellAsync();

        var result = await _api.QuoteAsync("pol-0001");

        var quote = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<CancellationQuoteResponse>();
        quote.Reference.ShouldBe("POL-0001");
        quote.CancellationDate.ShouldBe(PolicyApiHarness.Today);
        quote.HasClaims.ShouldBeFalse();
        quote.TotalRefund.ShouldBe(425.50m);
        quote.Refunds.ShouldBe([new RefundQuoteResponse(1, PaymentType.DirectDebit, 425.50m)]);

        var stored = (await _api.GetAsync("POL-0001")).ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();
        stored.Status.ShouldBe(PolicyStatus.Active);
        stored.Refunds.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_mid_term_quote_matches_the_refund_cancelling_on_that_date_gives()
    {
        await SellAsync();
        var cancellationDate = PolicyStart.AddDays(100).ToString("yyyy-MM-dd");

        var quote = (await _api.QuoteAsync("POL-0001", cancellationDate))
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<CancellationQuoteResponse>();
        var cancelled = (await _api.CancelAsync("POL-0001", new { cancellationDate }))
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();

        // 425.50 x 265 unused days / 365 = 308.9246...
        quote.TotalRefund.ShouldBe(308.92m);
        cancelled.Refunds.ShouldHaveSingleItem().Amount.ShouldBe(quote.TotalRefund);
    }

    [Fact]
    public async Task A_policy_with_claims_is_quoted_no_refund()
    {
        await SellAsync();
        await _api.RecordClaimAsync("POL-0001");

        var result = await _api.QuoteAsync("POL-0001");

        var quote = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<CancellationQuoteResponse>();
        quote.HasClaims.ShouldBeTrue();
        quote.TotalRefund.ShouldBe(0m);
        quote.Refunds.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("2026-06-14", "'Cancellation Date' cannot be in the past.")]
    [InlineData("2027-06-30", "'Cancellation Date' cannot be after the policy end date (2027-06-29).")]
    [InlineData("20/06/2026", "'Cancellation Date' must be a date in the format yyyy-MM-dd.")]
    public async Task Quoting_for_an_invalid_date_returns_400(string cancellationDate, string expectedError)
    {
        await SellAsync();

        var result = await _api.QuoteAsync("POL-0001", cancellationDate);

        var problem = result.ShouldBeOfType<BadRequestObjectResult>().Value.ShouldBeOfType<ValidationProblemDetails>();
        problem.Errors["CancellationDate"].ShouldBe([expectedError]);
    }

    [Fact]
    public async Task Quoting_for_an_unknown_policy_returns_404()
    {
        var result = await _api.QuoteAsync("NOPE");

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Quoting_for_a_cancelled_policy_returns_409()
    {
        await SellAsync();
        await _api.CancelAsync("POL-0001", new { cancellationDate = "2026-06-20" });

        var result = await _api.QuoteAsync("POL-0001");

        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        conflict.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldBe("The policy has already been cancelled from 2026-06-20.");
    }

    private async Task SellAsync() =>
        (await _api.SellAsync(PolicyApiHarness.ValidBody("POL-0001"))).ShouldBeOfType<CreatedResult>();
}
