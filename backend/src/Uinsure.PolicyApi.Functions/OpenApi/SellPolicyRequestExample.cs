using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Resolvers;
using Newtonsoft.Json.Serialization;
using Uinsure.PolicyApi.Core;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Functions.OpenApi;

/// <summary>
/// Request body example shown in Swagger UI. The start date is relative to today so
/// "Try it out" succeeds without editing.
/// </summary>
public sealed class SellPolicyRequestExample : OpenApiExample<SellPolicyRequest>
{
    public override IOpenApiExample<SellPolicyRequest> Build(NamingStrategy? namingStrategy = null)
    {
        Examples.Add(OpenApiExampleResolver.Resolve(
            "household",
            "A household policy with one policyholder",
            Create(TimeProvider.System.GetUtcToday()),
            namingStrategy));

        return this;
    }

    public static SellPolicyRequest Create(DateOnly today) => new()
    {
        Reference = "POL-0001",
        InsuranceType = InsuranceType.Household,
        StartDate = today.AddDays(14),
        Amount = 325.50m,
        AutoRenew = true,
        PaymentType = PaymentType.Card,
        Property = new PropertyRequest
        {
            AddressLine1 = "1 High Street",
            AddressLine2 = "Little Town",
            AddressLine3 = "Exampleshire",
            Postcode = "AB1 2CD",
        },
        Policyholders =
        [
            new PolicyholderRequest { FirstName = "Jane", LastName = "Smith", DateOfBirth = new DateOnly(1985, 4, 12) },
        ],
    };
}
