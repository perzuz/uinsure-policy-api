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

    [Fact]
    public async Task Saving_a_policy_that_another_request_changed_first_fails()
    {
        await AddInNewScopeAsync(NewPolicy("POL-1", "PAY-1"));

        // Two requests load the same policy before either saves.
        await using var first = _api.Services.CreateAsyncScope();
        await using var second = _api.Services.CreateAsyncScope();
        var firstRepository = first.ServiceProvider.GetRequiredService<IPolicyRepository>();
        var secondRepository = second.ServiceProvider.GetRequiredService<IPolicyRepository>();
        var firstCopy = (await firstRepository.GetForUpdateAsync("POL-1", TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var secondCopy = (await secondRepository.GetForUpdateAsync("POL-1", TestContext.Current.CancellationToken)).ShouldNotBeNull();

        firstCopy.Cancel(new DateOnly(2026, 6, 20), () => "REF-1").IsSuccess.ShouldBeTrue();
        secondCopy.Cancel(new DateOnly(2026, 6, 25), () => "REF-2").IsSuccess.ShouldBeTrue();

        (await firstRepository.SaveChangesAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await secondRepository.SaveChangesAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();

        // Only the first cancellation, and its single refund, were stored.
        await using var check = _api.Services.CreateAsyncScope();
        var stored = await check.ServiceProvider.GetRequiredService<IPolicyRepository>()
            .GetByReferenceAsync("POL-1", TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull().CancellationDate.ShouldBe(new DateOnly(2026, 6, 20));
        stored.Refunds.ShouldHaveSingleItem().RefundReference.ShouldBe("REF-1");
    }

    [Fact]
    public async Task Renewal_terms_and_payments_are_persisted()
    {
        var policy = NewPolicy("POL-1", "PAY-1", autoRenew: true);
        await AddInNewScopeAsync(policy);

        await using (var scope = _api.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IPolicyRepository>();
            var loaded = (await repository.GetForUpdateAsync("POL-1", TestContext.Current.CancellationToken)).ShouldNotBeNull();
            loaded.Renew(new DateOnly(2027, 6, 15), 120m, PaymentType.Cheque, () => "PAY-2").IsSuccess.ShouldBeTrue();
            (await repository.SaveChangesAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        }

        await using var check = _api.Services.CreateAsyncScope();
        var stored = (await check.ServiceProvider.GetRequiredService<IPolicyRepository>()
            .GetByReferenceAsync("POL-1", TestContext.Current.CancellationToken)).ShouldNotBeNull();

        stored.Terms.Select(t => (t.TermNumber, t.StartDate, t.Amount)).ShouldBe(
            [(1, new DateOnly(2026, 7, 1), 100m), (2, new DateOnly(2027, 7, 1), 120m)],
            ignoreOrder: true);
        stored.Payments.Single(p => p.TermNumber == 2).PaymentReference.ShouldBe("PAY-2");
        stored.Version.ShouldBe(1);
    }

    private async Task<bool> AddInNewScopeAsync(Policy policy)
    {
        await using var scope = _api.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPolicyRepository>();
        return await repository.AddAsync(policy, TestContext.Current.CancellationToken);
    }

    private static Policy NewPolicy(string reference, string paymentReference, bool autoRenew = false) =>
        Policy.Sell(
            reference,
            InsuranceType.Household,
            new DateOnly(2026, 7, 1),
            100m,
            autoRenew,
            new InsuredProperty("1 High Street", null, null, "AB1 2CD"),
            [new Policyholder("Jane", "Smith", new DateOnly(1990, 1, 1))],
            paymentReference,
            PaymentType.Card);
}
