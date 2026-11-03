using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Uinsure.PolicyApi.Core.Contracts;
using Uinsure.PolicyApi.Core.Services;
using Uinsure.PolicyApi.Core.Validation;

namespace Uinsure.PolicyApi.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPolicyCore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IReferenceGenerator, ReferenceGenerator>();
        services.AddScoped<IValidator<SellPolicyRequest>, SellPolicyRequestValidator>();
        services.AddScoped<IPolicyService, PolicyService>();

        return services;
    }
}
