using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Resolvers;
using Newtonsoft.Json.Serialization;
using Uinsure.PolicyApi.Core;
using Uinsure.PolicyApi.Core.Contracts;

namespace Uinsure.PolicyApi.Functions.OpenApi;

/// <summary>Cancel request example shown in Swagger UI: cancel with effect from today.</summary>
public sealed class CancelPolicyRequestExample : OpenApiExample<CancelPolicyRequest>
{
    public override IOpenApiExample<CancelPolicyRequest> Build(NamingStrategy? namingStrategy = null)
    {
        Examples.Add(OpenApiExampleResolver.Resolve(
            "today",
            "Cancel with effect from today",
            Create(TimeProvider.System.GetUtcToday()),
            namingStrategy));

        return this;
    }

    public static CancelPolicyRequest Create(DateOnly today) => new() { CancellationDate = today };
}
