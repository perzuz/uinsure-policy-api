using FluentValidation;
using Uinsure.PolicyApi.Core.Contracts;

namespace Uinsure.PolicyApi.Core.Validation;

/// <summary>
/// Validates the request on its own. Whether a payment type is required depends on the policy's
/// auto-renew setting, so that check lives in <c>Policy.Renew</c>.
/// </summary>
public sealed class RenewPolicyRequestValidator : AbstractValidator<RenewPolicyRequest>
{
    public RenewPolicyRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Amount).MustBeAValidPremium();

        RuleFor(x => x.PaymentType).IsInEnum();
    }
}
