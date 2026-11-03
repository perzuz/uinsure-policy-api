using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Serialization;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Domain;
using Uinsure.PolicyApi.Functions.OpenApi;
using Uinsure.PolicyApi.Functions.Policies;

namespace Uinsure.PolicyApi.Functions.Tests;

public sealed class OpenApiTests
{
    // Every function in the app, found by reflection, so a new function is covered automatically.
    private static readonly MethodInfo[] FunctionMethods =
    [
        .. typeof(SellPolicyFunction).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods())
            .Where(m => m.GetCustomAttribute<FunctionAttribute>() is not null),
    ];

    private static MethodInfo Function(string name) =>
        FunctionMethods.Single(m => m.GetCustomAttribute<FunctionAttribute>()!.Name == name);

    [Fact]
    public void All_four_policy_functions_exist()
    {
        FunctionMethods.Select(m => m.GetCustomAttribute<FunctionAttribute>()!.Name)
            .ShouldBe(["SellPolicy", "GetPolicy", "CancelPolicy", "RenewPolicy"], ignoreOrder: true);
    }

    [Fact]
    public void Each_function_is_in_its_own_class_named_after_it()
    {
        FunctionMethods.ShouldAllBe(m => m.DeclaringType!.Name == m.GetCustomAttribute<FunctionAttribute>()!.Name + "Function");
        FunctionMethods.GroupBy(m => m.DeclaringType).ShouldAllBe(g => g.Count() == 1);
    }

    [Fact]
    public void Every_function_is_documented_with_a_unique_operation()
    {
        var operations = FunctionMethods
            .Select(m => m.GetCustomAttribute<OpenApiOperationAttribute>()
                ?? throw new ShouldAssertException($"{m.DeclaringType!.Name} has no [OpenApiOperation] attribute."))
            .ToList();

        operations.Select(o => o.OperationId).ShouldBeUnique();
        operations.ShouldAllBe(o => !string.IsNullOrWhiteSpace(o.Summary));
    }

    [Theory]
    [InlineData("SellPolicy", new[] { HttpStatusCode.Created, HttpStatusCode.BadRequest, HttpStatusCode.Conflict, HttpStatusCode.UnsupportedMediaType })]
    [InlineData("GetPolicy", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound })]
    [InlineData("CancelPolicy", new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.Conflict, HttpStatusCode.UnsupportedMediaType })]
    [InlineData("RenewPolicy", new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.Conflict, HttpStatusCode.UnsupportedMediaType })]
    public void Every_status_code_a_function_returns_is_documented(string functionName, HttpStatusCode[] expected)
    {
        var documented = Function(functionName)
            .GetCustomAttributes<OpenApiResponseWithBodyAttribute>()
            .Select(a => a.StatusCode);

        documented.ShouldBe(expected, ignoreOrder: true);
    }

    [Fact]
    public void Cancel_and_renew_examples_are_in_the_format_the_api_accepts()
    {
        var today = new DateOnly(2026, 6, 15);
        var cancelJson = Newtonsoft.Json.JsonConvert.SerializeObject(CancelPolicyRequestExample.Create(today), CamelCase);
        var renewJson = Newtonsoft.Json.JsonConvert.SerializeObject(RenewPolicyRequestExample.Create(), CamelCase);

        JsonSerializer.Deserialize<CancelPolicyRequest>(cancelJson, PolicyApiHarness.Json)!.CancellationDate.ShouldBe(today);
        var renew = JsonSerializer.Deserialize<RenewPolicyRequest>(renewJson, PolicyApiHarness.Json)!;
        renew.Amount.ShouldBe(340.00m);
        renew.PaymentType.ShouldBe(PaymentType.Card);
    }

    private static readonly Newtonsoft.Json.JsonSerializerSettings CamelCase = new()
    {
        ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
    };

    [Fact]
    public void Enums_are_documented_and_serialised_by_name_for_the_openapi_extension()
    {
        // The extension uses Newtonsoft; the API uses System.Text.Json. Both must agree on names.
        Newtonsoft.Json.JsonConvert.SerializeObject(InsuranceType.BuyToLet).ShouldBe("\"BuyToLet\"");
        Newtonsoft.Json.JsonConvert.SerializeObject(PaymentType.DirectDebit).ShouldBe("\"DirectDebit\"");
        Newtonsoft.Json.JsonConvert.SerializeObject(PolicyStatus.Cancelled).ShouldBe("\"Cancelled\"");
    }

    [Fact]
    public void DateOnly_schema_is_rewritten_as_an_iso_date_string()
    {
        var document = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Schemas =
                {
                    [DateOnlySchemaFilter.SchemaName] = new OpenApiSchema
                    {
                        Type = "object",
                        Nullable = true,
                        Properties = { ["year"] = new OpenApiSchema { Type = "integer" } },
                    },
                },
            },
        };

        new DateOnlySchemaFilter().Apply(null!, document);

        var schema = document.Components.Schemas[DateOnlySchemaFilter.SchemaName];
        schema.Type.ShouldBe("string");
        schema.Format.ShouldBe("date");
        schema.Nullable.ShouldBeFalse();
        schema.Properties.ShouldBeEmpty();
    }

    [Fact]
    public void DateOnly_filter_ignores_documents_without_a_dateonly_schema()
    {
        var document = new OpenApiDocument { Components = new OpenApiComponents() };

        Should.NotThrow(() => new DateOnlySchemaFilter().Apply(null!, document));
    }

    [Fact]
    public void The_documented_example_is_in_the_format_the_api_accepts()
    {
        // The example is serialised by the extension (Newtonsoft) but read by the API (System.Text.Json).
        var example = new SellPolicyRequestExample().Build(new CamelCaseNamingStrategy()).Examples["household"];
        var json = example.Value.ShouldBeOfType<OpenApiString>().Value;

        var parsed = JsonSerializer.Deserialize<SellPolicyRequest>(json, PolicyApiHarness.Json).ShouldNotBeNull();

        parsed.InsuranceType.ShouldBe(InsuranceType.Household);
        parsed.PaymentType.ShouldBe(PaymentType.Card);
        parsed.Amount.ShouldBe(325.50m);
        parsed.StartDate.ShouldNotBeNull();
        parsed.Property.ShouldNotBeNull().Postcode.ShouldBe("AB1 2CD");
        parsed.Policyholders.ShouldNotBeNull().ShouldHaveSingleItem().DateOfBirth.ShouldBe(new DateOnly(1985, 4, 12));
    }

    [Fact]
    public async Task The_example_request_sells_a_policy()
    {
        // "Try it out" in Swagger UI should succeed without editing the example.
        await using var api = new PolicyApiHarness();

        var result = await api.SellAsync(SellPolicyRequestExample.Create(PolicyApiHarness.Today));

        result.ShouldBeOfType<Microsoft.AspNetCore.Mvc.CreatedResult>();
    }

    [Fact]
    public void Document_settings_describe_the_api()
    {
        var configuration = new OpenApiConfiguration();

        configuration.Info.Title.ShouldBe("Uinsure Policy API");
        configuration.OpenApiVersion.ShouldBe(OpenApiVersionType.V3);
        configuration.DocumentFilters.ShouldContain(f => f is DateOnlySchemaFilter);
    }
}
