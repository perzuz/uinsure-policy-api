using System.Text.Json.Serialization;

namespace Uinsure.PolicyApi.Core.Domain;

// See InsuranceType for why both converters are declared.
[JsonConverter(typeof(JsonStringEnumConverter<PaymentType>))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum PaymentType
{
    Card = 1,
    DirectDebit = 2,
    Cheque = 3,
}
