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
    public async Task GIVEN_no_date_WHEN_quoting_a_cancellation_THEN_it_is_quoted_for_today_and_the_policy_stays_active()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await _api.QuoteAsync("pol-0001");

        // Assert
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
    public async Task GIVEN_a_mid_term_date_WHEN_quoting_then_cancelling_THEN_the_refund_matches_the_quote()
    {
        // Arrange
        await SellAsync();
        var cancellationDate = PolicyStart.AddDays(100).ToString("yyyy-MM-dd");

        // Act
        var quote = (await _api.QuoteAsync("POL-0001", cancellationDate))
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<CancellationQuoteResponse>();
        var cancelled = (await _api.CancelAsync("POL-0001", new { cancellationDate }))
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();

        // Assert
        // 425.50 x 265 unused days / 365 = 308.9246...
        quote.TotalRefund.ShouldBe(308.92m);
        cancelled.Refunds.ShouldHaveSingleItem().Amount.ShouldBe(quote.TotalRefund);
    }

    [Fact]
    public async Task GIVEN_a_policy_with_claims_WHEN_quoting_a_cancellation_THEN_no_refund_is_quoted()
    {
        // Arrange
        await SellAsync();
        await _api.RecordClaimAsync("POL-0001");

        // Act
        var result = await _api.QuoteAsync("POL-0001");

        // Assert
        var quote = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<CancellationQuoteResponse>();
        quote.HasClaims.ShouldBeTrue();
        quote.TotalRefund.ShouldBe(0m);
        quote.Refunds.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("2026-06-14", "'Cancellation Date' cannot be in the past.")]
    [InlineData("2027-06-30", "'Cancellation Date' cannot be after the policy end date (2027-06-29).")]
    [InlineData("20/06/2026", "'Cancellation Date' must be a date in the format yyyy-MM-dd.")]
    public async Task GIVEN_an_invalid_date_WHEN_quoting_a_cancellation_THEN_400_is_returned(string cancellationDate, string expectedError)
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await _api.QuoteAsync("POL-0001", cancellationDate);

        // Assert
        var problem = result.ShouldBeOfType<BadRequestObjectResult>().Value.ShouldBeOfType<ValidationProblemDetails>();
        problem.Errors["CancellationDate"].ShouldBe([expectedError]);
    }

    [Fact]
    public async Task GIVEN_an_unknown_reference_WHEN_quoting_a_cancellation_THEN_404_is_returned()
    {
        // Act
        var result = await _api.QuoteAsync("NOPE");

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GIVEN_a_cancelled_policy_WHEN_quoting_a_cancellation_THEN_409_is_returned()
    {
        // Arrange
        await SellAsync();
        await _api.CancelAsync("POL-0001", new { cancellationDate = "2026-06-20" });

        // Act
        var result = await _api.QuoteAsync("POL-0001");

        // Assert
        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        conflict.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldBe("The policy has already been cancelled from 2026-06-20.");
    }

    private async Task SellAsync() =>
        (await _api.SellAsync(PolicyApiHarness.ValidBody("POL-0001"))).ShouldBeOfType<CreatedResult>();
}
