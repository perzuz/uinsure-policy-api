using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Services;
using Uinsure.PolicyApi.Functions.Http;
using Uinsure.PolicyApi.Functions.OpenApi;

namespace Uinsure.PolicyApi.Functions.Policies;

public sealed class SellPolicyFunction(IPolicyService policyService, ILogger<SellPolicyFunction> logger)
{
    [Function("SellPolicy")]
    [OpenApiOperation(
        operationId: "SellPolicy",
        tags: [OpenApiConfiguration.PoliciesTag],
        Summary = "Sell a policy",
        Description = "Creates a policy and records a payment for the full amount. Rules: the reference must be unique " +
            "(case-insensitive, max 20 characters); the start date must be between today and 60 days ahead; the policy " +
            "lasts 1 year (the end date is calculated); 1 to 3 policyholders, each at least 16 on the start date; " +
            "address line 1 and postcode (max 8 characters) are required; the amount must be greater than 0 with at most " +
            "2 decimal places.")]
    [OpenApiRequestBody(ContentTypes.Json, typeof(SellPolicyRequest), Required = true, Example = typeof(SellPolicyRequestExample))]
    [OpenApiResponseWithBody(HttpStatusCode.Created, ContentTypes.Json, typeof(PolicyResponse),
        CustomHeaderType = typeof(LocationHeader), Description = "Policy sold.")]
    [OpenApiResponseWithBody(HttpStatusCode.BadRequest, ContentTypes.ProblemJson, typeof(ValidationProblemDetails),
        Description = "A business rule failed (errors keyed by field, e.g. 'StartDate', 'Property.Postcode', " +
            "'Policyholders[0]'), or the body is not valid JSON.")]
    [OpenApiResponseWithBody(HttpStatusCode.Conflict, ContentTypes.ProblemJson, typeof(ProblemDetails),
        Description = "A policy with this reference already exists.")]
    [OpenApiResponseWithBody(HttpStatusCode.UnsupportedMediaType, ContentTypes.ProblemJson, typeof(ProblemDetails),
        Description = "The request body was not sent as application/json.")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "policies")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var (body, error) = await HttpResults.ReadJsonBodyAsync<SellPolicyRequest>(request, logger, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var result = await policyService.SellAsync(body!, cancellationToken);

        return HttpResults.ToActionResult(
            request,
            result,
            policy => new CreatedResult($"/api/policies/{Uri.EscapeDataString(policy.Reference)}", policy),
            conflictTitle: "Policy already exists");
    }
}
