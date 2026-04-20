using BlazorWebForms.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorWebForms.Core.Services;

public static class BlazorWebFormsServiceCollectionExtensions
{
    public static IServiceCollection AddBlazorWebFormsCore(this IServiceCollection services)
    {
        services.AddSingleton<IFormDefinitionSerializer, JsonFormDefinitionSerializer>();
        services.AddSingleton<IConditionEvaluator, SimpleConditionEvaluator>();
        services.AddSingleton<IFieldComponentRegistry, DefaultFieldComponentRegistry>();
        services.AddSingleton<IPermissionEvaluator, DefaultPermissionEvaluator>();
        services.AddScoped<FormsApplicationService>();
        return services;
    }
}
