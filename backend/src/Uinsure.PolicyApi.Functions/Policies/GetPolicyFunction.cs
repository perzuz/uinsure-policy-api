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

public sealed class GetPolicyFunction(IPolicyService policyService)
{
    [Function("GetPolicy")]
    [OpenApiOperation(operationId: "GetPolicy", tags: [OpenApiConfiguration.PoliciesTag], Summary = "Retrieve a policy")]
    [OpenApiParameter("reference", In = ParameterLocation.Path, Required = true, Type = typeof(string),
        Description = "The policy reference (case-insensitive).")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, ContentTypes.Json, typeof(PolicyResponse),
        Description = "The policy, with its property, policyholders, terms, payments and refunds.")]
    [OpenApiResponseWithBody(HttpStatusCode.NotFound, ContentTypes.ProblemJson, typeof(ProblemDetails),
        Description = "No policy exists with this reference.")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "policies/{reference}")] HttpRequest request,
        string reference,
        CancellationToken cancellationToken)
    {
        var result = await policyService.GetAsync(reference, cancellationToken);

        return HttpResults.ToActionResult(request, result, policy => new OkObjectResult(policy));
    }
}
