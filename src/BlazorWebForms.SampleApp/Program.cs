using BlazorWebForms.Core.Services;
using BlazorWebForms.Infrastructure.SqlServer;
using BlazorWebForms.SampleApp.Localization;
using BlazorWebForms.SampleApp;
using BlazorWebForms.SampleApp.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllersWithViews();
builder.Services.AddLocalization();
builder.Services.AddSingleton<AppLocalizer>();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = AppLocalizer.SupportedCultures
        .Select(CultureInfo.GetCultureInfo)
        .ToList();

    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders =
    [
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/";
        options.AccessDeniedPath = "/";
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.Authenticated, policy => policy.RequireAuthenticatedUser());
    options.AddPolicy(AuthPolicies.AdminConsole, policy => policy.Requirements.Add(new RoleSetRequirement("Admin", "Owner", "Manager")));
    options.AddPolicy(AuthPolicies.BuilderAccess, policy => policy.Requirements.Add(new RoleSetRequirement("Admin", "Owner", "Manager")));
    options.AddPolicy(AuthPolicies.PublishAccess, policy => policy.Requirements.Add(new RoleSetRequirement("Admin", "Owner", "Manager")));
    options.AddPolicy(AuthPolicies.ApproverAction, policy => policy.Requirements.Add(new RoleSetRequirement("Admin", "Owner", "Manager", "Approver")));
    options.AddPolicy(AuthPolicies.SelfViewAccess, policy => policy.Requirements.Add(new SelfOrManagerRequirement()));
    options.AddPolicy(AuthPolicies.InvitationManage, policy => policy.Requirements.Add(new RoleSetRequirement("Admin", "Owner", "Manager")));
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddBlazorWebFormsCore();
builder.Services.AddBlazorWebFormsSqlServer(options =>
{
    options.StorageRoot = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "uploads");
    options.EnableOutboundEmail = builder.Environment.IsProduction();
    options.EnableGraphIntegration = builder.Environment.IsProduction();
    options.EmailProviderStrategy = builder.Configuration["BlazorWebFormsSqlServer:EmailProviderStrategy"] ?? (builder.Environment.IsProduction() ? "Smtp" : "DryRun");
    options.EmailFromAddress = builder.Configuration["BlazorWebFormsSqlServer:EmailFromAddress"] ?? "noreply@example.com";
    options.EmailFromDisplayName = builder.Configuration["BlazorWebFormsSqlServer:EmailFromDisplayName"] ?? "BlazorWebForms";
    options.SmtpHost = builder.Configuration["BlazorWebFormsSqlServer:SmtpHost"];
    options.SmtpPort = int.TryParse(builder.Configuration["BlazorWebFormsSqlServer:SmtpPort"], out var smtpPort) ? smtpPort : 587;
    options.SmtpEnableSsl = !string.Equals(builder.Configuration["BlazorWebFormsSqlServer:SmtpEnableSsl"], "false", StringComparison.OrdinalIgnoreCase);
    options.SmtpUsername = builder.Configuration["BlazorWebFormsSqlServer:SmtpUsername"];
    options.SmtpPassword = builder.Configuration["BlazorWebFormsSqlServer:SmtpPassword"];
    options.SendGridApiKey = builder.Configuration["BlazorWebFormsSqlServer:SendGridApiKey"];
    options.GraphSender = builder.Configuration["BlazorWebFormsSqlServer:GraphSender"];
    options.GraphAccessToken = builder.Configuration["BlazorWebFormsSqlServer:GraphAccessToken"];

    // allow override via configuration (ConnectionStrings:BlazorWebForms)
    var conn = builder.Configuration.GetConnectionString("BlazorWebForms");
    if (!string.IsNullOrWhiteSpace(conn))
    {
        options.ConnectionString = conn;
    }

    // optional: override schema name via BlazorWebFormsSqlServer:SchemaName
    var schema = builder.Configuration["BlazorWebFormsSqlServer:SchemaName"];
    if (!string.IsNullOrWhiteSpace(schema))
    {
        options.SchemaName = schema;
    }
});
builder.Services.AddSingleton<IAuthorizationHandler, RoleSetAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, SelfOrManagerAuthorizationHandler>();
builder.Services.AddSingleton<EntryFileDownloadTokenService>();
builder.Services.AddSingleton<DevelopmentOnlyFilter>();
builder.Services.AddScoped<BlazorWebForms.Core.Abstractions.ICurrentUserContext, ClaimsCurrentUserContext>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

var localizationOptions = app.Services
    .GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>()
    .Value;
app.UseRequestLocalization(localizationOptions);

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    context.Response.Headers["Permissions-Policy"] = "unload=self, fullscreen=(), geolocation=()";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapGet("/culture/set", (HttpContext httpContext, string culture, string? returnUrl) =>
{
    var supported = AppLocalizer.SupportedCultures.Any(c => string.Equals(c, culture, StringComparison.OrdinalIgnoreCase));
    if (supported)
    {
        var requestCulture = new RequestCulture(culture, culture);
        httpContext.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(requestCulture),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax });
    }

    var destination = "/";
    if (!string.IsNullOrWhiteSpace(returnUrl) && Uri.TryCreate(returnUrl, UriKind.Relative, out _))
    {
        destination = returnUrl;
    }

    return Results.LocalRedirect(destination);
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    var formsService = scope.ServiceProvider.GetRequiredService<FormsApplicationService>();
    await formsService.SeedAsync();
}

app.Run();
