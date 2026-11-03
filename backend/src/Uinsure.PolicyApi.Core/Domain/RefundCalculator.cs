namespace Uinsure.PolicyApi.Core.Domain;

/// <summary>
/// Refund rules for cancelling a term:
/// <list type="bullet">
/// <item>before the term starts: full refund;</item>
/// <item>within the cooling-off period (the first 14 days, including the start date): full refund;</item>
/// <item>after that: pro rata for the days of cover not used;</item>
/// <item>on or after the end date: nothing.</item>
/// </list>
/// </summary>
public static class RefundCalculator
{
    public static decimal Calculate(PolicyTerm term, decimal amountPaid, DateOnly cancellationDate)
    {
        if (cancellationDate < term.StartDate)
        {
            return amountPaid;
        }

        if (cancellationDate >= term.EndDate)
        {
            return 0m;
        }

        if (cancellationDate < term.StartDate.AddDays(PolicyRules.CoolingOffDays))
        {
            return amountPaid;
        }

        // Cover ends on the cancellation date, so that day counts as unused.
        var unusedDays = term.EndDate.DayNumber - cancellationDate.DayNumber;

        return Math.Round(amountPaid * unusedDays / term.LengthInDays, 2, MidpointRounding.AwayFromZero);
    }
}
