using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorWebForms.Core.Services;

public static class BlazorWebFormsServiceCollectionExtensions
{
    public static IServiceCollection AddBlazorWebFormsCore(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton<IFormDefinitionSerializer, JsonFormDefinitionSerializer>();
        services.AddSingleton<IConditionEvaluator, SimpleConditionEvaluator>();
        services.AddSingleton<IPermissionEvaluator, DefaultPermissionEvaluator>();
        services.AddSingleton<ICoreMetadataCache, InMemoryCoreMetadataCache>();
        services.TryAddSingleton<IEmployeePrefillProvider, NoOpEmployeePrefillProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IFormPrefillProvider, ClaimFormPrefillProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IFormPrefillProvider, EmployeeFormPrefillProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IFormPrefillProvider, FixedValueFormPrefillProvider>());
        services.TryAddSingleton<IAntiAbuseGuard, NoOpAntiAbuseGuard>();
        services.TryAddSingleton<IOperationalTelemetry, NoOpOperationalTelemetry>();
        services.TryAddSingleton<IWebhookDispatcher, NoOpWebhookDispatcher>();
        services.TryAddSingleton<IFormulaEvaluator, SimpleFormulaEvaluator>();
        services.TryAddSingleton<ICaptchaValidator, NoOpCaptchaValidator>();
        services.TryAddSingleton<IFormAnalyticsStore, NoOpFormAnalyticsStore>();
        services.AddScoped<FormsApplicationService>();
        return services;
    }

    public static IServiceCollection AddCustomFieldHandler<T>(this IServiceCollection services)
        where T : class, ICustomFieldHandler
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICustomFieldHandler, T>());
        return services;
    }
}

internal sealed class NoOpEmployeePrefillProvider : IEmployeePrefillProvider
{
    public Task<IReadOnlyDictionary<string, string?>> GetEmployeeDataAsync(UserProfile requester, string employeeEmail, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string?>>(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));
}

internal sealed class NoOpAntiAbuseGuard : IAntiAbuseGuard
{
    public Task CheckUploadAllowedAsync(UserProfile user, FileUploadRequest request, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task CheckOutboundNotificationAllowedAsync(string channel, string recipient, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class NoOpOperationalTelemetry : IOperationalTelemetry
{
    public void TrackUpload(string source, long bytes, bool success)
    {
    }

    public void TrackPdfExport(string source, long bytes, bool success)
    {
    }

    public void TrackEmailDelivery(string channel, bool success)
    {
    }

    public void TrackFailure(string area, string operation, string reason)
    {
    }
}
