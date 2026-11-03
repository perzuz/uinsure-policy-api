using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Uinsure.PolicyApi.Core.Domain;
using Uinsure.PolicyApi.Core.Services;

namespace Uinsure.PolicyApi.Infrastructure;

public sealed class SqlitePolicyRepository(PolicyDbContext db) : IPolicyRepository
{
    private const int SqliteConstraintPrimaryKey = 1555;

    public Task<bool> ExistsAsync(string reference, CancellationToken cancellationToken) =>
        db.Policies.AnyAsync(p => p.Reference == reference, cancellationToken);

    // Owned collections (policyholders, terms, payments, refunds) are loaded automatically. A split
    // query loads each collection separately rather than joining them all, which would return
    // one row per combination of their items.
    public Task<Policy?> GetByReferenceAsync(string reference, CancellationToken cancellationToken) =>
        db.Policies.AsNoTracking().AsSplitQuery().SingleOrDefaultAsync(p => p.Reference == reference, cancellationToken);

    public Task<Policy?> GetForUpdateAsync(string reference, CancellationToken cancellationToken) =>
        db.Policies.AsSplitQuery().SingleOrDefaultAsync(p => p.Reference == reference, cancellationToken);

    public async Task<bool> AddAsync(Policy policy, CancellationToken cancellationToken)
    {
        db.Policies.Add(policy);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteConstraintPrimaryKey })
        {
            db.Entry(policy).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
