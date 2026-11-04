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
    public async Task GIVEN_a_valid_request_WHEN_selling_THEN_the_policy_is_saved_and_returned()
    {
        // Arrange
        var request = TestData.ValidRequest();

        // Act
        var result = await _service.SellAsync(request, TestContext.Current.CancellationToken);

        // Assert
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
    public async Task GIVEN_a_valid_request_WHEN_selling_THEN_a_payment_is_taken_for_the_full_amount_by_the_chosen_method()
    {
        // Arrange
        var request = TestData.ValidRequest() with { PaymentType = PaymentType.DirectDebit, Amount = 321.45m };

        // Act
        var result = await _service.SellAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var payment = result.Value.ShouldNotBeNull().Payments.ShouldHaveSingleItem();
        payment.PaymentReference.ShouldBe("PAY-TEST0001");
        payment.Type.ShouldBe(PaymentType.DirectDebit);
        payment.Amount.ShouldBe(321.45m);
        payment.TermNumber.ShouldBe(1);
    }

    [Fact]
    public async Task GIVEN_an_invalid_request_WHEN_selling_THEN_validation_errors_are_returned_and_nothing_is_saved()
    {
        // Arrange
        var request = TestData.ValidRequest() with { StartDate = TestData.Today.AddDays(61), Policyholders = [] };

        // Act
        var result = await _service.SellAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.ValidationFailed);
        result.ValidationErrors.Keys.ShouldBe(["StartDate", "Policyholders"], ignoreOrder: true);
        _repository.Policies.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("POL-0001")]
    [InlineData("pol-0001")]
    [InlineData(" Pol-0001 ")]
    public async Task GIVEN_an_existing_reference_WHEN_selling_THEN_conflict_is_returned(string duplicateReference)
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await _service.SellAsync(
            TestData.ValidRequest() with { Reference = duplicateReference },
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.Conflict);
        result.Error.ShouldBe("A policy with reference 'POL-0001' already exists.");
        _repository.Policies.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GIVEN_another_request_inserts_the_same_reference_first_WHEN_selling_THEN_conflict_is_returned()
    {
        // Arrange
        _repository.RejectNextAdd = true;

        // Act
        var result = await _service.SellAsync(TestData.ValidRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.Conflict);
    }

    // --- Get ---

    [Theory]
    [InlineData("POL-0001")]
    [InlineData("pol-0001")]
    public async Task GIVEN_an_existing_policy_WHEN_retrieving_it_THEN_it_is_returned(string reference)
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await _service.GetAsync(reference, TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.Success);
        result.Value.ShouldNotBeNull().Reference.ShouldBe("POL-0001");
    }

    [Fact]
    public async Task GIVEN_an_unknown_reference_WHEN_retrieving_THEN_not_found_is_returned()
    {
        // Act
        var result = await _service.GetAsync("missing", TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.NotFound);
        result.Value.ShouldBeNull();
        result.Error.ShouldBe("No policy found with reference 'MISSING'.");
    }

    // --- Cancel ---

    [Fact]
    public async Task GIVEN_an_active_policy_WHEN_cancelling_THEN_it_is_saved_as_cancelled_with_its_refund()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await CancelAsync("pol-0001", TestData.Today);

        // Assert
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
    public async Task GIVEN_no_cancellation_date_WHEN_cancelling_THEN_validation_fails_and_nothing_is_saved()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await _service.CancelAsync("POL-0001", new CancelPolicyRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.ValidationFailed);
        result.ValidationErrors.Keys.ShouldBe(["CancellationDate"]);
        _repository.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task GIVEN_a_past_date_WHEN_cancelling_THEN_validation_fails()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await CancelAsync("POL-0001", TestData.Today.AddDays(-1));

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.ValidationFailed);
        result.ValidationErrors["CancellationDate"].ShouldBe(["'Cancellation Date' cannot be in the past."]);
    }

    [Fact]
    public async Task GIVEN_a_date_after_the_end_date_WHEN_cancelling_THEN_validation_fails_and_nothing_is_saved()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await CancelAsync("POL-0001", PolicyEnd.AddDays(1));

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.ValidationFailed);
        result.ValidationErrors["CancellationDate"].ShouldBe(["'Cancellation Date' cannot be after the policy end date (2027-06-22)."]);
        _repository.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task GIVEN_an_unknown_reference_WHEN_cancelling_THEN_not_found_is_returned()
    {
        // Act
        var result = await CancelAsync("missing", TestData.Today);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.NotFound);
        result.Error.ShouldBe("No policy found with reference 'MISSING'.");
    }

    [Fact]
    public async Task GIVEN_a_cancelled_policy_WHEN_cancelling_again_THEN_conflict_is_returned()
    {
        // Arrange
        await SellAsync();
        await CancelAsync("POL-0001", TestData.Today);

        // Act
        var result = await CancelAsync("POL-0001", TestData.Today.AddDays(1));

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.Conflict);
        result.Error.ShouldBe("The policy has already been cancelled from 2026-06-15.");
    }

    [Fact]
    public async Task GIVEN_another_request_changed_the_policy_first_WHEN_saving_THEN_conflict_is_returned()
    {
        // Arrange
        await SellAsync();
        _repository.FailNextSave = true;

        // Act
        var result = await CancelAsync("POL-0001", TestData.Today);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.Conflict);
        result.Error.ShouldBe("Policy 'POL-0001' was changed by another request. Retrieve it and try again.");
    }

    // --- Renew ---

    [Fact]
    public async Task GIVEN_an_auto_renewing_policy_in_its_window_WHEN_renewing_THEN_a_term_is_added_and_a_payment_taken()
    {
        // Arrange
        await SellAsync();
        SetToday(PolicyEnd.AddDays(-10));

        // Act
        var result = await RenewAsync("POL-0001", 300.00m, PaymentType.DirectDebit);

        // Assert
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
    public async Task GIVEN_a_policy_that_does_not_auto_renew_WHEN_renewing_THEN_no_payment_is_taken()
    {
        // Arrange
        await SellAsync(TestData.ValidRequest() with { AutoRenew = false });
        SetToday(PolicyEnd);

        // Act
        var result = await RenewAsync("POL-0001", 300.00m, paymentType: null);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.Success);
        result.Value.ShouldNotBeNull().Terms.Count.ShouldBe(2);
        result.Value.Payments.ShouldHaveSingleItem().TermNumber.ShouldBe(1);
    }

    [Fact]
    public async Task GIVEN_the_renewal_window_has_not_opened_WHEN_renewing_THEN_conflict_is_returned_and_nothing_is_saved()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await RenewAsync("POL-0001", 300.00m, PaymentType.Card);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.Conflict);
        result.Error.ShouldBe(
            "A policy can only be renewed in the 30 days before its end date. This policy can be renewed from 2027-05-23.");
        _repository.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task GIVEN_an_auto_renewing_policy_WHEN_renewing_without_a_payment_type_THEN_validation_fails()
    {
        // Arrange
        await SellAsync();
        SetToday(PolicyEnd.AddDays(-10));

        // Act
        var result = await RenewAsync("POL-0001", 300.00m, paymentType: null);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.ValidationFailed);
        result.ValidationErrors.Keys.ShouldBe(["PaymentType"]);
        _repository.SaveCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("10.001")]
    public async Task GIVEN_a_missing_or_invalid_amount_WHEN_renewing_THEN_validation_fails(string? amount)
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await RenewAsync("POL-0001", amount is null ? null : decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), PaymentType.Card);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.ValidationFailed);
        result.ValidationErrors.Keys.ShouldBe(["Amount"]);
    }

    [Fact]
    public async Task GIVEN_an_unknown_reference_WHEN_renewing_THEN_not_found_is_returned()
    {
        // Act
        var result = await RenewAsync("missing", 300.00m, PaymentType.Card);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_an_auto_renewing_policy_WHEN_renewing_by_cheque_THEN_validation_fails_and_nothing_is_saved()
    {
        // Arrange
        await SellAsync();
        SetToday(PolicyEnd.AddDays(-10));

        // Act
        var result = await RenewAsync("POL-0001", 300.00m, PaymentType.Cheque);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.ValidationFailed);
        result.ValidationErrors["PaymentType"].ShouldBe([PolicyRules.AutoRenewByChequeError]);
        _repository.SaveCount.ShouldBe(0);
    }

    // --- Cancellation quote ---

    [Fact]
    public async Task GIVEN_an_active_policy_WHEN_quoting_a_cancellation_THEN_the_refunds_are_returned_without_saving()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await QuoteAsync("pol-0001", TestData.Today);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.Success);
        var quote = result.Value.ShouldNotBeNull();
        quote.Reference.ShouldBe("POL-0001");
        quote.CancellationDate.ShouldBe(TestData.Today);
        quote.HasClaims.ShouldBeFalse();
        quote.TotalRefund.ShouldBe(250.00m);
        quote.Refunds.ShouldBe([new RefundQuoteResponse(1, PaymentType.Card, 250.00m)]);
        _repository.SaveCount.ShouldBe(0);
    }

    [Fact]
    public async Task GIVEN_no_date_WHEN_quoting_a_cancellation_THEN_it_is_quoted_for_today()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await QuoteAsync("POL-0001", null);

        // Assert
        result.Value.ShouldNotBeNull().CancellationDate.ShouldBe(TestData.Today);
    }

    [Fact]
    public async Task GIVEN_a_past_date_WHEN_quoting_a_cancellation_THEN_validation_fails()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await QuoteAsync("POL-0001", TestData.Today.AddDays(-1));

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.ValidationFailed);
        result.ValidationErrors["CancellationDate"].ShouldBe(["'Cancellation Date' cannot be in the past."]);
    }

    [Fact]
    public async Task GIVEN_a_date_after_the_end_date_WHEN_quoting_a_cancellation_THEN_validation_fails()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await QuoteAsync("POL-0001", PolicyEnd.AddDays(1));

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.ValidationFailed);
        result.ValidationErrors.Keys.ShouldBe(["CancellationDate"]);
    }

    [Fact]
    public async Task GIVEN_a_cancelled_policy_WHEN_quoting_a_cancellation_THEN_conflict_is_returned()
    {
        // Arrange
        await SellAsync();
        await CancelAsync("POL-0001", TestData.Today);

        // Act
        var result = await QuoteAsync("POL-0001", TestData.Today);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.Conflict);
    }

    [Fact]
    public async Task GIVEN_an_unknown_reference_WHEN_quoting_a_cancellation_THEN_not_found_is_returned()
    {
        // Act
        var result = await QuoteAsync("missing", TestData.Today);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.NotFound);
        result.Error.ShouldBe("No policy found with reference 'MISSING'.");
    }

    // --- Claims ---

    [Fact]
    public async Task GIVEN_an_active_policy_WHEN_recording_a_claim_THEN_it_is_saved_and_the_quote_drops_to_zero()
    {
        // Arrange
        await SellAsync();

        // Act
        var result = await _service.RecordClaimAsync("pol-0001", TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.Success);
        result.Value.ShouldNotBeNull().HasClaims.ShouldBeTrue();
        _repository.SaveCount.ShouldBe(1);

        var quote = await QuoteAsync("POL-0001", TestData.Today);
        quote.Value.ShouldNotBeNull().HasClaims.ShouldBeTrue();
        quote.Value.TotalRefund.ShouldBe(0m);
        quote.Value.Refunds.ShouldBeEmpty();
    }

    [Fact]
    public async Task GIVEN_a_cancelled_policy_WHEN_recording_a_claim_THEN_conflict_is_returned()
    {
        // Arrange
        await SellAsync();
        await CancelAsync("POL-0001", TestData.Today);

        // Act
        var result = await _service.RecordClaimAsync("POL-0001", TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ServiceResultStatus.Conflict);
        result.Error.ShouldBe("A claim cannot be recorded on a cancelled policy.");
    }

    [Fact]
    public async Task GIVEN_an_unknown_reference_WHEN_recording_a_claim_THEN_not_found_is_returned()
    {
        // Act
        var result = await _service.RecordClaimAsync("missing", TestContext.Current.CancellationToken);

        // Assert
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

    private Task<ServiceResult<CancellationQuoteResponse>> QuoteAsync(string reference, DateOnly? cancellationDate) =>
        _service.QuoteCancellationAsync(
            reference,
            new CancelPolicyRequest { CancellationDate = cancellationDate },
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
