# Using BlazorWebForms

This guide explains how to consume and actively develop `BlazorWebForms` while working in a separate solution.

## Package IDs

- `BlazorWebForms.Core`
- `BlazorWebForms.Blazor`
- `BlazorWebForms.Infrastructure.SqlServer`

## License and version baseline

- License metadata: `AGPL-3.0-only`
- Current package version: `1.0.0`

## Option 1: Consume from NuGet.org (published packages)

Use this when you want stable consumption.

```powershell
dotnet add package BlazorWebForms.Core --version 1.0.0
dotnet add package BlazorWebForms.Blazor --version 1.0.0
dotnet add package BlazorWebForms.Infrastructure.SqlServer --version 1.0.0
```

## Option 2: Local NuGet feed for active development

Use this when you are changing this repo and testing those changes in another solution.

### 1) Pack locally in dependency order

From this repository root:

```powershell
dotnet pack src/BlazorWebForms.Core/BlazorWebForms.Core.csproj -c Release -o ./nupkgs
dotnet pack src/BlazorWebForms.Blazor/BlazorWebForms.Blazor.csproj -c Release -o ./nupkgs
dotnet pack src/BlazorWebForms.Infrastructure.SqlServer/BlazorWebForms.Infrastructure.SqlServer.csproj -c Release -o ./nupkgs
```

### 2) Add local feed in your other solution

In the other solution root:

```powershell
dotnet nuget add source "D:\source\repos\HansAnonymous\BlazorWebForms\nupkgs" --name BlazorWebFormsLocal
```

Then install packages (same IDs as above).

### 3) Refresh package after each new local pack

If the version does not change, clear cache in the consumer solution:

```powershell
dotnet nuget locals all --clear
```

Recommended: bump version for each local test cycle (for example `1.0.1-local.1`) to avoid cache confusion.

## Option 3: ProjectReference for fastest inner-loop development

Use this when you want instant code/debug updates without packing.

In your other solution's `.csproj`, replace package references with project references to this repo:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\BlazorWebForms\src\BlazorWebForms.Core\BlazorWebForms.Core.csproj" />
  <ProjectReference Include="..\..\BlazorWebForms\src\BlazorWebForms.Blazor\BlazorWebForms.Blazor.csproj" />
  <ProjectReference Include="..\..\BlazorWebForms\src\BlazorWebForms.Infrastructure.SqlServer\BlazorWebForms.Infrastructure.SqlServer.csproj" />
</ItemGroup>
```

Notes:
- Keep either `PackageReference` or `ProjectReference` for the same library, not both.
- If your consumer app only needs UI components, you may only need `Core` + `Blazor`.
- Use `Infrastructure.SqlServer` only where SQL persistence integration is needed.

## Recommended workflow

- Daily app development: use **ProjectReference**.
- Validation before publishing: switch back to **PackageReference** and test with packed artifacts.
- Public release: publish the final package set to NuGet.org with matching versions.

## Custom form prefill providers

Register `IFormPrefillProvider` implementations when an app needs to populate answers from application-owned data such as HR tables, cost-center records, or other database lookups.

```csharp
builder.Services.AddBlazorWebFormsCore();
builder.Services.AddSingleton<IFormPrefillProvider, HrDatabasePrefillProvider>();

public sealed class HrDatabasePrefillProvider(EmployeeDbContext db) : IFormPrefillProvider
{
    public string ProviderKey => "hr-database";

    public bool CanResolve(FormFieldPrefillDefinition prefill) =>
        prefill.Source == PrefillSourceKind.Custom;

    public async Task<string?> ResolveAsync(FormPrefillRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.SubjectEmail ?? request.Requester.Email;
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.Email == email, cancellationToken);

        return request.Field.Prefill.Key switch
        {
            "department" => employee?.Department,
            "costCenter" => employee?.CostCenter,
            "managerEmail" => employee?.ManagerEmail,
            _ => null
        };
    }
}
```

Builder configuration for a custom field:

```text
Prefill source: Custom
Prefill provider key: hr-database
Prefill key: costCenter
Only prefill empty answers: true
```

Built-in providers remain available for `Claim`, `Employee` (`IEmployeePrefillProvider`), and `FixedValue`. Manager-prefilled drafts intentionally skip claim providers so a manager does not stamp their own claims into a submitter's draft; custom and employee providers receive `SubjectEmail` for the target submitter.

## Quick verification checklist

1. Restore succeeds in consumer solution.
2. Build succeeds in consumer solution.
3. App starts and key forms render.
4. SQL integration paths run (if using `Infrastructure.SqlServer`).
5. No stale cache package is being used (`dotnet nuget locals all --clear` if needed).
