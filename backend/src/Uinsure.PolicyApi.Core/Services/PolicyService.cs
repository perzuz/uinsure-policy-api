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

    /// <summary>The refunds cancelling would give, without cancelling. A missing date means today.</summary>
    Task<ServiceResult<CancellationQuoteResponse>> QuoteCancellationAsync(
        string reference,
        CancelPolicyRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<PolicyResponse>> RecordClaimAsync(string reference, CancellationToken cancellationToken);
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
            ? PolicyNotFound<PolicyResponse>(normalised)
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

    public async Task<ServiceResult<CancellationQuoteResponse>> QuoteCancellationAsync(
        string reference,
        CancelPolicyRequest request,
        CancellationToken cancellationToken)
    {
        request = request with { CancellationDate = request.CancellationDate ?? timeProvider.GetUtcToday() };

        var validation = await cancelValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ServiceResult<CancellationQuoteResponse>.ValidationFailed(validation.ToDictionary().AsReadOnly());
        }

        var normalised = Policy.NormaliseReference(reference);
        var policy = await repository.GetByReferenceAsync(normalised, cancellationToken);
        if (policy is null)
        {
            return PolicyNotFound<CancellationQuoteResponse>(normalised);
        }

        var cancellationDate = request.CancellationDate!.Value;
        var quote = policy.QuoteCancellation(cancellationDate);
        if (!quote.IsSuccess)
        {
            return DomainFailure<CancellationQuoteResponse>(quote.Error!, quote.Field);
        }

        return ServiceResult<CancellationQuoteResponse>.Success(
            CancellationQuoteResponse.From(policy, cancellationDate, quote.Value!));
    }

    public async Task<ServiceResult<PolicyResponse>> RecordClaimAsync(string reference, CancellationToken cancellationToken)
    {
        var result = await UpdateAsync(reference, policy => policy.RecordClaim(), cancellationToken);

        if (result.Status == ServiceResultStatus.Success)
        {
            logger.LogInformation("Recorded a claim on policy {PolicyReference}", result.Value!.Reference);
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
            return PolicyNotFound<PolicyResponse>(normalised);
        }

        var outcome = operation(policy);
        if (!outcome.IsSuccess)
        {
            return DomainFailure<PolicyResponse>(outcome.Error!, outcome.Field);
        }

        if (!await repository.SaveChangesAsync(cancellationToken))
        {
            return ServiceResult<PolicyResponse>.Conflict(
                $"Policy '{normalised}' was changed by another request. Retrieve it and try again.");
        }

        return ServiceResult<PolicyResponse>.Success(PolicyResponse.From(policy));
    }

    /// <summary>A rejection that names a request field is a validation error; any other is a conflict with the policy's state.</summary>
    private static ServiceResult<T> DomainFailure<T>(string error, string? field) =>
        field is null
            ? ServiceResult<T>.Conflict(error)
            : ServiceResult<T>.ValidationFailed(new Dictionary<string, string[]> { [field] = [error] });

    private static ServiceResult<T> PolicyNotFound<T>(string reference) =>
        ServiceResult<T>.NotFound($"No policy found with reference '{reference}'.");

    private static ServiceResult<PolicyResponse> DuplicateReference(string reference) =>
        ServiceResult<PolicyResponse>.Conflict($"A policy with reference '{reference}' already exists.");
}
