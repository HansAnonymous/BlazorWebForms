using BlazorWebForms.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorWebForms.Infrastructure.SqlServer;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBlazorWebFormsSqlServer(
        this IServiceCollection services,
        Action<BlazorWebFormsSqlServerOptions>? configure = null)
    {
        var options = new BlazorWebFormsSqlServerOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.TryAddSingleton<IFormsRepository, InMemorySqlFormsRepository>();
        services.TryAddSingleton<IFileStorage, LocalFileStorage>();
        services.TryAddSingleton<IPdfExporter, TextPdfExporter>();
        services.TryAddSingleton<IEmailNotifier, MemoryEmailNotifier>();
        services.TryAddSingleton<ICurrentUserContext, DemoCurrentUserContext>();
        services.TryAddSingleton<SqlServerSchemaDescriptor>();
        return services;
    }
}
