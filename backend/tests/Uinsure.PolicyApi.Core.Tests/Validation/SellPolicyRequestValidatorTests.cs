using System.Globalization;
using FluentValidation.TestHelper;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Domain;
using Uinsure.PolicyApi.Core.Validation;

namespace Uinsure.PolicyApi.Core.Tests.Validation;

public sealed class SellPolicyRequestValidatorTests
{
    private readonly SellPolicyRequestValidator _validator = new(TestData.Clock());

    [Fact]
    public void GIVEN_a_valid_request_WHEN_validating_THEN_there_are_no_errors()
    {
        // Act & Assert
        _validator.TestValidate(TestData.ValidRequest()).ShouldNotHaveAnyValidationErrors();
    }

    // --- Reference ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GIVEN_no_reference_WHEN_validating_THEN_it_is_invalid(string? reference)
    {
        // Arrange
        var request = TestData.ValidRequest() with { Reference = reference };

        // Act & Assert
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Reference);
    }

    [Fact]
    public void GIVEN_a_reference_at_max_length_WHEN_validating_THEN_it_is_valid()
    {
        // Arrange
        var request = TestData.ValidRequest() with { Reference = new string('A', PolicyRules.MaxReferenceLength) };

        // Act & Assert
        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.Reference);
    }

    [Fact]
    public void GIVEN_a_reference_over_max_length_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var request = TestData.ValidRequest() with { Reference = new string('A', PolicyRules.MaxReferenceLength + 1) };

        // Act & Assert
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Reference);
    }

    // --- Start date: can be sold up to 60 days in advance ---

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(60)]
    public void GIVEN_a_start_date_from_today_up_to_60_days_ahead_WHEN_validating_THEN_it_is_valid(int daysAhead)
    {
        // Arrange
        var request = TestData.ValidRequest() with { StartDate = TestData.Today.AddDays(daysAhead) };

        // Act & Assert
        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.StartDate);
    }

    [Theory]
    [InlineData(61)]
    [InlineData(365)]
    public void GIVEN_a_start_date_more_than_60_days_ahead_WHEN_validating_THEN_it_is_invalid(int daysAhead)
    {
        // Arrange
        var request = TestData.ValidRequest() with { StartDate = TestData.Today.AddDays(daysAhead) };

        // Act & Assert
        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.StartDate)
            .WithErrorMessage("A policy can only be sold up to 60 days in advance.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-30)]
    public void GIVEN_a_start_date_in_the_past_WHEN_validating_THEN_it_is_invalid(int daysAhead)
    {
        // Arrange
        var request = TestData.ValidRequest() with { StartDate = TestData.Today.AddDays(daysAhead) };

        // Act & Assert
        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.StartDate)
            .WithErrorMessage("'Start Date' cannot be in the past.");
    }

    [Fact]
    public void GIVEN_no_start_date_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var request = TestData.ValidRequest() with { StartDate = null };

        // Act & Assert
        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.StartDate)
            .Only();
    }

    [Fact]
    public void GIVEN_the_clock_moves_after_the_validator_is_created_WHEN_validating_THEN_today_is_the_current_date()
    {
        // Arrange
        var clock = TestData.Clock();
        var validator = new SellPolicyRequestValidator(clock);
        var request = TestData.ValidRequest() with { StartDate = TestData.Today.AddDays(1) };

        validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.StartDate);

        // Act
        clock.Advance(TimeSpan.FromDays(2));

        // Assert
        validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.StartDate);
    }

    // --- Amount ---

    [Theory]
    [InlineData("0.01")]
    [InlineData("100.5")]
    [InlineData("100.50")]
    [InlineData("1234.99")]
    public void GIVEN_a_positive_amount_with_up_to_two_decimal_places_WHEN_validating_THEN_it_is_valid(string amount)
    {
        // Arrange
        var request = TestData.ValidRequest() with { Amount = decimal.Parse(amount, CultureInfo.InvariantCulture) };

        // Act & Assert
        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.Amount);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-10.00")]
    [InlineData("10.001")]
    public void GIVEN_a_zero_negative_or_sub_penny_amount_WHEN_validating_THEN_it_is_invalid(string amount)
    {
        // Arrange
        var request = TestData.ValidRequest() with { Amount = decimal.Parse(amount, CultureInfo.InvariantCulture) };

        // Act & Assert
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void GIVEN_no_amount_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var request = TestData.ValidRequest() with { Amount = null };

        // Act & Assert
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Amount);
    }

    // --- Enums ---

    [Fact]
    public void GIVEN_no_insurance_type_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var request = TestData.ValidRequest() with { InsuranceType = null };

        // Act & Assert
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.InsuranceType);
    }

    [Fact]
    public void GIVEN_an_undefined_insurance_type_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var request = TestData.ValidRequest() with { InsuranceType = (InsuranceType)99 };

        // Act & Assert
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.InsuranceType);
    }

    [Theory]
    [InlineData(InsuranceType.Household)]
    [InlineData(InsuranceType.BuyToLet)]
    public void GIVEN_each_insurance_type_WHEN_validating_THEN_it_is_valid(InsuranceType insuranceType)
    {
        // Arrange
        var request = TestData.ValidRequest() with { InsuranceType = insuranceType };

        // Act & Assert
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void GIVEN_no_payment_type_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var request = TestData.ValidRequest() with { PaymentType = null };

        // Act & Assert
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.PaymentType);
    }

    [Fact]
    public void GIVEN_an_undefined_payment_type_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var request = TestData.ValidRequest() with { PaymentType = 0 };

        // Act & Assert
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.PaymentType);
    }

    [Theory]
    [InlineData(PaymentType.Card)]
    [InlineData(PaymentType.DirectDebit)]
    [InlineData(PaymentType.Cheque)]
    public void GIVEN_a_policy_that_does_not_auto_renew_WHEN_validating_each_payment_type_THEN_it_is_valid(PaymentType paymentType)
    {
        // Arrange
        var request = TestData.ValidRequest() with { PaymentType = paymentType, AutoRenew = false };

        // Act & Assert
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(PaymentType.Card)]
    [InlineData(PaymentType.DirectDebit)]
    public void GIVEN_an_auto_renewing_policy_WHEN_validating_card_or_direct_debit_THEN_it_is_valid(PaymentType paymentType)
    {
        // Arrange
        var request = TestData.ValidRequest() with { PaymentType = paymentType, AutoRenew = true };

        // Act & Assert
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void GIVEN_an_auto_renewing_policy_WHEN_validating_a_cheque_payment_THEN_it_is_invalid()
    {
        // Arrange
        var request = TestData.ValidRequest() with { PaymentType = PaymentType.Cheque, AutoRenew = true };

        // Act & Assert
        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.PaymentType)
            .WithErrorMessage(PolicyRules.AutoRenewByChequeError)
            .Only();
    }

    // --- Property ---

    [Fact]
    public void GIVEN_no_property_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var request = TestData.ValidRequest() with { Property = null };

        // Act & Assert
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Property);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void GIVEN_no_address_line_1_WHEN_validating_THEN_it_is_invalid(string? addressLine1)
    {
        // Arrange
        var request = WithProperty(p => p with { AddressLine1 = addressLine1 });

        // Act & Assert
        _validator.TestValidate(request).ShouldHaveValidationErrorFor("Property.AddressLine1");
    }

    [Fact]
    public void GIVEN_no_address_lines_2_and_3_WHEN_validating_THEN_it_is_valid()
    {
        // Arrange
        var request = WithProperty(p => p with { AddressLine2 = null, AddressLine3 = null });

        // Act & Assert
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("SW1A 1AA")]
    [InlineData("M1 1AE")]
    [InlineData("  sw1a 1aa  ")]
    public void GIVEN_a_postcode_of_up_to_8_characters_WHEN_validating_THEN_it_is_valid(string postcode)
    {
        // Arrange
        var request = WithProperty(p => p with { Postcode = postcode });

        // Act & Assert
        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor("Property.Postcode");
    }

    [Fact]
    public void GIVEN_a_postcode_over_8_characters_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var request = WithProperty(p => p with { Postcode = "SW1A 1AAX" });

        // Act & Assert
        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor("Property.Postcode")
            .WithErrorMessage("'Postcode' must be 8 characters or fewer.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GIVEN_no_postcode_WHEN_validating_THEN_it_is_invalid(string? postcode)
    {
        // Arrange
        var request = WithProperty(p => p with { Postcode = postcode });

        // Act & Assert
        _validator.TestValidate(request).ShouldHaveValidationErrorFor("Property.Postcode");
    }

    // --- Policyholders: between 1 and 3 ---

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GIVEN_one_to_three_policyholders_WHEN_validating_THEN_it_is_valid(int count)
    {
        // Arrange
        var request = TestData.ValidRequest() with { Policyholders = Holders(count) };

        // Act & Assert
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void GIVEN_an_empty_policyholder_list_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var request = TestData.ValidRequest() with { Policyholders = [] };

        // Act & Assert
        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.Policyholders)
            .WithErrorMessage("A policy must have at least 1 policyholder.");
    }

    [Fact]
    public void GIVEN_no_policyholder_list_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var request = TestData.ValidRequest() with { Policyholders = null };

        // Act & Assert
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Policyholders);
    }

    [Fact]
    public void GIVEN_four_policyholders_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var request = TestData.ValidRequest() with { Policyholders = Holders(4) };

        // Act & Assert
        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.Policyholders)
            .WithErrorMessage("A policy can have no more than 3 policyholders.");
    }

    [Fact]
    public void GIVEN_a_policyholder_missing_names_or_date_of_birth_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var request = TestData.ValidRequest() with
        {
            Policyholders = [new PolicyholderRequest { FirstName = "", LastName = null, DateOfBirth = null }],
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor("Policyholders[0].FirstName");
        result.ShouldHaveValidationErrorFor("Policyholders[0].LastName");
        result.ShouldHaveValidationErrorFor("Policyholders[0].DateOfBirth");
    }

    [Fact]
    public void GIVEN_a_null_policyholder_entry_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var request = TestData.ValidRequest() with { Policyholders = [null!] };

        // Act & Assert
        _validator.TestValidate(request).ShouldHaveValidationErrorFor("Policyholders[0]");
    }

    // --- Policyholders: over 16 on the start date ---

    [Fact]
    public void GIVEN_a_policyholder_turning_16_on_the_start_date_WHEN_validating_THEN_it_is_valid()
    {
        // Arrange
        var startDate = TestData.Today.AddDays(10);
        var request = TestData.ValidRequest() with
        {
            StartDate = startDate,
            Policyholders = [TestData.Holder(startDate.AddYears(-16))],
        };

        // Act & Assert
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void GIVEN_a_policyholder_turning_16_the_day_after_the_start_date_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var startDate = TestData.Today.AddDays(10);
        var request = TestData.ValidRequest() with
        {
            StartDate = startDate,
            Policyholders = [TestData.Holder(startDate.AddYears(-16).AddDays(1))],
        };

        // Act & Assert
        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor("Policyholders[0]")
            .WithErrorMessage("All policyholders must be at least 16 years old on the policy start date.");
    }

    [Fact]
    public void GIVEN_a_policyholder_under_16_today_but_16_on_the_start_date_WHEN_validating_THEN_it_is_valid()
    {
        // Arrange
        // Under 16 today, but 16 by the time the policy starts.
        var startDate = TestData.Today.AddDays(30);
        var request = TestData.ValidRequest() with
        {
            StartDate = startDate,
            Policyholders = [TestData.Holder(TestData.Today.AddYears(-16).AddDays(15))],
        };

        // Act & Assert
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void GIVEN_any_underage_policyholder_WHEN_validating_THEN_it_is_invalid()
    {
        // Arrange
        var request = TestData.ValidRequest() with
        {
            Policyholders =
            [
                TestData.Holder(new DateOnly(1980, 1, 1)),
                TestData.Holder(TestData.Today.AddYears(-10)),
            ],
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor("Policyholders[1]");
        result.ShouldNotHaveValidationErrorFor("Policyholders[0]");
    }

    [Fact]
    public void GIVEN_no_start_date_WHEN_validating_THEN_the_age_rule_is_not_reported()
    {
        // Arrange
        var request = TestData.ValidRequest() with
        {
            StartDate = null,
            Policyholders = [TestData.Holder(TestData.Today)],
        };

        // Act & Assert
        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor("Policyholders[0]");
    }

    private static SellPolicyRequest WithProperty(Func<PropertyRequest, PropertyRequest> change)
    {
        var request = TestData.ValidRequest();
        return request with { Property = change(request.Property!) };
    }

    private static List<PolicyholderRequest> Holders(int count) =>
        [.. Enumerable.Range(0, count).Select(i => TestData.Holder(new DateOnly(1980 + i, 1, 1)))];
}
