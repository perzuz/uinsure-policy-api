using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Services;
using Uinsure.PolicyApi.Functions.Http;
using Uinsure.PolicyApi.Functions.OpenApi;

namespace Uinsure.PolicyApi.Functions.Policies;

public sealed class CancelPolicyFunction(IPolicyService policyService, ILogger<CancelPolicyFunction> logger)
{
    [Function("CancelPolicy")]
    [OpenApiOperation(
        operationId: "CancelPolicy",
        tags: [OpenApiConfiguration.PoliciesTag],
        Summary = "Cancel a policy",
        Description = "Cancels the policy from the given date and refunds each paid term to the same method as its " +
            "payment: in full if cancelled before the term starts or within its first 14 days (cooling off), otherwise " +
            "pro rata for the days of cover not used. The date must be today or later and no later than the end date.")]
    [OpenApiParameter("reference", In = ParameterLocation.Path, Required = true, Type = typeof(string),
        Description = "The policy reference (case-insensitive).")]
    [OpenApiRequestBody(ContentTypes.Json, typeof(CancelPolicyRequest), Required = true, Example = typeof(CancelPolicyRequestExample))]
    [OpenApiResponseWithBody(HttpStatusCode.OK, ContentTypes.Json, typeof(PolicyResponse),
        Description = "Policy cancelled. The refunds are listed on the policy.")]
    [OpenApiResponseWithBody(HttpStatusCode.BadRequest, ContentTypes.ProblemJson, typeof(ValidationProblemDetails),
        Description = "The cancellation date is missing, in the past or after the policy end date, or the body is not valid JSON.")]
    [OpenApiResponseWithBody(HttpStatusCode.NotFound, ContentTypes.ProblemJson, typeof(ProblemDetails),
        Description = "No policy exists with this reference.")]
    [OpenApiResponseWithBody(HttpStatusCode.Conflict, ContentTypes.ProblemJson, typeof(ProblemDetails),
        Description = "The policy is already cancelled, or was changed by another request.")]
    [OpenApiResponseWithBody(HttpStatusCode.UnsupportedMediaType, ContentTypes.ProblemJson, typeof(ProblemDetails),
        Description = "The request body was not sent as application/json.")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "policies/{reference}/cancel")] HttpRequest request,
        string reference,
        CancellationToken cancellationToken)
    {
        var (body, error) = await HttpResults.ReadJsonBodyAsync<CancelPolicyRequest>(request, logger, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var result = await policyService.CancelAsync(reference, body!, cancellationToken);

        return HttpResults.ToActionResult(request, result, policy => new OkObjectResult(policy), conflictTitle: "Policy cannot be cancelled");
    }
}
