using System.Text.Json.Serialization;

namespace Uinsure.PolicyApi.Core.Domain;

// System.Text.Json handles the API's actual serialisation. The Newtonsoft converter is only
// read by the OpenAPI (Swagger) extension, so the documented values match: names, not numbers.
[JsonConverter(typeof(JsonStringEnumConverter<InsuranceType>))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum InsuranceType
{
    Household = 1,
    BuyToLet = 2,
}
