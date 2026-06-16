using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Infrastructure.SqlServer.Integrations;
using Microsoft.EntityFrameworkCore;
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
        services.AddHttpClient("BlazorWebForms.Email.SendGrid");
        services.AddHttpClient("BlazorWebForms.Email.Graph");
        // register EF Core DbContext and EF-backed repository (scoped)
        services.AddDbContext<BlazorWebFormsDbContext>(builder => builder.UseSqlServer(options.ConnectionString));
        services.AddScoped<IFormsRepository, EfFormsRepository>();

        services.TryAddSingleton<IFileStorage, LocalFileStorage>();
        services.TryAddSingleton<DryRunEmailIntegration>();
        services.TryAddSingleton<SmtpEmailIntegration>();
        services.TryAddSingleton<SendGridEmailIntegration>();
        services.TryAddSingleton<GraphEmailIntegration>();
        services.TryAddSingleton<IEmailIntegration, EmailIntegrationRouter>();
        services.TryAddSingleton<IGraphIntegration, GraphIntegration>();
        services.TryAddSingleton<IPdfIntegration, PdfIntegration>();
        services.TryAddSingleton<IAntiAbuseGuard, DefaultAntiAbuseGuard>();
        services.AddSingleton<IOperationalTelemetry, InMemoryOperationalTelemetry>();
        services.TryAddSingleton<IPdfExporter, TextPdfExporter>();
        services.TryAddScoped<DraftCleanupService>();
        services.TryAddSingleton<IEmailNotifier, TemplateEmailNotifier>();
        services.TryAddSingleton<ICurrentUserContext, DemoCurrentUserContext>();
        services.TryAddSingleton<SqlServerSchemaDescriptor>();
        return services;
    }
}
