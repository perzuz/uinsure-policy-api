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
    public void GIVEN_the_function_app_WHEN_listing_its_functions_THEN_all_policy_functions_exist()
    {
        // Act & Assert
        FunctionMethods.Select(m => m.GetCustomAttribute<FunctionAttribute>()!.Name)
            .ShouldBe(
                ["SellPolicy", "GetPolicy", "CancelPolicy", "RenewPolicy", "GetCancellationQuote", "RecordClaim"],
                ignoreOrder: true);
    }

    [Fact]
    public void GIVEN_the_function_app_WHEN_inspecting_each_function_THEN_it_is_alone_in_a_class_named_after_it()
    {
        // Act & Assert
        FunctionMethods.ShouldAllBe(m => m.DeclaringType!.Name == m.GetCustomAttribute<FunctionAttribute>()!.Name + "Function");
        FunctionMethods.GroupBy(m => m.DeclaringType).ShouldAllBe(g => g.Count() == 1);
    }

    [Fact]
    public void GIVEN_the_function_app_WHEN_inspecting_each_function_THEN_it_has_a_unique_documented_operation()
    {
        // Act
        var operations = FunctionMethods
            .Select(m => m.GetCustomAttribute<OpenApiOperationAttribute>()
                ?? throw new ShouldAssertException($"{m.DeclaringType!.Name} has no [OpenApiOperation] attribute."))
            .ToList();

        // Assert
        operations.Select(o => o.OperationId).ShouldBeUnique();
        operations.ShouldAllBe(o => !string.IsNullOrWhiteSpace(o.Summary));
    }

    [Theory]
    [InlineData("SellPolicy", new[] { HttpStatusCode.Created, HttpStatusCode.BadRequest, HttpStatusCode.Conflict, HttpStatusCode.UnsupportedMediaType })]
    [InlineData("GetPolicy", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound })]
    [InlineData("CancelPolicy", new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.Conflict, HttpStatusCode.UnsupportedMediaType })]
    [InlineData("RenewPolicy", new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.Conflict, HttpStatusCode.UnsupportedMediaType })]
    [InlineData("GetCancellationQuote", new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.Conflict })]
    [InlineData("RecordClaim", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Conflict })]
    public void GIVEN_a_function_WHEN_reading_its_openapi_attributes_THEN_every_status_code_it_returns_is_documented(string functionName, HttpStatusCode[] expected)
    {
        // Act
        var documented = Function(functionName)
            .GetCustomAttributes<OpenApiResponseWithBodyAttribute>()
            .Select(a => a.StatusCode);

        // Assert
        documented.ShouldBe(expected, ignoreOrder: true);
    }

    [Fact]
    public void GIVEN_the_cancel_and_renew_examples_WHEN_the_api_reads_them_THEN_they_deserialise_correctly()
    {
        // Arrange
        var today = new DateOnly(2026, 6, 15);
        var cancelJson = Newtonsoft.Json.JsonConvert.SerializeObject(CancelPolicyRequestExample.Create(today), CamelCase);
        var renewJson = Newtonsoft.Json.JsonConvert.SerializeObject(RenewPolicyRequestExample.Create(), CamelCase);

        // Act & Assert
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
    public void GIVEN_the_enums_WHEN_serialised_by_the_openapi_extension_THEN_they_are_written_by_name()
    {
        // Act & Assert
        // The extension uses Newtonsoft; the API uses System.Text.Json. Both must agree on names.
        Newtonsoft.Json.JsonConvert.SerializeObject(InsuranceType.BuyToLet).ShouldBe("\"BuyToLet\"");
        Newtonsoft.Json.JsonConvert.SerializeObject(PaymentType.DirectDebit).ShouldBe("\"DirectDebit\"");
        Newtonsoft.Json.JsonConvert.SerializeObject(PolicyStatus.Cancelled).ShouldBe("\"Cancelled\"");
    }

    [Fact]
    public void GIVEN_a_dateonly_object_schema_WHEN_applying_the_filter_THEN_it_becomes_an_iso_date_string()
    {
        // Arrange
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

        // Act
        new DateOnlySchemaFilter().Apply(null!, document);

        // Assert
        var schema = document.Components.Schemas[DateOnlySchemaFilter.SchemaName];
        schema.Type.ShouldBe("string");
        schema.Format.ShouldBe("date");
        schema.Nullable.ShouldBeFalse();
        schema.Properties.ShouldBeEmpty();
    }

    [Fact]
    public void GIVEN_a_document_without_a_dateonly_schema_WHEN_applying_the_filter_THEN_nothing_fails()
    {
        // Arrange
        var document = new OpenApiDocument { Components = new OpenApiComponents() };

        // Act & Assert
        Should.NotThrow(() => new DateOnlySchemaFilter().Apply(null!, document));
    }

    [Fact]
    public void GIVEN_the_sell_example_WHEN_the_api_reads_it_THEN_it_deserialises_correctly()
    {
        // Arrange
        // The example is serialised by the extension (Newtonsoft) but read by the API (System.Text.Json).
        var example = new SellPolicyRequestExample().Build(new CamelCaseNamingStrategy()).Examples["household"];
        var json = example.Value.ShouldBeOfType<OpenApiString>().Value;

        // Act
        var parsed = JsonSerializer.Deserialize<SellPolicyRequest>(json, PolicyApiHarness.Json).ShouldNotBeNull();

        // Assert
        parsed.InsuranceType.ShouldBe(InsuranceType.Household);
        parsed.PaymentType.ShouldBe(PaymentType.Card);
        parsed.Amount.ShouldBe(325.50m);
        parsed.StartDate.ShouldNotBeNull();
        parsed.Property.ShouldNotBeNull().Postcode.ShouldBe("AB1 2CD");
        parsed.Policyholders.ShouldNotBeNull().ShouldHaveSingleItem().DateOfBirth.ShouldBe(new DateOnly(1985, 4, 12));
    }

    [Fact]
    public async Task GIVEN_the_sell_example_WHEN_selling_it_THEN_the_policy_is_created()
    {
        // Arrange
        // "Try it out" in Swagger UI should succeed without editing the example.
        await using var api = new PolicyApiHarness();

        // Act
        var result = await api.SellAsync(SellPolicyRequestExample.Create(PolicyApiHarness.Today));

        // Assert
        result.ShouldBeOfType<Microsoft.AspNetCore.Mvc.CreatedResult>();
    }

    [Fact]
    public void GIVEN_the_openapi_configuration_WHEN_reading_it_THEN_it_describes_the_api()
    {
        // Act
        var configuration = new OpenApiConfiguration();

        // Assert
        configuration.Info.Title.ShouldBe("Uinsure Policy API");
        configuration.OpenApiVersion.ShouldBe(OpenApiVersionType.V3);
        configuration.DocumentFilters.ShouldContain(f => f is DateOnlySchemaFilter);
    }
}
