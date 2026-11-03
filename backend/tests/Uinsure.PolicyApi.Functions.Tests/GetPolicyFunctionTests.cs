using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Functions.Tests;

public sealed class GetPolicyFunctionTests : IAsyncDisposable
{
    private readonly PolicyApiHarness _api = new();

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    [Fact]
    public async Task A_sold_policy_can_be_retrieved_with_all_its_details()
    {
        var sold = (await _api.SellAsync(PolicyApiHarness.ValidBody("POL-0001")))
            .ShouldBeOfType<CreatedResult>().Value.ShouldBeOfType<PolicyResponse>();

        var result = await _api.GetAsync("POL-0001");

        var policy = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>();

        // Read back from SQLite in a fresh scope, so this proves everything was persisted.
        policy.Reference.ShouldBe(sold.Reference);
        policy.InsuranceType.ShouldBe(sold.InsuranceType);
        policy.StartDate.ShouldBe(sold.StartDate);
        policy.EndDate.ShouldBe(sold.EndDate);
        policy.Amount.ShouldBe(sold.Amount);
        policy.HasClaims.ShouldBe(sold.HasClaims);
        policy.AutoRenew.ShouldBe(sold.AutoRenew);
        policy.Property.ShouldBe(sold.Property);
        policy.Policyholders.ShouldBe(sold.Policyholders, ignoreOrder: true);
        policy.Payments.ShouldBe(sold.Payments);
        policy.Refunds.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("pol-0001")]
    [InlineData("Pol-0001")]
    public async Task Policy_references_are_case_insensitive(string reference)
    {
        await _api.SellAsync(PolicyApiHarness.ValidBody("POL-0001"));

        var result = await _api.GetAsync(reference);

        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>().Reference.ShouldBe("POL-0001");
    }

    [Fact]
    public async Task Retrieving_an_unknown_policy_returns_404_problem_details()
    {
        var result = await _api.GetAsync("does-not-exist");

        var notFound = result.ShouldBeOfType<ObjectResult>();
        notFound.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        var problem = notFound.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Policy not found");
        problem.Detail.ShouldBe("No policy found with reference 'DOES-NOT-EXIST'.");
        problem.Instance.ShouldBe("/api/policies/does-not-exist");
    }

    [Fact]
    public async Task Policies_are_serialised_with_readable_enums_and_iso_dates()
    {
        var policy = (await _api.SellAsync(PolicyApiHarness.ValidBody()))
            .ShouldBeOfType<CreatedResult>().Value.ShouldBeOfType<PolicyResponse>();

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(policy, PolicyApiHarness.Json));
        var root = json.RootElement;

        root.GetProperty("insuranceType").GetString().ShouldBe(nameof(InsuranceType.BuyToLet));
        root.GetProperty("startDate").GetString().ShouldBe("2026-06-29");
        root.GetProperty("endDate").GetString().ShouldBe("2027-06-29");
        root.GetProperty("payments")[0].GetProperty("type").GetString().ShouldBe(nameof(PaymentType.DirectDebit));
    }
}
