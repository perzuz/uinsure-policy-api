using System.Globalization;
using FluentValidation.TestHelper;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Domain;
using Uinsure.PolicyApi.Core.Validation;

namespace Uinsure.PolicyApi.Core.Tests.Validation;

public sealed class CancelAndRenewRequestValidatorTests
{
    private readonly CancelPolicyRequestValidator _cancelValidator = new(TestData.Clock());
    private readonly RenewPolicyRequestValidator _renewValidator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(400)]
    public void Cancellation_date_today_or_later_is_valid(int daysAhead)
    {
        _cancelValidator.TestValidate(new CancelPolicyRequest { CancellationDate = TestData.Today.AddDays(daysAhead) })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Cancellation_date_in_the_past_is_invalid()
    {
        _cancelValidator.TestValidate(new CancelPolicyRequest { CancellationDate = TestData.Today.AddDays(-1) })
            .ShouldHaveValidationErrorFor(x => x.CancellationDate)
            .WithErrorMessage("'Cancellation Date' cannot be in the past.");
    }

    [Fact]
    public void Cancellation_date_is_required()
    {
        _cancelValidator.TestValidate(new CancelPolicyRequest())
            .ShouldHaveValidationErrorFor(x => x.CancellationDate)
            .Only();
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("340")]
    [InlineData("340.50")]
    public void Renewal_amount_greater_than_zero_with_up_to_two_decimal_places_is_valid(string amount)
    {
        _renewValidator.TestValidate(new RenewPolicyRequest { Amount = Parse(amount), PaymentType = PaymentType.Card })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("340.001")]
    public void Missing_zero_negative_or_sub_penny_renewal_amount_is_invalid(string? amount)
    {
        _renewValidator.TestValidate(new RenewPolicyRequest { Amount = amount is null ? null : Parse(amount) })
            .ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Renewal_payment_type_is_optional_at_request_level()
    {
        // Whether it is required depends on the policy's auto-renew setting, checked by Policy.Renew.
        _renewValidator.TestValidate(new RenewPolicyRequest { Amount = 340m })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Undefined_renewal_payment_type_is_invalid()
    {
        _renewValidator.TestValidate(new RenewPolicyRequest { Amount = 340m, PaymentType = (PaymentType)99 })
            .ShouldHaveValidationErrorFor(x => x.PaymentType);
    }

    private static decimal Parse(string amount) => decimal.Parse(amount, CultureInfo.InvariantCulture);
}
