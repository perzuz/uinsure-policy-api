using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.OpenApi.Models;

namespace Uinsure.PolicyApi.Functions.OpenApi;

/// <summary>Documents the Location header returned with 201 Created.</summary>
public sealed class LocationHeader : IOpenApiCustomResponseHeader
{
    public Dictionary<string, OpenApiHeader> Headers { get; set; } = new()
    {
        ["Location"] = new OpenApiHeader
        {
            Description = "URL of the new policy.",
            Schema = new OpenApiSchema { Type = "string" },
        },
    };
}
