using Microsoft.Extensions.Time.Testing;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Tests;

internal static class TestData
{
    public static readonly DateOnly Today = new(2026, 6, 15);

    public static FakeTimeProvider Clock() => new(new DateTimeOffset(Today, new TimeOnly(9, 30), TimeSpan.Zero));

    public static SellPolicyRequest ValidRequest() => new()
    {
        Reference = "POL-0001",
        InsuranceType = InsuranceType.Household,
        StartDate = Today.AddDays(7),
        Amount = 250.00m,
        AutoRenew = true,
        PaymentType = PaymentType.Card,
        Property = new PropertyRequest
        {
            AddressLine1 = "1 High Street",
            AddressLine2 = "Little Town",
            Postcode = "AB1 2CD",
        },
        Policyholders =
        [
            new PolicyholderRequest { FirstName = "Jane", LastName = "Smith", DateOfBirth = new DateOnly(1985, 4, 12) },
        ],
    };

    public static PolicyholderRequest Holder(DateOnly dateOfBirth) =>
        new() { FirstName = "Alex", LastName = "Jones", DateOfBirth = dateOfBirth };
}
