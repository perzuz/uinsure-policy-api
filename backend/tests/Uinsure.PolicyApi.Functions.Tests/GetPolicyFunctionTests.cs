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
    public async Task GIVEN_a_sold_policy_WHEN_retrieving_it_THEN_all_its_details_are_returned()
    {
        // Arrange
        var sold = (await _api.SellAsync(PolicyApiHarness.ValidBody("POL-0001")))
            .ShouldBeOfType<CreatedResult>().Value.ShouldBeOfType<PolicyResponse>();

        // Act
        var result = await _api.GetAsync("POL-0001");

        // Assert
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
    public async Task GIVEN_a_reference_in_a_different_case_WHEN_retrieving_THEN_the_policy_is_found(string reference)
    {
        // Arrange
        await _api.SellAsync(PolicyApiHarness.ValidBody("POL-0001"));

        // Act
        var result = await _api.GetAsync(reference);

        // Assert
        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<PolicyResponse>().Reference.ShouldBe("POL-0001");
    }

    [Fact]
    public async Task GIVEN_an_unknown_reference_WHEN_retrieving_THEN_404_problem_details_are_returned()
    {
        // Act
        var result = await _api.GetAsync("does-not-exist");

        // Assert
        var notFound = result.ShouldBeOfType<ObjectResult>();
        notFound.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        var problem = notFound.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Policy not found");
        problem.Detail.ShouldBe("No policy found with reference 'DOES-NOT-EXIST'.");
        problem.Instance.ShouldBe("/api/policies/does-not-exist");
    }

    [Fact]
    public async Task GIVEN_a_sold_policy_WHEN_serialising_it_THEN_enums_are_names_and_dates_are_iso()
    {
        // Arrange
        var policy = (await _api.SellAsync(PolicyApiHarness.ValidBody()))
            .ShouldBeOfType<CreatedResult>().Value.ShouldBeOfType<PolicyResponse>();

        // Act
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(policy, PolicyApiHarness.Json));
        var root = json.RootElement;

        // Assert
        root.GetProperty("insuranceType").GetString().ShouldBe(nameof(InsuranceType.BuyToLet));
        root.GetProperty("startDate").GetString().ShouldBe("2026-06-29");
        root.GetProperty("endDate").GetString().ShouldBe("2027-06-29");
        root.GetProperty("payments")[0].GetProperty("type").GetString().ShouldBe(nameof(PaymentType.DirectDebit));
    }
}
