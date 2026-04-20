using BlazorWebForms.Core.Services;
using BlazorWebForms.Infrastructure.SqlServer;
using BlazorWebForms.SampleApp.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddBlazorWebFormsCore();
builder.Services.AddBlazorWebFormsSqlServer(options =>
{
    options.StorageRoot = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "uploads");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    var formsService = scope.ServiceProvider.GetRequiredService<FormsApplicationService>();
    await formsService.SeedAsync();
}

app.Run();
