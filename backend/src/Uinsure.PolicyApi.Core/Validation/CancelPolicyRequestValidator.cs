using FluentValidation;
using Uinsure.PolicyApi.Core.Contracts;

namespace Uinsure.PolicyApi.Core.Validation;

/// <summary>
/// Validates the request on its own. The check that the date is not after the policy end date
/// needs the policy, so it lives in <c>Policy.Cancel</c>.
/// </summary>
public sealed class CancelPolicyRequestValidator : AbstractValidator<CancelPolicyRequest>
{
    public CancelPolicyRequestValidator(TimeProvider timeProvider)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CancellationDate)
            .NotNull()
            .Must(date => date >= timeProvider.GetUtcToday())
            .WithMessage("'Cancellation Date' cannot be in the past.");
    }
}
