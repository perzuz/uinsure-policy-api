using System.Text.Json.Serialization;

namespace Uinsure.PolicyApi.Core.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<PaymentType>))]
public enum PaymentType
{
    Card = 1,
    DirectDebit = 2,
    Cheque = 3,
}
