using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Resolvers;
using Newtonsoft.Json.Serialization;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Functions.OpenApi;

/// <summary>Renew request example shown in Swagger UI.</summary>
public sealed class RenewPolicyRequestExample : OpenApiExample<RenewPolicyRequest>
{
    public override IOpenApiExample<RenewPolicyRequest> Build(NamingStrategy? namingStrategy = null)
    {
        Examples.Add(OpenApiExampleResolver.Resolve(
            "card",
            "Renew for another year, paying by card",
            Create(),
            namingStrategy));

        return this;
    }

    public static RenewPolicyRequest Create() => new() { Amount = 340.00m, PaymentType = PaymentType.Card };
}
