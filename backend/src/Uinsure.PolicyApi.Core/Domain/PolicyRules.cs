namespace Uinsure.PolicyApi.Core.Domain;

/// <summary>
/// Business rules from the specification, kept in one place so the validator,
/// the domain model and the persistence mapping all agree.
/// </summary>
public static class PolicyRules
{
    public const int MaxDaysSoldInAdvance = 60;
    public const int TermInYears = 1;
    public const int MinPolicyholders = 1;
    public const int MaxPolicyholders = 3;
    public const int MinPolicyholderAge = 16;
    public const int RenewalWindowDays = 30;
    public const int CoolingOffDays = 14;

    public const int MaxReferenceLength = 20;
    public const int MaxNameLength = 100;
    public const int MaxAddressLineLength = 100;
    public const int MaxPostcodeLength = 8;

    public const string AutoRenewByChequeError = "Auto-renewing policies cannot be paid by cheque.";

    /// <summary>Cheques can't be collected automatically, so they can't pay for an auto renewal.</summary>
    public static bool IsAllowedPaymentType(bool autoRenew, PaymentType paymentType) =>
        !(autoRenew && paymentType == PaymentType.Cheque);

    public static DateOnly CalculateEndDate(DateOnly startDate) => startDate.AddYears(TermInYears);

    /// <summary>
    /// Age in whole years on <paramref name="onDate"/>. Someone born on 29 February
    /// turns a year older on 1 March in non-leap years (UK convention).
    /// </summary>
    public static int AgeOn(DateOnly dateOfBirth, DateOnly onDate)
    {
        var age = onDate.Year - dateOfBirth.Year;
        if (dateOfBirth > onDate.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
