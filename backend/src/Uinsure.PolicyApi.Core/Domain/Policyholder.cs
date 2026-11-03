namespace Uinsure.PolicyApi.Core.Domain;

public sealed class Policyholder
{
    public Policyholder(string firstName, string lastName, DateOnly dateOfBirth)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        DateOfBirth = dateOfBirth;
    }

    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public DateOnly DateOfBirth { get; private set; }
}
