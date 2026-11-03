using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Services;
using Uinsure.PolicyApi.Functions.Http;
using Uinsure.PolicyApi.Functions.OpenApi;

namespace Uinsure.PolicyApi.Functions.Policies;

public sealed class GetCancellationQuoteFunction(IPolicyService policyService)
{
    private const string DateFormat = "yyyy-MM-dd";

    [Function("GetCancellationQuote")]
    [OpenApiOperation(
        operationId: "GetCancellationQuote",
        tags: [OpenApiConfiguration.PoliciesTag],
        Summary = "Quote the refund for cancelling a policy",
        Description = "Returns the refunds that cancelling the policy on the given date would give, using the same rules " +
            "as cancelling, without cancelling it. A policy with claims gets no refund.")]
    [OpenApiParameter("reference", In = ParameterLocation.Path, Required = true, Type = typeof(string),
        Description = "The policy reference (case-insensitive).")]
    [OpenApiParameter("cancellationDate", In = ParameterLocation.Query, Required = false, Type = typeof(string),
        Description = "The date cover would end, as yyyy-MM-dd. Defaults to today. Must be today or later and no later than the end date.")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, ContentTypes.Json, typeof(CancellationQuoteResponse),
        Description = "The refunds cancelling on this date would give, per paid term.")]
    [OpenApiResponseWithBody(HttpStatusCode.BadRequest, ContentTypes.ProblemJson, typeof(ValidationProblemDetails),
        Description = "The cancellation date is not a yyyy-MM-dd date, is in the past, or is after the policy end date.")]
    [OpenApiResponseWithBody(HttpStatusCode.NotFound, ContentTypes.ProblemJson, typeof(ProblemDetails),
        Description = "No policy exists with this reference.")]
    [OpenApiResponseWithBody(HttpStatusCode.Conflict, ContentTypes.ProblemJson, typeof(ProblemDetails),
        Description = "The policy is already cancelled.")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "policies/{reference}/cancellation-quote")] HttpRequest request,
        string reference,
        CancellationToken cancellationToken)
    {
        DateOnly? cancellationDate = null;
        string? dateText = request.Query["cancellationDate"];
        if (!string.IsNullOrEmpty(dateText))
        {
            if (!DateOnly.TryParseExact(dateText, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return HttpResults.ValidationProblem(request, new Dictionary<string, string[]>
                {
                    ["CancellationDate"] = [$"'Cancellation Date' must be a date in the format {DateFormat}."],
                });
            }

            cancellationDate = parsed;
        }

        var result = await policyService.QuoteCancellationAsync(
            reference,
            new CancelPolicyRequest { CancellationDate = cancellationDate },
            cancellationToken);

        return HttpResults.ToActionResult(request, result, quote => new OkObjectResult(quote), conflictTitle: "Policy cannot be cancelled");
    }
}
