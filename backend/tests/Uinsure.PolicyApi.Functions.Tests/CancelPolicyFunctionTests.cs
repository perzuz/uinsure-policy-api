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
    public async Task GIVEN_a_sold_policy_WHEN_cancelling_before_the_start_date_THEN_it_is_refunded_in_full_and_persisted()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await _api.CancelAsync("pol-0001", new { cancellationDate = "2026-06-20" });

        // Assert
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
    public async Task GIVEN_a_policy_past_cooling_off_WHEN_cancelling_mid_term_THEN_the_unused_days_are_refunded()
    {
        // Arrange
        await SellAsync();
        var cancellationDate = PolicyStart.AddDays(100);
        _api.SetToday(cancellationDate);

        // Act
        var result = await _api.CancelAsync("POL-0001", new { cancellationDate = cancellationDate.ToString("yyyy-MM-dd") });

        // Assert
        // 425.50 x 265 unused days / 365 = 308.9246...
        var policy = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();
        policy.Refunds.ShouldHaveSingleItem().Amount.ShouldBe(308.92m);
    }

    [Fact]
    public async Task GIVEN_an_unknown_reference_WHEN_cancelling_THEN_404_is_returned()
    {
        // Act
        var result = await _api.CancelAsync("NOPE", new { cancellationDate = "2026-06-20" });

        // Assert
        var notFound = result.ShouldBeOfType<ObjectResult>();
        notFound.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        notFound.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldBe("No policy found with reference 'NOPE'.");
    }

    [Fact]
    public async Task GIVEN_a_cancelled_policy_WHEN_cancelling_again_THEN_409_is_returned()
    {
        // Arrange
        await SellAsync();
        await _api.CancelAsync("POL-0001", new { cancellationDate = "2026-06-20" });

        // Act
        var result = await _api.CancelAsync("POL-0001", new { cancellationDate = "2026-06-21" });

        // Assert
        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        var problem = conflict.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Policy cannot be cancelled");
        problem.Detail.ShouldBe("The policy has already been cancelled from 2026-06-20.");
    }

    [Theory]
    [InlineData("2026-06-14", "'Cancellation Date' cannot be in the past.")]
    [InlineData("2027-06-30", "'Cancellation Date' cannot be after the policy end date (2027-06-29).")]
    public async Task GIVEN_an_invalid_date_WHEN_cancelling_THEN_400_is_returned(string cancellationDate, string expectedError)
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await _api.CancelAsync("POL-0001", new { cancellationDate });

        // Assert
        var problem = result.ShouldBeOfType<BadRequestObjectResult>().Value.ShouldBeOfType<ValidationProblemDetails>();
        problem.Errors["CancellationDate"].ShouldBe([expectedError]);
    }

    [Fact]
    public async Task GIVEN_no_date_WHEN_cancelling_THEN_400_is_returned()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await _api.CancelRawAsync("POL-0001", "{}");

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>().Value.ShouldBeOfType<ValidationProblemDetails>()
            .Errors.Keys.ShouldBe(["CancellationDate"]);
    }

    [Fact]
    public async Task GIVEN_malformed_json_WHEN_cancelling_THEN_400_names_the_field()
    {
        // Act
        var result = await _api.CancelRawAsync("POL-0001", "{\"cancellationDate\": \"20/06/2026\"}");

        // Assert
        var badRequest = result.ShouldBeOfType<ObjectResult>();
        badRequest.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        badRequest.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldNotBeNull().ShouldContain("'$.cancellationDate'");
    }

    [Fact]
    public async Task GIVEN_a_non_json_content_type_WHEN_cancelling_THEN_415_is_returned()
    {
        // Act
        var result = await _api.CancelRawAsync("POL-0001", "cancellationDate=2026-06-20", "text/plain");

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status415UnsupportedMediaType);
    }

    private async Task SellAsync() =>
        (await _api.SellAsync(PolicyApiHarness.ValidBody("POL-0001"))).ShouldBeOfType<CreatedResult>();
}
