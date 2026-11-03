using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Services;

namespace Uinsure.PolicyApi.Functions;

public sealed class PolicyFunctions(IPolicyService policyService, ILogger<PolicyFunctions> logger)
{
    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web);

    [Function("SellPolicy")]
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
