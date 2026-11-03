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

public sealed class RecordClaimFunction(IPolicyService policyService)
{
    [Function("RecordClaim")]
    [OpenApiOperation(
        operationId: "RecordClaim",
        tags: [OpenApiConfiguration.PoliciesTag],
        Summary = "Record a claim on a policy",
        Description = "Marks the policy as having claims. A policy with claims gets no refund when it is cancelled. " +
            "No claim details are stored, and recording a further claim changes nothing.")]
    [OpenApiParameter("reference", In = ParameterLocation.Path, Required = true, Type = typeof(string),
        Description = "The policy reference (case-insensitive).")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, ContentTypes.Json, typeof(PolicyResponse),
        Description = "Claim recorded. The policy now shows hasClaims: true.")]
    [OpenApiResponseWithBody(HttpStatusCode.NotFound, ContentTypes.ProblemJson, typeof(ProblemDetails),
        Description = "No policy exists with this reference.")]
    [OpenApiResponseWithBody(HttpStatusCode.Conflict, ContentTypes.ProblemJson, typeof(ProblemDetails),
        Description = "The policy is cancelled, or was changed by another request.")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "policies/{reference}/claims")] HttpRequest request,
        string reference,
        CancellationToken cancellationToken)
    {
        var result = await policyService.RecordClaimAsync(reference, cancellationToken);

        return HttpResults.ToActionResult(request, result, policy => new OkObjectResult(policy), conflictTitle: "Claim cannot be recorded");
    }
}
