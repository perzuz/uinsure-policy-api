using System.Text.Json.Serialization;

namespace Uinsure.PolicyApi.Core.Domain;

[JsonConverter(typeof(JsonStringEnumConverter<InsuranceType>))]
public enum InsuranceType
{
    Household = 1,
    BuyToLet = 2,
}
