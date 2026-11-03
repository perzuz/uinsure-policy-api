using System.Text.Json.Serialization;

namespace Uinsure.PolicyApi.Core.Domain;

// See InsuranceType for why both converters are declared.
[JsonConverter(typeof(JsonStringEnumConverter<PolicyStatus>))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum PolicyStatus
{
    Active = 1,
    Cancelled = 2,
}
