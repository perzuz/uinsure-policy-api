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
    public void Valid_request_has_no_errors()
    {
        _validator.TestValidate(TestData.ValidRequest()).ShouldNotHaveAnyValidationErrors();
    }

    // --- Reference ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Reference_is_required(string? reference)
    {
        var request = TestData.ValidRequest() with { Reference = reference };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Reference);
    }

    [Fact]
    public void Reference_at_max_length_is_valid()
    {
        var request = TestData.ValidRequest() with { Reference = new string('A', PolicyRules.MaxReferenceLength) };

        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.Reference);
    }

    [Fact]
    public void Reference_over_max_length_is_invalid()
    {
        var request = TestData.ValidRequest() with { Reference = new string('A', PolicyRules.MaxReferenceLength + 1) };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Reference);
    }

    // --- Start date: can be sold up to 60 days in advance ---

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(60)]
    public void Start_date_from_today_up_to_60_days_ahead_is_valid(int daysAhead)
    {
        var request = TestData.ValidRequest() with { StartDate = TestData.Today.AddDays(daysAhead) };

        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.StartDate);
    }

    [Theory]
    [InlineData(61)]
    [InlineData(365)]
    public void Start_date_more_than_60_days_ahead_is_invalid(int daysAhead)
    {
        var request = TestData.ValidRequest() with { StartDate = TestData.Today.AddDays(daysAhead) };

        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.StartDate)
            .WithErrorMessage("A policy can only be sold up to 60 days in advance.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-30)]
    public void Start_date_in_the_past_is_invalid(int daysAhead)
    {
        var request = TestData.ValidRequest() with { StartDate = TestData.Today.AddDays(daysAhead) };

        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.StartDate)
            .WithErrorMessage("'Start Date' cannot be in the past.");
    }

    [Fact]
    public void Start_date_is_required()
    {
        var request = TestData.ValidRequest() with { StartDate = null };

        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.StartDate)
            .Only();
    }

    [Fact]
    public void Today_is_evaluated_at_validation_time_not_construction_time()
    {
        var clock = TestData.Clock();
        var validator = new SellPolicyRequestValidator(clock);
        var request = TestData.ValidRequest() with { StartDate = TestData.Today.AddDays(1) };

        validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.StartDate);

        clock.Advance(TimeSpan.FromDays(2));

        validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.StartDate);
    }

    // --- Amount ---

    [Theory]
    [InlineData("0.01")]
    [InlineData("100.5")]
    [InlineData("100.50")]
    [InlineData("1234.99")]
    public void Positive_amount_with_up_to_two_decimal_places_is_valid(string amount)
    {
        var request = TestData.ValidRequest() with { Amount = decimal.Parse(amount, CultureInfo.InvariantCulture) };

        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.Amount);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-10.00")]
    [InlineData("10.001")]
    public void Zero_negative_or_sub_penny_amount_is_invalid(string amount)
    {
        var request = TestData.ValidRequest() with { Amount = decimal.Parse(amount, CultureInfo.InvariantCulture) };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Amount_is_required()
    {
        var request = TestData.ValidRequest() with { Amount = null };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Amount);
    }

    // --- Enums ---

    [Fact]
    public void Insurance_type_is_required()
    {
        var request = TestData.ValidRequest() with { InsuranceType = null };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.InsuranceType);
    }

    [Fact]
    public void Undefined_insurance_type_is_invalid()
    {
        var request = TestData.ValidRequest() with { InsuranceType = (InsuranceType)99 };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.InsuranceType);
    }

    [Theory]
    [InlineData(InsuranceType.Household)]
    [InlineData(InsuranceType.BuyToLet)]
    public void Each_insurance_type_is_valid(InsuranceType insuranceType)
    {
        var request = TestData.ValidRequest() with { InsuranceType = insuranceType };

        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Payment_type_is_required()
    {
        var request = TestData.ValidRequest() with { PaymentType = null };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.PaymentType);
    }

    [Fact]
    public void Undefined_payment_type_is_invalid()
    {
        var request = TestData.ValidRequest() with { PaymentType = 0 };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.PaymentType);
    }

    [Theory]
    [InlineData(PaymentType.Card)]
    [InlineData(PaymentType.DirectDebit)]
    [InlineData(PaymentType.Cheque)]
    public void Each_payment_type_is_valid_for_a_policy_that_does_not_auto_renew(PaymentType paymentType)
    {
        var request = TestData.ValidRequest() with { PaymentType = paymentType, AutoRenew = false };

        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(PaymentType.Card)]
    [InlineData(PaymentType.DirectDebit)]
    public void Card_and_direct_debit_are_valid_for_an_auto_renewing_policy(PaymentType paymentType)
    {
        var request = TestData.ValidRequest() with { PaymentType = paymentType, AutoRenew = true };

        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void An_auto_renewing_policy_cannot_be_paid_by_cheque()
    {
        var request = TestData.ValidRequest() with { PaymentType = PaymentType.Cheque, AutoRenew = true };

        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.PaymentType)
            .WithErrorMessage(PolicyRules.AutoRenewByChequeError)
            .Only();
    }

    // --- Property ---

    [Fact]
    public void Property_is_required()
    {
        var request = TestData.ValidRequest() with { Property = null };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Property);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Address_line_1_is_required(string? addressLine1)
    {
        var request = WithProperty(p => p with { AddressLine1 = addressLine1 });

        _validator.TestValidate(request).ShouldHaveValidationErrorFor("Property.AddressLine1");
    }

    [Fact]
    public void Address_lines_2_and_3_are_optional()
    {
        var request = WithProperty(p => p with { AddressLine2 = null, AddressLine3 = null });

        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("SW1A 1AA")]
    [InlineData("M1 1AE")]
    [InlineData("  sw1a 1aa  ")]
    public void Postcode_of_up_to_8_characters_is_valid(string postcode)
    {
        var request = WithProperty(p => p with { Postcode = postcode });

        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor("Property.Postcode");
    }

    [Fact]
    public void Postcode_over_8_characters_is_invalid()
    {
        var request = WithProperty(p => p with { Postcode = "SW1A 1AAX" });

        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor("Property.Postcode")
            .WithErrorMessage("'Postcode' must be 8 characters or fewer.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Postcode_is_required(string? postcode)
    {
        var request = WithProperty(p => p with { Postcode = postcode });

        _validator.TestValidate(request).ShouldHaveValidationErrorFor("Property.Postcode");
    }

    // --- Policyholders: between 1 and 3 ---

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void One_to_three_policyholders_is_valid(int count)
    {
        var request = TestData.ValidRequest() with { Policyholders = Holders(count) };

        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void No_policyholders_is_invalid()
    {
        var request = TestData.ValidRequest() with { Policyholders = [] };

        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.Policyholders)
            .WithErrorMessage("A policy must have at least 1 policyholder.");
    }

    [Fact]
    public void Missing_policyholders_is_invalid()
    {
        var request = TestData.ValidRequest() with { Policyholders = null };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Policyholders);
    }

    [Fact]
    public void Four_policyholders_is_invalid()
    {
        var request = TestData.ValidRequest() with { Policyholders = Holders(4) };

        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(x => x.Policyholders)
            .WithErrorMessage("A policy can have no more than 3 policyholders.");
    }

    [Fact]
    public void Policyholder_names_and_date_of_birth_are_required()
    {
        var request = TestData.ValidRequest() with
        {
            Policyholders = [new PolicyholderRequest { FirstName = "", LastName = null, DateOfBirth = null }],
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor("Policyholders[0].FirstName");
        result.ShouldHaveValidationErrorFor("Policyholders[0].LastName");
        result.ShouldHaveValidationErrorFor("Policyholders[0].DateOfBirth");
    }

    [Fact]
    public void Null_policyholder_entry_is_invalid()
    {
        var request = TestData.ValidRequest() with { Policyholders = [null!] };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor("Policyholders[0]");
    }

    // --- Policyholders: over 16 on the start date ---

    [Fact]
    public void Policyholder_turning_16_on_the_start_date_is_valid()
    {
        var startDate = TestData.Today.AddDays(10);
        var request = TestData.ValidRequest() with
        {
            StartDate = startDate,
            Policyholders = [TestData.Holder(startDate.AddYears(-16))],
        };

        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Policyholder_turning_16_the_day_after_the_start_date_is_invalid()
    {
        var startDate = TestData.Today.AddDays(10);
        var request = TestData.ValidRequest() with
        {
            StartDate = startDate,
            Policyholders = [TestData.Holder(startDate.AddYears(-16).AddDays(1))],
        };

        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor("Policyholders[0]")
            .WithErrorMessage("All policyholders must be at least 16 years old on the policy start date.");
    }

    [Fact]
    public void Age_is_measured_on_the_start_date_not_today()
    {
        // Under 16 today, but 16 by the time the policy starts.
        var startDate = TestData.Today.AddDays(30);
        var request = TestData.ValidRequest() with
        {
            StartDate = startDate,
            Policyholders = [TestData.Holder(TestData.Today.AddYears(-16).AddDays(15))],
        };

        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Any_underage_policyholder_makes_the_request_invalid()
    {
        var request = TestData.ValidRequest() with
        {
            Policyholders =
            [
                TestData.Holder(new DateOnly(1980, 1, 1)),
                TestData.Holder(TestData.Today.AddYears(-10)),
            ],
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor("Policyholders[1]");
        result.ShouldNotHaveValidationErrorFor("Policyholders[0]");
    }

    [Fact]
    public void Age_rule_is_not_reported_when_start_date_is_missing()
    {
        var request = TestData.ValidRequest() with
        {
            StartDate = null,
            Policyholders = [TestData.Holder(TestData.Today)],
        };

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
