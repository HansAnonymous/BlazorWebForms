using BlazorWebForms.Core.Services;
using BlazorWebForms.Infrastructure.SqlServer;
using BlazorWebForms.SampleApp;
using BlazorWebForms.SampleApp.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllersWithViews();

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
builder.Services.AddScoped<BlazorWebForms.Core.Abstractions.ICurrentUserContext, ClaimsCurrentUserContext>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    var formsService = scope.ServiceProvider.GetRequiredService<FormsApplicationService>();
    await formsService.SeedAsync();
}

app.Run();
