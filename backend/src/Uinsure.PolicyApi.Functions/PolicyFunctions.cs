using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Services;
using Uinsure.PolicyApi.Functions.OpenApi;

namespace Uinsure.PolicyApi.Functions;

public sealed class PolicyFunctions(IPolicyService policyService, ILogger<PolicyFunctions> logger)
{
    private const string PoliciesTag = "Policies";
    private const string JsonContentType = "application/json";
    private const string ProblemJsonContentType = "application/problem+json";

    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web);

    [Function("SellPolicy")]
    [OpenApiOperation(
        operationId: "SellPolicy",
        tags: [PoliciesTag],
        Summary = "Sell a policy",
        Description = "Creates a policy and records a payment for the full amount. Rules: the reference must be unique " +
            "(case-insensitive, max 20 characters); the start date must be between today and 60 days ahead; the policy " +
            "lasts 1 year (the end date is calculated); 1 to 3 policyholders, each at least 16 on the start date; " +
            "address line 1 and postcode (max 8 characters) are required; the amount must be greater than 0 with at most " +
            "2 decimal places.")]
    [OpenApiRequestBody(JsonContentType, typeof(SellPolicyRequest), Required = true, Example = typeof(SellPolicyRequestExample))]
    [OpenApiResponseWithBody(HttpStatusCode.Created, JsonContentType, typeof(PolicyResponse),
        CustomHeaderType = typeof(LocationHeader), Description = "Policy sold.")]
    [OpenApiResponseWithBody(HttpStatusCode.BadRequest, ProblemJsonContentType, typeof(ValidationProblemDetails),
        Description = "A business rule failed (errors keyed by field, e.g. 'StartDate', 'Property.Postcode', " +
            "'Policyholders[0]'), or the body is not valid JSON.")]
    [OpenApiResponseWithBody(HttpStatusCode.Conflict, ProblemJsonContentType, typeof(ProblemDetails),
        Description = "A policy with this reference already exists.")]
    [OpenApiResponseWithBody(HttpStatusCode.UnsupportedMediaType, ProblemJsonContentType, typeof(ProblemDetails),
        Description = "The request body was not sent as application/json.")]
    public async Task<IActionResult> SellPolicy(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "policies")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return Problem(
                request,
                StatusCodes.Status415UnsupportedMediaType,
                "Unsupported media type",
                "The request body must be JSON with a 'Content-Type: application/json' header.");
        }

        SellPolicyRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<SellPolicyRequest>(RequestJsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            // Report the JSON path rather than ex.Message, which exposes internal .NET type names.
            logger.LogInformation(ex, "Rejected malformed sell policy request");
            return Problem(
                request,
                StatusCodes.Status400BadRequest,
                "Malformed request body",
                ex.Path is null or "$"
                    ? "The request body is not valid JSON."
                    : $"The value at '{ex.Path}' is not valid. Check the field's type, format (dates are yyyy-MM-dd) and allowed values.");
        }

        if (body is null)
        {
            return Problem(request, StatusCodes.Status400BadRequest, "Missing request body", "A policy must be supplied in the request body.");
        }

        var result = await policyService.SellAsync(body, cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success => new CreatedResult($"/api/policies/{Uri.EscapeDataString(result.Value!.Reference)}", result.Value),
            ServiceResultStatus.ValidationFailed => ValidationProblem(request, result.ValidationErrors),
            ServiceResultStatus.Conflict => Problem(request, StatusCodes.Status409Conflict, "Policy already exists", result.Error),
            _ => throw new InvalidOperationException($"Unexpected result status '{result.Status}' when selling a policy."),
        };
    }

    [Function("GetPolicy")]
    [OpenApiOperation(operationId: "GetPolicy", tags: [PoliciesTag], Summary = "Retrieve a policy")]
    [OpenApiParameter("reference", In = ParameterLocation.Path, Required = true, Type = typeof(string),
        Description = "The policy reference (case-insensitive).")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, JsonContentType, typeof(PolicyResponse),
        Description = "The policy, with its property, policyholders, payments and refunds.")]
    [OpenApiResponseWithBody(HttpStatusCode.NotFound, ProblemJsonContentType, typeof(ProblemDetails),
        Description = "No policy exists with this reference.")]
    public async Task<IActionResult> GetPolicy(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "policies/{reference}")] HttpRequest request,
        string reference,
        CancellationToken cancellationToken)
    {
        var result = await policyService.GetAsync(reference, cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success => new OkObjectResult(result.Value),
            ServiceResultStatus.NotFound => Problem(request, StatusCodes.Status404NotFound, "Policy not found", result.Error),
            _ => throw new InvalidOperationException($"Unexpected result status '{result.Status}' when retrieving a policy."),
        };
    }

    private static ObjectResult Problem(HttpRequest request, int status, string title, string? detail) =>
        new(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = request.Path,
        })
        {
            StatusCode = status,
        };

    private static BadRequestObjectResult ValidationProblem(HttpRequest request, IReadOnlyDictionary<string, string[]> errors) =>
        new(new ValidationProblemDetails(errors.ToDictionary())
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Detail = "See the errors property for details of each invalid field.",
            Instance = request.Path,
        });
}
