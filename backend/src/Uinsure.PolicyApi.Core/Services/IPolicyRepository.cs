using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Services;

public interface IPolicyRepository
{
    Task<bool> ExistsAsync(string reference, CancellationToken cancellationToken);

    Task<Policy?> GetByReferenceAsync(string reference, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a new policy. Returns <c>false</c> if a policy with the same reference
    /// already exists (e.g. a concurrent sale won the race after the existence check).
    /// </summary>
    Task<bool> AddAsync(Policy policy, CancellationToken cancellationToken);
}
