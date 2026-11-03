using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Functions.Tests;

public sealed class SellPolicyFunctionTests : IAsyncDisposable
{
    private readonly PolicyApiHarness _api = new();

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    [Fact]
    public async Task Selling_a_valid_policy_returns_201_with_location_and_policy()
    {
        var result = await _api.SellAsync(PolicyApiHarness.ValidBody("pol-0001"));

        var created = result.ShouldBeOfType<CreatedResult>();
        created.Location.ShouldBe("/api/policies/POL-0001");

        var policy = created.Value.ShouldBeOfType<PolicyResponse>();
        policy.Reference.ShouldBe("POL-0001");
        policy.InsuranceType.ShouldBe(InsuranceType.BuyToLet);
        policy.StartDate.ShouldBe(new DateOnly(2026, 6, 29));
        policy.EndDate.ShouldBe(new DateOnly(2027, 6, 29));
        policy.Amount.ShouldBe(425.50m);
        policy.HasClaims.ShouldBeFalse();
        policy.AutoRenew.ShouldBeTrue();
        policy.Property.Postcode.ShouldBe("EX1 2AM");
        policy.Policyholders.Count.ShouldBe(2);
        policy.Refunds.ShouldBeEmpty();

        var payment = policy.Payments.ShouldHaveSingleItem();
        payment.PaymentReference.ShouldStartWith("PAY-");
        payment.Type.ShouldBe(PaymentType.DirectDebit);
        payment.Amount.ShouldBe(425.50m);
    }

    [Fact]
    public async Task Selling_an_invalid_policy_returns_400_with_errors_for_each_invalid_field()
    {
        var body = PolicyApiHarness.ValidBody();
        body["startDate"] = PolicyApiHarness.Today.AddDays(61).ToString("yyyy-MM-dd");
        body["amount"] = 0;
        body["property"] = new Dictionary<string, object?> { ["addressLine1"] = "", ["postcode"] = "SW1A 1AAX" };
        body["policyholders"] = new[]
        {
            new Dictionary<string, object?> { ["firstName"] = "Teen", ["lastName"] = "Ager", ["dateOfBirth"] = "2015-01-01" },
        };

        var result = await _api.SellAsync(body);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        var problem = badRequest.Value.ShouldBeOfType<ValidationProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Instance.ShouldBe("/api/policies");
        problem.Errors.Keys.ShouldBe(
            ["StartDate", "Amount", "Property.AddressLine1", "Property.Postcode", "Policyholders[0]"],
            ignoreOrder: true);
        problem.Errors["StartDate"].ShouldBe(["A policy can only be sold up to 60 days in advance."]);
    }

    [Fact]
    public async Task Selling_a_policy_with_missing_fields_reports_them_as_required()
    {
        var result = await _api.SellRawAsync("{}");

        var problem = result.ShouldBeOfType<BadRequestObjectResult>().Value.ShouldBeOfType<ValidationProblemDetails>();
        problem.Errors.Keys.ShouldBe(
            ["Reference", "InsuranceType", "PaymentType", "Amount", "StartDate", "Property", "Policyholders"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Selling_a_duplicate_reference_returns_409()
    {
        await _api.SellAsync(PolicyApiHarness.ValidBody("POL-0001"));

        var result = await _api.SellAsync(PolicyApiHarness.ValidBody("pol-0001"));

        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        var problem = conflict.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Policy already exists");
        problem.Detail.ShouldBe("A policy with reference 'POL-0001' already exists.");
    }

    [Theory]
    [InlineData("{\"reference\": \"POL-1\", \"amount\": \"lots\"}", "$.amount")]
    [InlineData("{\"startDate\": \"15/06/2026\"}", "$.startDate")]
    [InlineData("{\"insuranceType\": \"Car\"}", "$.insuranceType")]
    [InlineData("{\"paymentType\": \"Bitcoin\"}", "$.paymentType")]
    [InlineData("{\"policyholders\": [{\"dateOfBirth\": \"yesterday\"}]}", "$.policyholders[0].dateOfBirth")]
    public async Task Invalid_field_value_returns_400_naming_the_field(string body, string expectedPath)
    {
        var result = await _api.SellRawAsync(body);

        var badRequest = result.ShouldBeOfType<ObjectResult>();
        badRequest.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        var problem = badRequest.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Malformed request body");
        problem.Detail.ShouldNotBeNull().ShouldContain($"'{expectedPath}'");
        problem.Detail.ShouldNotContain("System.");
    }

    [Fact]
    public async Task Invalid_json_returns_400()
    {
        var result = await _api.SellRawAsync("{ not json");

        var badRequest = result.ShouldBeOfType<ObjectResult>();
        badRequest.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        badRequest.Value.ShouldBeOfType<ProblemDetails>().Title.ShouldBe("Malformed request body");
    }

    [Fact]
    public async Task Null_body_returns_400()
    {
        var result = await _api.SellRawAsync("null");

        var badRequest = result.ShouldBeOfType<ObjectResult>();
        badRequest.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        badRequest.Value.ShouldBeOfType<ProblemDetails>().Title.ShouldBe("Missing request body");
    }

    [Fact]
    public async Task Non_json_content_type_returns_415()
    {
        var result = await _api.SellRawAsync("reference=POL-1", "application/x-www-form-urlencoded");

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status415UnsupportedMediaType);
    }
}
