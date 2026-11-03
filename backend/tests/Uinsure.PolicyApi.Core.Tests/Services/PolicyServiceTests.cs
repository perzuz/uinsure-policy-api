using Microsoft.Extensions.Logging.Abstractions;
using Uinsure.PolicyApi.Core.Domain;
using Uinsure.PolicyApi.Core.Services;
using Uinsure.PolicyApi.Core.Validation;

namespace Uinsure.PolicyApi.Core.Tests.Services;

public sealed class PolicyServiceTests
{
    private readonly InMemoryPolicyRepository _repository = new();
    private readonly PolicyService _service;

    public PolicyServiceTests()
    {
        _service = new PolicyService(
            new SellPolicyRequestValidator(TestData.Clock()),
            _repository,
            new FixedReferenceGenerator("PAY-TEST0001"),
            NullLogger<PolicyService>.Instance);
    }

    [Fact]
    public async Task Selling_a_valid_policy_saves_and_returns_it()
    {
        var request = TestData.ValidRequest();

        var result = await _service.SellAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ServiceResultStatus.Success);
        var policy = result.Value.ShouldNotBeNull();
        policy.Reference.ShouldBe("POL-0001");
        policy.StartDate.ShouldBe(request.StartDate!.Value);
        policy.EndDate.ShouldBe(request.StartDate.Value.AddYears(1));
        policy.HasClaims.ShouldBeFalse();
        policy.AutoRenew.ShouldBeTrue();
        _repository.Policies.ShouldContainKey("POL-0001");
    }

    [Fact]
    public async Task Selling_a_policy_creates_a_payment_for_the_full_amount_using_the_chosen_method()
    {
        var request = TestData.ValidRequest() with { PaymentType = PaymentType.DirectDebit, Amount = 321.45m };

        var result = await _service.SellAsync(request, TestContext.Current.CancellationToken);

        var payment = result.Value.ShouldNotBeNull().Payments.ShouldHaveSingleItem();
        payment.PaymentReference.ShouldBe("PAY-TEST0001");
        payment.Type.ShouldBe(PaymentType.DirectDebit);
        payment.Amount.ShouldBe(321.45m);
    }

    [Fact]
    public async Task Selling_an_invalid_policy_returns_validation_errors_and_saves_nothing()
    {
        var request = TestData.ValidRequest() with { StartDate = TestData.Today.AddDays(61), Policyholders = [] };

        var result = await _service.SellAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ServiceResultStatus.ValidationFailed);
        result.ValidationErrors.Keys.ShouldBe(["StartDate", "Policyholders"], ignoreOrder: true);
        _repository.Policies.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("POL-0001")]
    [InlineData("pol-0001")]
    [InlineData(" Pol-0001 ")]
    public async Task Selling_a_policy_with_an_existing_reference_returns_conflict(string duplicateReference)
    {
        await _service.SellAsync(TestData.ValidRequest(), TestContext.Current.CancellationToken);

        var result = await _service.SellAsync(
            TestData.ValidRequest() with { Reference = duplicateReference },
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ServiceResultStatus.Conflict);
        result.Error.ShouldBe("A policy with reference 'POL-0001' already exists.");
        _repository.Policies.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Losing_a_race_to_insert_the_same_reference_returns_conflict()
    {
        _repository.RejectNextAdd = true;

        var result = await _service.SellAsync(TestData.ValidRequest(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ServiceResultStatus.Conflict);
    }

    [Theory]
    [InlineData("POL-0001")]
    [InlineData("pol-0001")]
    public async Task Retrieving_an_existing_policy_returns_it(string reference)
    {
        await _service.SellAsync(TestData.ValidRequest(), TestContext.Current.CancellationToken);

        var result = await _service.GetAsync(reference, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ServiceResultStatus.Success);
        result.Value.ShouldNotBeNull().Reference.ShouldBe("POL-0001");
    }

    [Fact]
    public async Task Retrieving_an_unknown_policy_returns_not_found()
    {
        var result = await _service.GetAsync("missing", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ServiceResultStatus.NotFound);
        result.Value.ShouldBeNull();
        result.Error.ShouldBe("No policy found with reference 'MISSING'.");
    }

    private sealed class InMemoryPolicyRepository : IPolicyRepository
    {
        public Dictionary<string, Policy> Policies { get; } = [];

        public bool RejectNextAdd { get; set; }

        public Task<bool> ExistsAsync(string reference, CancellationToken cancellationToken) =>
            Task.FromResult(Policies.ContainsKey(reference));

        public Task<Policy?> GetByReferenceAsync(string reference, CancellationToken cancellationToken) =>
            Task.FromResult(Policies.GetValueOrDefault(reference));

        public Task<bool> AddAsync(Policy policy, CancellationToken cancellationToken)
        {
            if (RejectNextAdd)
            {
                RejectNextAdd = false;
                return Task.FromResult(false);
            }

            return Task.FromResult(Policies.TryAdd(policy.Reference, policy));
        }
    }

    private sealed class FixedReferenceGenerator(string paymentReference) : IReferenceGenerator
    {
        public string NewPaymentReference() => paymentReference;
    }
}
