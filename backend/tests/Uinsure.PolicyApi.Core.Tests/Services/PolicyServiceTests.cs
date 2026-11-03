using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Domain;
using Uinsure.PolicyApi.Core.Services;
using Uinsure.PolicyApi.Core.Validation;

namespace Uinsure.PolicyApi.Core.Tests.Services;

public sealed class PolicyServiceTests
{
    private readonly FakeTimeProvider _clock = TestData.Clock();
    private readonly InMemoryPolicyRepository _repository = new();
    private readonly PolicyService _service;

    public PolicyServiceTests()
    {
        _service = new PolicyService(
            new SellPolicyRequestValidator(_clock),
            new CancelPolicyRequestValidator(_clock),
            new RenewPolicyRequestValidator(),
            _repository,
            new SequentialReferenceGenerator(),
            _clock,
            NullLogger<PolicyService>.Instance);
    }

    // TestData.ValidRequest() starts 7 days after TestData.Today (2026-06-22) and ends 2027-06-22.
    private static readonly DateOnly PolicyStart = TestData.Today.AddDays(7);
    private static readonly DateOnly PolicyEnd = PolicyStart.AddYears(1);

    // --- Sell ---

    [Fact]
    public async Task Selling_a_valid_policy_saves_and_returns_it()
    {
        var request = TestData.ValidRequest();

        var result = await _service.SellAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ServiceResultStatus.Success);
        var policy = result.Value.ShouldNotBeNull();
        policy.Reference.ShouldBe("POL-0001");
        policy.Status.ShouldBe(PolicyStatus.Active);
        policy.StartDate.ShouldBe(request.StartDate!.Value);
        policy.EndDate.ShouldBe(request.StartDate.Value.AddYears(1));
        policy.HasClaims.ShouldBeFalse();
        policy.AutoRenew.ShouldBeTrue();
        policy.Terms.ShouldHaveSingleItem().TermNumber.ShouldBe(1);
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
        payment.TermNumber.ShouldBe(1);
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
        await SellAsync();

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

    // --- Get ---

    [Theory]
    [InlineData("POL-0001")]
    [InlineData("pol-0001")]
    public async Task Retrieving_an_existing_policy_returns_it(string reference)
    {
        await SellAsync();

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

    // --- Cancel ---

    [Fact]
    public async Task Cancelling_a_policy_saves_it_as_cancelled_with_its_refund()
    {
        await SellAsync();

        var result = await CancelAsync("pol-0001", TestData.Today);

        result.Status.ShouldBe(ServiceResultStatus.Success);
        var policy = result.Value.ShouldNotBeNull();
        policy.Status.ShouldBe(PolicyStatus.Cancelled);
        policy.CancellationDate.ShouldBe(TestData.Today);
        var refund = policy.Refunds.ShouldHaveSingleItem();
        refund.RefundReference.ShouldBe("REF-TEST0001");
        refund.Amount.ShouldBe(250.00m);
        refund.Type.ShouldBe(PaymentType.Card);
        _repository.SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task Cancelling_requires_a_cancellation_date()
    {
        await SellAsync();

        var result = await _service.CancelAsync("POL-0001", new CancelPolicyRequest(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ServiceResultStatus.ValidationFailed);
        result.ValidationErrors.Keys.ShouldBe(["CancellationDate"]);
        _repository.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Cancelling_from_a_past_date_is_invalid()
    {
        await SellAsync();

        var result = await CancelAsync("POL-0001", TestData.Today.AddDays(-1));

        result.Status.ShouldBe(ServiceResultStatus.ValidationFailed);
        result.ValidationErrors["CancellationDate"].ShouldBe(["'Cancellation Date' cannot be in the past."]);
    }

    [Fact]
    public async Task Cancelling_from_after_the_end_date_is_invalid()
    {
        await SellAsync();

        var result = await CancelAsync("POL-0001", PolicyEnd.AddDays(1));

        result.Status.ShouldBe(ServiceResultStatus.ValidationFailed);
        result.ValidationErrors["CancellationDate"].ShouldBe(["'Cancellation Date' cannot be after the policy end date (2027-06-22)."]);
        _repository.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Cancelling_an_unknown_policy_returns_not_found()
    {
        var result = await CancelAsync("missing", TestData.Today);

        result.Status.ShouldBe(ServiceResultStatus.NotFound);
        result.Error.ShouldBe("No policy found with reference 'MISSING'.");
    }

    [Fact]
    public async Task Cancelling_an_already_cancelled_policy_returns_conflict()
    {
        await SellAsync();
        await CancelAsync("POL-0001", TestData.Today);

        var result = await CancelAsync("POL-0001", TestData.Today.AddDays(1));

        result.Status.ShouldBe(ServiceResultStatus.Conflict);
        result.Error.ShouldBe("The policy has already been cancelled from 2026-06-15.");
    }

    [Fact]
    public async Task A_concurrent_change_returns_conflict()
    {
        await SellAsync();
        _repository.FailNextSave = true;

        var result = await CancelAsync("POL-0001", TestData.Today);

        result.Status.ShouldBe(ServiceResultStatus.Conflict);
        result.Error.ShouldBe("Policy 'POL-0001' was changed by another request. Retrieve it and try again.");
    }

    // --- Renew ---

    [Fact]
    public async Task Renewing_within_the_window_adds_a_term_and_takes_a_payment_for_an_auto_renewing_policy()
    {
        await SellAsync();
        SetToday(PolicyEnd.AddDays(-10));

        var result = await RenewAsync("POL-0001", 300.00m, PaymentType.DirectDebit);

        result.Status.ShouldBe(ServiceResultStatus.Success);
        var policy = result.Value.ShouldNotBeNull();
        policy.Terms.Count.ShouldBe(2);
        policy.StartDate.ShouldBe(PolicyEnd);
        policy.EndDate.ShouldBe(PolicyEnd.AddYears(1));
        policy.Amount.ShouldBe(300.00m);
        var payment = policy.Payments.Single(p => p.TermNumber == 2);
        payment.PaymentReference.ShouldBe("PAY-TEST0002");
        payment.Type.ShouldBe(PaymentType.DirectDebit);
        payment.Amount.ShouldBe(300.00m);
        _repository.SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task Renewing_a_policy_that_does_not_auto_renew_takes_no_payment()
    {
        await SellAsync(TestData.ValidRequest() with { AutoRenew = false });
        SetToday(PolicyEnd);

        var result = await RenewAsync("POL-0001", 300.00m, paymentType: null);

        result.Status.ShouldBe(ServiceResultStatus.Success);
        result.Value.ShouldNotBeNull().Terms.Count.ShouldBe(2);
        result.Value.Payments.ShouldHaveSingleItem().TermNumber.ShouldBe(1);
    }

    [Fact]
    public async Task Renewing_before_the_window_opens_returns_conflict()
    {
        await SellAsync();

        var result = await RenewAsync("POL-0001", 300.00m, PaymentType.Card);

        result.Status.ShouldBe(ServiceResultStatus.Conflict);
        result.Error.ShouldBe(
            "A policy can only be renewed in the 30 days before its end date. This policy can be renewed from 2027-05-23.");
        _repository.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task Renewing_an_auto_renewing_policy_requires_a_payment_type()
    {
        await SellAsync();
        SetToday(PolicyEnd.AddDays(-10));

        var result = await RenewAsync("POL-0001", 300.00m, paymentType: null);

        result.Status.ShouldBe(ServiceResultStatus.ValidationFailed);
        result.ValidationErrors.Keys.ShouldBe(["PaymentType"]);
        _repository.SaveCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("10.001")]
    public async Task Renewing_requires_a_valid_amount(string? amount)
    {
        await SellAsync();

        var result = await RenewAsync("POL-0001", amount is null ? null : decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), PaymentType.Card);

        result.Status.ShouldBe(ServiceResultStatus.ValidationFailed);
        result.ValidationErrors.Keys.ShouldBe(["Amount"]);
    }

    [Fact]
    public async Task Renewing_an_unknown_policy_returns_not_found()
    {
        var result = await RenewAsync("missing", 300.00m, PaymentType.Card);

        result.Status.ShouldBe(ServiceResultStatus.NotFound);
    }

    private async Task SellAsync(SellPolicyRequest? request = null) =>
        (await _service.SellAsync(request ?? TestData.ValidRequest(), TestContext.Current.CancellationToken))
            .Status.ShouldBe(ServiceResultStatus.Success);

    private Task<ServiceResult<PolicyResponse>> CancelAsync(string reference, DateOnly cancellationDate) =>
        _service.CancelAsync(
            reference,
            new CancelPolicyRequest { CancellationDate = cancellationDate },
            TestContext.Current.CancellationToken);

    private Task<ServiceResult<PolicyResponse>> RenewAsync(string reference, decimal? amount, PaymentType? paymentType) =>
        _service.RenewAsync(
            reference,
            new RenewPolicyRequest { Amount = amount, PaymentType = paymentType },
            TestContext.Current.CancellationToken);

    private void SetToday(DateOnly today) => _clock.SetUtcNow(new DateTimeOffset(today, TimeOnly.MinValue, TimeSpan.Zero));

    private sealed class InMemoryPolicyRepository : IPolicyRepository
    {
        public Dictionary<string, Policy> Policies { get; } = [];

        public bool RejectNextAdd { get; set; }

        public bool FailNextSave { get; set; }

        public int SaveCount { get; private set; }

        public Task<bool> ExistsAsync(string reference, CancellationToken cancellationToken) =>
            Task.FromResult(Policies.ContainsKey(reference));

        public Task<Policy?> GetByReferenceAsync(string reference, CancellationToken cancellationToken) =>
            Task.FromResult(Policies.GetValueOrDefault(reference));

        public Task<Policy?> GetForUpdateAsync(string reference, CancellationToken cancellationToken) =>
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

        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                return Task.FromResult(false);
            }

            SaveCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class SequentialReferenceGenerator : IReferenceGenerator
    {
        private int _payments;
        private int _refunds;

        public string NewPaymentReference() => $"PAY-TEST{++_payments:0000}";

        public string NewRefundReference() => $"REF-TEST{++_refunds:0000}";
    }
}
