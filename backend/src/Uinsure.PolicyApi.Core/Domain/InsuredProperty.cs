namespace Uinsure.PolicyApi.Core.Domain;

/// <summary>The property covered by the insurance.</summary>
public sealed class InsuredProperty
{
    public InsuredProperty(string addressLine1, string? addressLine2, string? addressLine3, string postcode)
    {
        AddressLine1 = addressLine1.Trim();
        AddressLine2 = NullIfBlank(addressLine2);
        AddressLine3 = NullIfBlank(addressLine3);
        Postcode = NormalisePostcode(postcode);
    }

    public string AddressLine1 { get; private set; }
    public string? AddressLine2 { get; private set; }
    public string? AddressLine3 { get; private set; }
    public string Postcode { get; private set; }

    public static string NormalisePostcode(string postcode) => postcode.Trim().ToUpperInvariant();

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
