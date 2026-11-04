using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests.Domain;

public sealed class PolicyTests
{
    private static readonly DateOnly StartDate = new(2026, 7, 1);

    [Fact]
    public void GIVEN_a_start_date_WHEN_selling_a_policy_THEN_the_term_is_one_year()
    {
        // Act
        var policy = SellPolicy();

        // Assert
        policy.StartDate.ShouldBe(StartDate);
        policy.EndDate.ShouldBe(new DateOnly(2027, 7, 1));
    }

    [Fact]
    public void GIVEN_valid_details_WHEN_selling_a_policy_THEN_the_details_are_recorded()
    {
        // Act
        var policy = SellPolicy(autoRenew: true);

        // Assert
        policy.InsuranceType.ShouldBe(InsuranceType.BuyToLet);
        policy.Amount.ShouldBe(199.99m);
        policy.AutoRenew.ShouldBeTrue();
        policy.HasClaims.ShouldBeFalse();
        policy.Property.AddressLine1.ShouldBe("1 High Street");
        policy.Policyholders.ShouldHaveSingleItem().LastName.ShouldBe("Smith");
        policy.Refunds.ShouldBeEmpty();
    }

    [Fact]
    public void GIVEN_a_payment_WHEN_selling_a_policy_THEN_it_is_attached()
    {
        // Act
        var policy = SellPolicy();

        // Assert
        var payment = policy.Payments.ShouldHaveSingleItem();
        payment.PaymentReference.ShouldBe("PAY-1");
        payment.Type.ShouldBe(PaymentType.DirectDebit);
        payment.Amount.ShouldBe(199.99m);
        payment.TermNumber.ShouldBe(1);
    }

    [Fact]
    public void GIVEN_valid_details_WHEN_selling_a_policy_THEN_it_is_active_with_a_first_term()
    {
        // Act
        var policy = SellPolicy();

        // Assert
        var term = policy.Terms.ShouldHaveSingleItem();
        term.TermNumber.ShouldBe(1);
        term.StartDate.ShouldBe(StartDate);
        term.EndDate.ShouldBe(new DateOnly(2027, 7, 1));
        term.Amount.ShouldBe(199.99m);
        policy.LatestTerm.ShouldBe(term);
        policy.Status.ShouldBe(PolicyStatus.Active);
        policy.CancellationDate.ShouldBeNull();
    }

    [Theory]
    [InlineData("pol-1", "POL-1")]
    [InlineData("  POL-1  ", "POL-1")]
    public void GIVEN_a_padded_or_lower_case_reference_WHEN_selling_a_policy_THEN_it_is_trimmed_and_upper_cased(string reference, string expected)
    {
        // Act & Assert
        SellPolicy(reference).Reference.ShouldBe(expected);
    }

    [Fact]
    public void GIVEN_padded_address_values_WHEN_creating_a_property_THEN_they_are_normalised()
    {
        // Act
        var property = new InsuredProperty("  1 High Street ", "   ", null, " ab1 2cd ");

        // Assert
        property.AddressLine1.ShouldBe("1 High Street");
        property.AddressLine2.ShouldBeNull();
        property.AddressLine3.ShouldBeNull();
        property.Postcode.ShouldBe("AB1 2CD");
    }

    [Fact]
    public void GIVEN_padded_names_WHEN_creating_a_policyholder_THEN_they_are_trimmed()
    {
        // Act
        var holder = new Policyholder(" Jane ", " Smith ", new DateOnly(1990, 1, 1));

        // Assert
        holder.FirstName.ShouldBe("Jane");
        holder.LastName.ShouldBe("Smith");
    }

    private static Policy SellPolicy(string reference = "POL-1", bool autoRenew = false) =>
        Policy.Sell(
            reference,
            InsuranceType.BuyToLet,
            StartDate,
            199.99m,
            autoRenew,
            new InsuredProperty("1 High Street", null, null, "AB1 2CD"),
            [new Policyholder("Jane", "Smith", new DateOnly(1990, 1, 1))],
            "PAY-1",
            PaymentType.DirectDebit);
}
