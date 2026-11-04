using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Functions.Tests;

public sealed class RenewPolicyFunctionTests : IAsyncDisposable
{
    // PolicyApiHarness.ValidBody(): auto-renewing, 2026-06-29 to 2027-06-29; renewal window opens 2027-05-30.
    private static readonly DateOnly PolicyEnd = new(2027, 6, 29);

    private readonly PolicyApiHarness _api = new();

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    [Fact]
    public async Task GIVEN_a_policy_in_its_renewal_window_WHEN_renewing_THEN_a_term_and_payment_are_added_and_persisted()
    {
        // Arrange
        await SellAsync();
        _api.SetToday(PolicyEnd.AddDays(-10));

        // Act
        var result = await _api.RenewAsync("pol-0001", new { amount = 450.00m, paymentType = "Card" });

        // Assert
        var policy = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();
        policy.Terms.Select(t => (t.TermNumber, t.StartDate, t.EndDate, t.Amount)).ShouldBe(
        [
            (1, new DateOnly(2026, 6, 29), PolicyEnd, 425.50m),
            (2, PolicyEnd, new DateOnly(2028, 6, 29), 450.00m),
        ]);
        policy.StartDate.ShouldBe(PolicyEnd);
        policy.EndDate.ShouldBe(new DateOnly(2028, 6, 29));
        policy.Amount.ShouldBe(450.00m);
        var payment = policy.Payments.Single(p => p.TermNumber == 2);
        payment.Type.ShouldBe(PaymentType.Card);
        payment.Amount.ShouldBe(450.00m);

        var stored = (await _api.GetAsync("POL-0001")).ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();
        stored.Terms.ShouldBe(policy.Terms);
        stored.Payments.ShouldBe(policy.Payments);
    }

    [Fact]
    public async Task GIVEN_a_policy_that_does_not_auto_renew_WHEN_renewing_THEN_no_payment_is_taken()
    {
        // Arrange
        var body = PolicyApiHarness.ValidBody("POL-0001");
        body["autoRenew"] = false;
        (await _api.SellAsync(body)).ShouldBeOfType<CreatedResult>();
        _api.SetToday(PolicyEnd);

        // Act
        var result = await _api.RenewAsync("POL-0001", new { amount = 450.00m });

        // Assert
        var policy = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();
        policy.Terms.Count.ShouldBe(2);
        policy.Payments.ShouldHaveSingleItem().TermNumber.ShouldBe(1);
    }

    [Fact]
    public async Task GIVEN_the_renewal_window_has_not_opened_WHEN_renewing_THEN_409_gives_the_date_it_opens()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await _api.RenewAsync("POL-0001", new { amount = 450.00m, paymentType = "Card" });

        // Assert
        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        var problem = conflict.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Policy cannot be renewed");
        problem.Detail.ShouldBe(
            "A policy can only be renewed in the 30 days before its end date. This policy can be renewed from 2027-05-30.");
    }

    [Fact]
    public async Task GIVEN_the_end_date_has_passed_WHEN_renewing_THEN_409_is_returned()
    {
        // Arrange
        await SellAsync();
        _api.SetToday(PolicyEnd.AddDays(1));

        // Act
        var result = await _api.RenewAsync("POL-0001", new { amount = 450.00m, paymentType = "Card" });

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task GIVEN_an_already_renewed_policy_WHEN_renewing_again_THEN_409_is_returned()
    {
        // Arrange
        await SellAsync();
        _api.SetToday(PolicyEnd.AddDays(-10));
        await _api.RenewAsync("POL-0001", new { amount = 450.00m, paymentType = "Card" });

        // Act
        var result = await _api.RenewAsync("POL-0001", new { amount = 450.00m, paymentType = "Card" });

        // Assert
        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        conflict.Value.ShouldBeOfType<ProblemDetails>().Detail
            .ShouldBe("The policy has already been renewed; the next term starts on 2027-06-29.");
    }

    [Fact]
    public async Task GIVEN_a_cancelled_policy_WHEN_renewing_THEN_409_is_returned()
    {
        // Arrange
        await SellAsync();
        await _api.CancelAsync("POL-0001", new { cancellationDate = "2026-06-20" });
        _api.SetToday(PolicyEnd.AddDays(-10));

        // Act
        var result = await _api.RenewAsync("POL-0001", new { amount = 450.00m, paymentType = "Card" });

        // Assert
        result.ShouldBeOfType<ObjectResult>().Value.ShouldBeOfType<ProblemDetails>().Detail
            .ShouldBe("A cancelled policy cannot be renewed.");
    }

    [Fact]
    public async Task GIVEN_an_auto_renewing_policy_WHEN_renewing_without_a_payment_type_THEN_400_is_returned()
    {
        // Arrange
        await SellAsync();
        _api.SetToday(PolicyEnd.AddDays(-10));

        // Act
        var result = await _api.RenewAsync("POL-0001", new { amount = 450.00m });

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>().Value.ShouldBeOfType<ValidationProblemDetails>()
            .Errors.Keys.ShouldBe(["PaymentType"]);
    }

    [Fact]
    public async Task GIVEN_an_auto_renewing_policy_WHEN_renewing_by_cheque_THEN_400_is_returned()
    {
        // Arrange
        await SellAsync();
        _api.SetToday(PolicyEnd.AddDays(-10));

        // Act
        var result = await _api.RenewAsync("POL-0001", new { amount = 450.00m, paymentType = "Cheque" });

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>().Value.ShouldBeOfType<ValidationProblemDetails>()
            .Errors["PaymentType"].ShouldBe(["Auto-renewing policies cannot be paid by cheque."]);
    }

    [Fact]
    public async Task GIVEN_an_invalid_amount_WHEN_renewing_THEN_400_is_returned()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await _api.RenewAsync("POL-0001", new { amount = 0, paymentType = "Card" });

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>().Value.ShouldBeOfType<ValidationProblemDetails>()
            .Errors.Keys.ShouldBe(["Amount"]);
    }

    [Fact]
    public async Task GIVEN_an_unknown_reference_WHEN_renewing_THEN_404_is_returned()
    {
        // Act
        var result = await _api.RenewAsync("NOPE", new { amount = 450.00m, paymentType = "Card" });

        // Assert
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GIVEN_an_unknown_payment_type_WHEN_renewing_THEN_400_names_the_field()
    {
        // Act
        var result = await _api.RenewRawAsync("POL-0001", "{\"amount\": 450, \"paymentType\": \"Bitcoin\"}");

        // Assert
        var badRequest = result.ShouldBeOfType<ObjectResult>();
        badRequest.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        badRequest.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldNotBeNull().ShouldContain("'$.paymentType'");
    }

    private async Task SellAsync() =>
        (await _api.SellAsync(PolicyApiHarness.ValidBody("POL-0001"))).ShouldBeOfType<CreatedResult>();
}
