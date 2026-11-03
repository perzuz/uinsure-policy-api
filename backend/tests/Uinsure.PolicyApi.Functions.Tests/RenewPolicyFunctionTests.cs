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
    public async Task Renewing_within_the_window_adds_a_term_and_payment_and_is_persisted()
    {
        await SellAsync();
        _api.SetToday(PolicyEnd.AddDays(-10));

        var result = await _api.RenewAsync("pol-0001", new { amount = 450.00m, paymentType = "Card" });

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
    public async Task Renewing_a_policy_that_does_not_auto_renew_takes_no_payment()
    {
        var body = PolicyApiHarness.ValidBody("POL-0001");
        body["autoRenew"] = false;
        (await _api.SellAsync(body)).ShouldBeOfType<CreatedResult>();
        _api.SetToday(PolicyEnd);

        var result = await _api.RenewAsync("POL-0001", new { amount = 450.00m });

        var policy = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();
        policy.Terms.Count.ShouldBe(2);
        policy.Payments.ShouldHaveSingleItem().TermNumber.ShouldBe(1);
    }

    [Fact]
    public async Task Renewing_before_the_window_returns_409_with_the_date_it_opens()
    {
        await SellAsync();

        var result = await _api.RenewAsync("POL-0001", new { amount = 450.00m, paymentType = "Card" });

        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        var problem = conflict.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Policy cannot be renewed");
        problem.Detail.ShouldBe(
            "A policy can only be renewed in the 30 days before its end date. This policy can be renewed from 2027-05-30.");
    }

    [Fact]
    public async Task Renewing_after_the_end_date_returns_409()
    {
        await SellAsync();
        _api.SetToday(PolicyEnd.AddDays(1));

        var result = await _api.RenewAsync("POL-0001", new { amount = 450.00m, paymentType = "Card" });

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Renewing_twice_returns_409()
    {
        await SellAsync();
        _api.SetToday(PolicyEnd.AddDays(-10));
        await _api.RenewAsync("POL-0001", new { amount = 450.00m, paymentType = "Card" });

        var result = await _api.RenewAsync("POL-0001", new { amount = 450.00m, paymentType = "Card" });

        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        conflict.Value.ShouldBeOfType<ProblemDetails>().Detail
            .ShouldBe("The policy has already been renewed; the next term starts on 2027-06-29.");
    }

    [Fact]
    public async Task Renewing_a_cancelled_policy_returns_409()
    {
        await SellAsync();
        await _api.CancelAsync("POL-0001", new { cancellationDate = "2026-06-20" });
        _api.SetToday(PolicyEnd.AddDays(-10));

        var result = await _api.RenewAsync("POL-0001", new { amount = 450.00m, paymentType = "Card" });

        result.ShouldBeOfType<ObjectResult>().Value.ShouldBeOfType<ProblemDetails>().Detail
            .ShouldBe("A cancelled policy cannot be renewed.");
    }

    [Fact]
    public async Task Renewing_an_auto_renewing_policy_without_a_payment_type_returns_400()
    {
        await SellAsync();
        _api.SetToday(PolicyEnd.AddDays(-10));

        var result = await _api.RenewAsync("POL-0001", new { amount = 450.00m });

        result.ShouldBeOfType<BadRequestObjectResult>().Value.ShouldBeOfType<ValidationProblemDetails>()
            .Errors.Keys.ShouldBe(["PaymentType"]);
    }

    [Fact]
    public async Task Renewing_with_an_invalid_amount_returns_400()
    {
        await SellAsync();

        var result = await _api.RenewAsync("POL-0001", new { amount = 0, paymentType = "Card" });

        result.ShouldBeOfType<BadRequestObjectResult>().Value.ShouldBeOfType<ValidationProblemDetails>()
            .Errors.Keys.ShouldBe(["Amount"]);
    }

    [Fact]
    public async Task Renewing_an_unknown_policy_returns_404()
    {
        var result = await _api.RenewAsync("NOPE", new { amount = 450.00m, paymentType = "Card" });

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Invalid_payment_type_returns_400_naming_the_field()
    {
        var result = await _api.RenewRawAsync("POL-0001", "{\"amount\": 450, \"paymentType\": \"Bitcoin\"}");

        var badRequest = result.ShouldBeOfType<ObjectResult>();
        badRequest.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        badRequest.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldNotBeNull().ShouldContain("'$.paymentType'");
    }

    private async Task SellAsync() =>
        (await _api.SellAsync(PolicyApiHarness.ValidBody("POL-0001"))).ShouldBeOfType<CreatedResult>();
}
