namespace Uinsure.PolicyApi.Core.Domain;

/// <summary>
/// One year of cover. A policy starts with term 1 when sold; each renewal adds the next term,
/// starting on the previous term's end date.
/// </summary>
public sealed class PolicyTerm
{
    public PolicyTerm(int termNumber, DateOnly startDate, decimal amount)
    {
        TermNumber = termNumber;
        StartDate = startDate;
        EndDate = PolicyRules.CalculateEndDate(startDate);
        Amount = amount;
    }

    public int TermNumber { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public decimal Amount { get; private set; }

    public int LengthInDays => EndDate.DayNumber - StartDate.DayNumber;
}
