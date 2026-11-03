using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;

namespace Uinsure.PolicyApi.Functions.OpenApi;

/// <summary>
/// OpenAPI document settings. The OpenAPI extension discovers this class automatically
/// because it derives from <see cref="DefaultOpenApiConfigurationOptions"/>.
/// </summary>
public sealed class OpenApiConfiguration : DefaultOpenApiConfigurationOptions
{
    /// <summary>Swagger tag grouping the policy operations.</summary>
    public const string PoliciesTag = "Policies";

    public override OpenApiInfo Info { get; set; } = new()
    {
        Title = "Uinsure Policy API",
        Version = "1.0.0",
        Description = "Sell, retrieve, cancel and renew home insurance policies (Household and Buy to Let). " +
            "Errors are returned as RFC 9457 Problem Details.",
    };

    public override OpenApiVersionType OpenApiVersion { get; set; } = OpenApiVersionType.V3;

    public override List<IDocumentFilter> DocumentFilters { get; set; } = [new DateOnlySchemaFilter()];
}
