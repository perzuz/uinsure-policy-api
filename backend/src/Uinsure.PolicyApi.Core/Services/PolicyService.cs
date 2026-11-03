using FluentValidation;
using Microsoft.Extensions.Logging;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Services;

public interface IPolicyService
{
    Task<ServiceResult<PolicyResponse>> SellAsync(SellPolicyRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<PolicyResponse>> GetAsync(string reference, CancellationToken cancellationToken);

    Task<ServiceResult<PolicyResponse>> CancelAsync(string reference, CancelPolicyRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<PolicyResponse>> RenewAsync(string reference, RenewPolicyRequest request, CancellationToken cancellationToken);
}

public sealed class PolicyService(
    IValidator<SellPolicyRequest> sellValidator,
    IValidator<CancelPolicyRequest> cancelValidator,
    IValidator<RenewPolicyRequest> renewValidator,
    IPolicyRepository repository,
    IReferenceGenerator referenceGenerator,
    TimeProvider timeProvider,
    ILogger<PolicyService> logger) : IPolicyService
{
    public async Task<ServiceResult<PolicyResponse>> SellAsync(SellPolicyRequest request, CancellationToken cancellationToken)
    {
        var validation = await sellValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ServiceResult<PolicyResponse>.ValidationFailed(validation.ToDictionary().AsReadOnly());
        }

        var reference = Policy.NormaliseReference(request.Reference!);
        if (await repository.ExistsAsync(reference, cancellationToken))
        {
            return DuplicateReference(reference);
        }

        var policy = Policy.Sell(
            reference,
            request.InsuranceType!.Value,
            request.StartDate!.Value,
            request.Amount!.Value,
            request.AutoRenew,
            new InsuredProperty(
                request.Property!.AddressLine1!,
                request.Property.AddressLine2,
                request.Property.AddressLine3,
                request.Property.Postcode!),
            request.Policyholders!.Select(h => new Policyholder(h.FirstName!, h.LastName!, h.DateOfBirth!.Value)),
            referenceGenerator.NewPaymentReference(),
            request.PaymentType!.Value);

        if (!await repository.AddAsync(policy, cancellationToken))
        {
            return DuplicateReference(reference);
        }

        logger.LogInformation(
            "Sold policy {PolicyReference} starting {StartDate:yyyy-MM-dd} with payment {PaymentReference}",
            policy.Reference,
            policy.StartDate,
            policy.Payments.Single().PaymentReference);

        return ServiceResult<PolicyResponse>.Success(PolicyResponse.From(policy));
    }

    public async Task<ServiceResult<PolicyResponse>> GetAsync(string reference, CancellationToken cancellationToken)
    {
        var normalised = Policy.NormaliseReference(reference);
        var policy = await repository.GetByReferenceAsync(normalised, cancellationToken);

        return policy is null
            ? PolicyNotFound(normalised)
            : ServiceResult<PolicyResponse>.Success(PolicyResponse.From(policy));
    }

    public async Task<ServiceResult<PolicyResponse>> CancelAsync(
        string reference,
        CancelPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await cancelValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ServiceResult<PolicyResponse>.ValidationFailed(validation.ToDictionary().AsReadOnly());
        }

        var result = await UpdateAsync(
            reference,
            policy => policy.Cancel(request.CancellationDate!.Value, referenceGenerator.NewRefundReference),
            cancellationToken);

        if (result.Status == ServiceResultStatus.Success)
        {
            logger.LogInformation(
                "Cancelled policy {PolicyReference} from {CancellationDate:yyyy-MM-dd} with refunds totalling {RefundTotal}",
                result.Value!.Reference,
                result.Value.CancellationDate,
                result.Value.Refunds.Sum(r => r.Amount));
        }

        return result;
    }

    public async Task<ServiceResult<PolicyResponse>> RenewAsync(
        string reference,
        RenewPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await renewValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ServiceResult<PolicyResponse>.ValidationFailed(validation.ToDictionary().AsReadOnly());
        }

        var result = await UpdateAsync(
            reference,
            policy => policy.Renew(
                timeProvider.GetUtcToday(),
                request.Amount!.Value,
                request.PaymentType,
                referenceGenerator.NewPaymentReference),
            cancellationToken);

        if (result.Status == ServiceResultStatus.Success)
        {
            logger.LogInformation(
                "Renewed policy {PolicyReference} for term {TermNumber} starting {StartDate:yyyy-MM-dd}",
                result.Value!.Reference,
                result.Value.Terms[^1].TermNumber,
                result.Value.StartDate);
        }

        return result;
    }

    /// <summary>Loads a policy, applies a domain operation to it and saves the result.</summary>
    private async Task<ServiceResult<PolicyResponse>> UpdateAsync(
        string reference,
        Func<Policy, DomainResult> operation,
        CancellationToken cancellationToken)
    {
        var normalised = Policy.NormaliseReference(reference);
        var policy = await repository.GetForUpdateAsync(normalised, cancellationToken);
        if (policy is null)
        {
            return PolicyNotFound(normalised);
        }

        var outcome = operation(policy);
        if (!outcome.IsSuccess)
        {
            return outcome.Field is null
                ? ServiceResult<PolicyResponse>.Conflict(outcome.Error!)
                : ServiceResult<PolicyResponse>.ValidationFailed(
                    new Dictionary<string, string[]> { [outcome.Field] = [outcome.Error!] });
        }

        if (!await repository.SaveChangesAsync(cancellationToken))
        {
            return ServiceResult<PolicyResponse>.Conflict(
                $"Policy '{normalised}' was changed by another request. Retrieve it and try again.");
        }

        return ServiceResult<PolicyResponse>.Success(PolicyResponse.From(policy));
    }

    private static ServiceResult<PolicyResponse> PolicyNotFound(string reference) =>
        ServiceResult<PolicyResponse>.NotFound($"No policy found with reference '{reference}'.");

    private static ServiceResult<PolicyResponse> DuplicateReference(string reference) =>
        ServiceResult<PolicyResponse>.Conflict($"A policy with reference '{reference}' already exists.");
}
