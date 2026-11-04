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
    public void GIVEN_a_cancellation_date_today_or_later_WHEN_validating_THEN_it_is_valid(int daysAhead)
    {
        // Act & Assert
        _cancelValidator.TestValidate(new CancelPolicyRequest { CancellationDate = TestData.Today.AddDays(daysAhead) })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void GIVEN_a_cancellation_date_in_the_past_WHEN_validating_THEN_it_is_invalid()
    {
        // Act & Assert
        _cancelValidator.TestValidate(new CancelPolicyRequest { CancellationDate = TestData.Today.AddDays(-1) })
            .ShouldHaveValidationErrorFor(x => x.CancellationDate)
            .WithErrorMessage("'Cancellation Date' cannot be in the past.");
    }

    [Fact]
    public void GIVEN_no_cancellation_date_WHEN_validating_THEN_it_is_invalid()
    {
        // Act & Assert
        _cancelValidator.TestValidate(new CancelPolicyRequest())
            .ShouldHaveValidationErrorFor(x => x.CancellationDate)
            .Only();
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("340")]
    [InlineData("340.50")]
    public void GIVEN_a_renewal_amount_above_zero_with_up_to_two_decimal_places_WHEN_validating_THEN_it_is_valid(string amount)
    {
        // Act & Assert
        _renewValidator.TestValidate(new RenewPolicyRequest { Amount = Parse(amount), PaymentType = PaymentType.Card })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("340.001")]
    public void GIVEN_a_missing_zero_negative_or_sub_penny_renewal_amount_WHEN_validating_THEN_it_is_invalid(string? amount)
    {
        // Act & Assert
        _renewValidator.TestValidate(new RenewPolicyRequest { Amount = amount is null ? null : Parse(amount) })
            .ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void GIVEN_no_renewal_payment_type_WHEN_validating_the_request_THEN_it_is_valid()
    {
        // Act & Assert
        // Whether it is required depends on the policy's auto-renew setting, checked by Policy.Renew.
        _renewValidator.TestValidate(new RenewPolicyRequest { Amount = 340m })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void GIVEN_an_undefined_renewal_payment_type_WHEN_validating_THEN_it_is_invalid()
    {
        // Act & Assert
        _renewValidator.TestValidate(new RenewPolicyRequest { Amount = 340m, PaymentType = (PaymentType)99 })
            .ShouldHaveValidationErrorFor(x => x.PaymentType);
    }

    private static decimal Parse(string amount) => decimal.Parse(amount, CultureInfo.InvariantCulture);
}
