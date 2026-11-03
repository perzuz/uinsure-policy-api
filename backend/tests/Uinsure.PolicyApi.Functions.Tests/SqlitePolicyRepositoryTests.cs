using Microsoft.Extensions.DependencyInjection;
using Uinsure.PolicyApi.Core.Domain;
using Uinsure.PolicyApi.Core.Services;

namespace Uinsure.PolicyApi.Functions.Tests;

public sealed class SqlitePolicyRepositoryTests : IAsyncDisposable
{
    private readonly PolicyApiHarness _api = new();

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    [Fact]
    public async Task Database_rejects_a_duplicate_reference_that_slips_past_the_existence_check()
    {
        // Simulates two concurrent requests: both checked ExistsAsync before either saved.
        (await AddInNewScopeAsync(NewPolicy("POL-RACE", "PAY-1"))).ShouldBeTrue();

        (await AddInNewScopeAsync(NewPolicy("POL-RACE", "PAY-2"))).ShouldBeFalse();
    }

    [Fact]
    public async Task Exists_reflects_saved_policies()
    {
        await AddInNewScopeAsync(NewPolicy("POL-1", "PAY-1"));

        await using var scope = _api.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPolicyRepository>();

        (await repository.ExistsAsync("POL-1", TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await repository.ExistsAsync("POL-2", TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    private async Task<bool> AddInNewScopeAsync(Policy policy)
    {
        await using var scope = _api.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPolicyRepository>();
        return await repository.AddAsync(policy, TestContext.Current.CancellationToken);
    }

    private static Policy NewPolicy(string reference, string paymentReference) =>
        Policy.Sell(
            reference,
            InsuranceType.Household,
            new DateOnly(2026, 7, 1),
            100m,
            autoRenew: false,
            new InsuredProperty("1 High Street", null, null, "AB1 2CD"),
            [new Policyholder("Jane", "Smith", new DateOnly(1990, 1, 1))],
            new Payment(paymentReference, PaymentType.Card, 100m));
}
