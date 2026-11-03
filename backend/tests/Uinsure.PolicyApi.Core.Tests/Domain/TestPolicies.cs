using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests.Domain;

internal static class TestPolicies
{
    /// <summary>Term 1 runs 2026-07-01 to 2027-07-01 (365 days); the renewal window opens 2027-06-01.</summary>
    public static readonly DateOnly StartDate = new(2026, 7, 1);

    public static readonly DateOnly EndDate = new(2027, 7, 1);

    public static Policy Sold(
        decimal amount = 365.00m,
        bool autoRenew = true,
        PaymentType paymentType = PaymentType.DirectDebit) =>
        Policy.Sell(
            "POL-1",
            InsuranceType.Household,
            StartDate,
            amount,
            autoRenew,
            new InsuredProperty("1 High Street", null, null, "AB1 2CD"),
            [new Policyholder("Jane", "Smith", new DateOnly(1990, 1, 1))],
            "PAY-1",
            paymentType);

    public static Func<string> References(string prefix)
    {
        var next = 0;
        return () => $"{prefix}-{++next}";
    }
}
