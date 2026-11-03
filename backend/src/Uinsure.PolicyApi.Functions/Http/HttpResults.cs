using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Uinsure.PolicyApi.Core.Services;

namespace Uinsure.PolicyApi.Functions.Http;

public static class ContentTypes
{
    public const string Json = "application/json";
    public const string ProblemJson = "application/problem+json";
}

/// <summary>Request parsing and response mapping shared by the policy functions.</summary>
public static class HttpResults
{
    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Reads a JSON request body. Returns either the body or the error response to send:
    /// 415 for a non-JSON content type, or 400 for malformed JSON or a missing body.
    /// </summary>
    public static async Task<(T? Body, IActionResult? Error)> ReadJsonBodyAsync<T>(
        HttpRequest request,
        ILogger logger,
        CancellationToken cancellationToken)
        where T : class
    {
        if (!request.HasJsonContentType())
        {
            return (null, Problem(
                request,
                StatusCodes.Status415UnsupportedMediaType,
                "Unsupported media type",
                "The request body must be JSON with a 'Content-Type: application/json' header."));
        }

        T? body;
        try
        {
            body = await request.ReadFromJsonAsync<T>(RequestJsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            // Report the JSON path rather than ex.Message, which exposes internal .NET type names.
            logger.LogInformation(ex, "Rejected malformed {RequestType}", typeof(T).Name);
            return (null, Problem(
                request,
                StatusCodes.Status400BadRequest,
                "Malformed request body",
                ex.Path is null or "$"
                    ? "The request body is not valid JSON."
                    : $"The value at '{ex.Path}' is not valid. Check the field's type, format (dates are yyyy-MM-dd) and allowed values."));
        }

        return body is null
            ? (null, Problem(request, StatusCodes.Status400BadRequest, "Missing request body", "A JSON request body is required."))
            : (body, null);
    }

    /// <summary>
    /// Maps a service result to a response: <paramref name="onSuccess"/> for success, otherwise
    /// 400 (validation), 404 (not found) or 409 (conflict, titled <paramref name="conflictTitle"/>).
    /// </summary>
    public static IActionResult ToActionResult<T>(
        HttpRequest request,
        ServiceResult<T> result,
        Func<T, IActionResult> onSuccess,
        string conflictTitle = "Conflict") =>
        result.Status switch
        {
            ServiceResultStatus.Success => onSuccess(result.Value!),
            ServiceResultStatus.ValidationFailed => ValidationProblem(request, result.ValidationErrors),
            ServiceResultStatus.NotFound => Problem(request, StatusCodes.Status404NotFound, "Policy not found", result.Error),
            ServiceResultStatus.Conflict => Problem(request, StatusCodes.Status409Conflict, conflictTitle, result.Error),
            _ => throw new InvalidOperationException($"Unexpected result status '{result.Status}'."),
        };

    public static ObjectResult Problem(HttpRequest request, int status, string title, string? detail) =>
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

    public static BadRequestObjectResult ValidationProblem(HttpRequest request, IReadOnlyDictionary<string, string[]> errors) =>
        new(new ValidationProblemDetails(errors.ToDictionary())
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Detail = "See the errors property for details of each invalid field.",
            Instance = request.Path,
        });
}
