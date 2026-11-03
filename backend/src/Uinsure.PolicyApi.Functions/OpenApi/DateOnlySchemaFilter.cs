using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.OpenApi.Models;

namespace Uinsure.PolicyApi.Functions.OpenApi;

/// <summary>
/// The OpenAPI extension predates <see cref="DateOnly"/> and documents it as an object with
/// year/month/day/dayOfWeek properties. The API sends and receives ISO dates ("2026-11-20"),
/// so this rewrites the shared schema that every DateOnly property references.
/// </summary>
public sealed class DateOnlySchemaFilter : IDocumentFilter
{
    public const string SchemaName = "dateOnly";

    public void Apply(IHttpRequestDataObject req, OpenApiDocument document)
    {
        if (document.Components?.Schemas is null || !document.Components.Schemas.TryGetValue(SchemaName, out var schema))
        {
            return;
        }

        schema.Type = "string";
        schema.Format = "date";
        schema.Description = "ISO 8601 date (yyyy-MM-dd).";
        schema.Nullable = false;
        schema.Properties.Clear();
    }
}
