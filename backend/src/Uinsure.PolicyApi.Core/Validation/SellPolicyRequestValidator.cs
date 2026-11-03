using FluentValidation;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Domain;

namespace Uinsure.PolicyApi.Core.Validation;

public sealed class SellPolicyRequestValidator : AbstractValidator<SellPolicyRequest>
{
    public SellPolicyRequestValidator(TimeProvider timeProvider)
    {
        // Stop at the first failure per property so e.g. a missing date doesn't also
        // report "date in the past".
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Reference)
            .NotEmpty()
            .MaximumLength(PolicyRules.MaxReferenceLength);

        RuleFor(x => x.InsuranceType)
            .NotNull()
            .IsInEnum();

        RuleFor(x => x.PaymentType)
            .NotNull()
            .IsInEnum();

        RuleFor(x => x.Amount).MustBeAValidPremium();

        RuleFor(x => x.StartDate)
            .NotNull()
            .Must(startDate => startDate >= timeProvider.GetUtcToday())
            .WithMessage("'Start Date' cannot be in the past.")
            .Must(startDate => startDate <= timeProvider.GetUtcToday().AddDays(PolicyRules.MaxDaysSoldInAdvance))
            .WithMessage($"A policy can only be sold up to {PolicyRules.MaxDaysSoldInAdvance} days in advance.");

        RuleFor(x => x.Property)
            .NotNull()
            .ChildRules(property =>
            {
                // Child validators don't inherit the parent's cascade mode.
                property.RuleLevelCascadeMode = CascadeMode.Stop;

                property.RuleFor(p => p!.AddressLine1)
                    .NotEmpty()
                    .MaximumLength(PolicyRules.MaxAddressLineLength);
                property.RuleFor(p => p!.AddressLine2)
                    .MaximumLength(PolicyRules.MaxAddressLineLength);
                property.RuleFor(p => p!.AddressLine3)
                    .MaximumLength(PolicyRules.MaxAddressLineLength);
                property.RuleFor(p => p!.Postcode)
                    .NotEmpty()
                    .Must(postcode => InsuredProperty.NormalisePostcode(postcode!).Length <= PolicyRules.MaxPostcodeLength)
                    .WithMessage($"'Postcode' must be {PolicyRules.MaxPostcodeLength} characters or fewer.");
            });

        RuleFor(x => x.Policyholders)
            .NotNull()
            .WithMessage($"A policy must have at least {PolicyRules.MinPolicyholders} policyholder.")
            .Must(holders => holders!.Count >= PolicyRules.MinPolicyholders)
            .WithMessage($"A policy must have at least {PolicyRules.MinPolicyholders} policyholder.")
            .Must(holders => holders!.Count <= PolicyRules.MaxPolicyholders)
            .WithMessage($"A policy can have no more than {PolicyRules.MaxPolicyholders} policyholders.");

        RuleForEach(x => x.Policyholders)
            .NotNull()
            .ChildRules(holder =>
            {
                holder.RuleLevelCascadeMode = CascadeMode.Stop;

                holder.RuleFor(h => h.FirstName)
                    .NotEmpty()
                    .MaximumLength(PolicyRules.MaxNameLength);
                holder.RuleFor(h => h.LastName)
                    .NotEmpty()
                    .MaximumLength(PolicyRules.MaxNameLength);
                holder.RuleFor(h => h.DateOfBirth)
                    .NotNull();
            })
            .Must((request, holder) => IsOldEnoughOnStartDate(holder, request.StartDate))
            .WithMessage($"All policyholders must be at least {PolicyRules.MinPolicyholderAge} years old on the policy start date.");
    }

    private static bool IsOldEnoughOnStartDate(PolicyholderRequest holder, DateOnly? startDate)
    {
        // Missing values are reported by their own rules.
        if (holder.DateOfBirth is not { } dateOfBirth || startDate is not { } start)
        {
            return true;
        }

        return PolicyRules.AgeOn(dateOfBirth, start) >= PolicyRules.MinPolicyholderAge;
    }
}
