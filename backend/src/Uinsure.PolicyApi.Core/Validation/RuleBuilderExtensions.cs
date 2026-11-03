using FluentValidation;

namespace Uinsure.PolicyApi.Core.Validation;

internal static class RuleBuilderExtensions
{
    /// <summary>A required premium: greater than 0 with no more than 2 decimal places.</summary>
    public static IRuleBuilderOptions<T, decimal?> MustBeAValidPremium<T>(this IRuleBuilder<T, decimal?> rule) =>
        rule
            .NotNull()
            .GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .WithMessage("'{PropertyName}' must have no more than 2 decimal places.");
}
