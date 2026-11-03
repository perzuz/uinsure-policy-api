using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Services;

public interface IPolicyRepository
{
    Task<bool> ExistsAsync(string reference, CancellationToken cancellationToken);

    /// <summary>Loads a policy for reading only; changes to it are not saved.</summary>
    Task<Policy?> GetByReferenceAsync(string reference, CancellationToken cancellationToken);

    /// <summary>Loads a policy so that changes made to it are saved by <see cref="SaveChangesAsync"/>.</summary>
    Task<Policy?> GetForUpdateAsync(string reference, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a new policy. Returns <c>false</c> if a policy with the same reference
    /// already exists (e.g. a concurrent sale won the race after the existence check).
    /// </summary>
    Task<bool> AddAsync(Policy policy, CancellationToken cancellationToken);

    /// <summary>
    /// Saves changes to policies loaded with <see cref="GetForUpdateAsync"/>. Returns <c>false</c>
    /// if the policy was changed by another request after it was loaded.
    /// </summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}
