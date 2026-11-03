using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Uinsure.PolicyApi.Core.Services;

namespace Uinsure.PolicyApi.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPolicyInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddPolicyInfrastructure(options => options.UseSqlite(connectionString));

    public static IServiceCollection AddPolicyInfrastructure(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddDbContext<PolicyDbContext>(configureDbContext);
        services.AddScoped<IPolicyRepository, SqlitePolicyRepository>();

        return services;
    }

    /// <summary>
    /// Creates the database schema if it doesn't exist. Migrations are deliberately not
    /// used for this exercise; a production system would use EF Core migrations instead.
    /// </summary>
    public static async Task EnsurePolicyDatabaseCreatedAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PolicyDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
