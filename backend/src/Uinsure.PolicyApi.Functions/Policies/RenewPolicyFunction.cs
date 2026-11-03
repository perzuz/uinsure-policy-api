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

public sealed class RenewPolicyFunction(IPolicyService policyService, ILogger<RenewPolicyFunction> logger)
{
    [Function("RenewPolicy")]
    [OpenApiOperation(
        operationId: "RenewPolicy",
        tags: [OpenApiConfiguration.PoliciesTag],
        Summary = "Renew a policy",
        Description = "Adds a new one-year term starting on the current end date. Allowed from 30 days before the end " +
            "date up to and including the end date. A payment for the new term is taken only if the policy is set to " +
            "auto renew, in which case paymentType is required.")]
    [OpenApiParameter("reference", In = ParameterLocation.Path, Required = true, Type = typeof(string),
        Description = "The policy reference (case-insensitive).")]
    [OpenApiRequestBody(ContentTypes.Json, typeof(RenewPolicyRequest), Required = true, Example = typeof(RenewPolicyRequestExample))]
    [OpenApiResponseWithBody(HttpStatusCode.OK, ContentTypes.Json, typeof(PolicyResponse),
        Description = "Policy renewed. The new term, and its payment if one was taken, are listed on the policy.")]
    [OpenApiResponseWithBody(HttpStatusCode.BadRequest, ContentTypes.ProblemJson, typeof(ValidationProblemDetails),
        Description = "The amount is invalid, the payment type is missing for an auto-renewing policy, or the body is not valid JSON.")]
    [OpenApiResponseWithBody(HttpStatusCode.NotFound, ContentTypes.ProblemJson, typeof(ProblemDetails),
        Description = "No policy exists with this reference.")]
    [OpenApiResponseWithBody(HttpStatusCode.Conflict, ContentTypes.ProblemJson, typeof(ProblemDetails),
        Description = "The policy is outside its renewal window, already renewed, cancelled, or was changed by another request.")]
    [OpenApiResponseWithBody(HttpStatusCode.UnsupportedMediaType, ContentTypes.ProblemJson, typeof(ProblemDetails),
        Description = "The request body was not sent as application/json.")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "policies/{reference}/renew")] HttpRequest request,
        string reference,
        CancellationToken cancellationToken)
    {
        var (body, error) = await HttpResults.ReadJsonBodyAsync<RenewPolicyRequest>(request, logger, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var result = await policyService.RenewAsync(reference, body!, cancellationToken);

        return HttpResults.ToActionResult(request, result, policy => new OkObjectResult(policy), conflictTitle: "Policy cannot be renewed");
    }
}
