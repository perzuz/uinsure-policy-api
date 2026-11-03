using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests.Domain;

public sealed class PolicyTests
{
    private static readonly DateOnly StartDate = new(2026, 7, 1);

    [Fact]
    public void Selling_a_policy_sets_a_one_year_term()
    {
        var policy = SellPolicy();

        policy.StartDate.ShouldBe(StartDate);
        policy.EndDate.ShouldBe(new DateOnly(2027, 7, 1));
    }

    [Fact]
    public void Selling_a_policy_records_the_supplied_details()
    {
        var policy = SellPolicy(autoRenew: true);

        policy.InsuranceType.ShouldBe(InsuranceType.BuyToLet);
        policy.Amount.ShouldBe(199.99m);
        policy.AutoRenew.ShouldBeTrue();
        policy.HasClaims.ShouldBeFalse();
        policy.Property.AddressLine1.ShouldBe("1 High Street");
        policy.Policyholders.ShouldHaveSingleItem().LastName.ShouldBe("Smith");
        policy.Refunds.ShouldBeEmpty();
    }

    [Fact]
    public void Selling_a_policy_attaches_the_payment()
    {
        var policy = SellPolicy();

        var payment = policy.Payments.ShouldHaveSingleItem();
        payment.PaymentReference.ShouldBe("PAY-1");
        payment.Type.ShouldBe(PaymentType.DirectDebit);
        payment.Amount.ShouldBe(199.99m);
    }

    [Theory]
    [InlineData("pol-1", "POL-1")]
    [InlineData("  POL-1  ", "POL-1")]
    public void Reference_is_normalised(string reference, string expected)
    {
        SellPolicy(reference).Reference.ShouldBe(expected);
    }

    [Fact]
    public void Property_values_are_normalised()
    {
        var property = new InsuredProperty("  1 High Street ", "   ", null, " ab1 2cd ");

        property.AddressLine1.ShouldBe("1 High Street");
        property.AddressLine2.ShouldBeNull();
        property.AddressLine3.ShouldBeNull();
        property.Postcode.ShouldBe("AB1 2CD");
    }

    [Fact]
    public void Policyholder_names_are_trimmed()
    {
        var holder = new Policyholder(" Jane ", " Smith ", new DateOnly(1990, 1, 1));

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
            new Payment("PAY-1", PaymentType.DirectDebit, 199.99m));
}
