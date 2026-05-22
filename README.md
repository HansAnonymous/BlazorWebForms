# BlazorWebForms — Run & Dev Guide

Quick guide: run sample app with SQL persistence and apply EF migrations.

Prereqs
- .NET 8/10 SDK installed
- LocalDB (SQL Server Express LocalDB) or SQL Server accessible

Install EF CLI (if not already):

```powershell
dotnet tool install --global dotnet-ef
```

Restore and build:

```powershell
dotnet restore
dotnet build -c Debug
```

Apply migrations (creates database):

```powershell
dotnet ef database update --project src/BlazorWebForms.Infrastructure.SqlServer/BlazorWebForms.Infrastructure.SqlServer.csproj --startup-project src/BlazorWebForms.SampleApp/BlazorWebForms.SampleApp.csproj
```

Run sample app:

```powershell
dotnet run --project src/BlazorWebForms.SampleApp/BlazorWebForms.SampleApp.csproj -c Debug
```

Configuration
- Development connection string: `src/BlazorWebForms.SampleApp/appsettings.Development.json` → `ConnectionStrings:BlazorWebForms`.
- Optional schema override: `BlazorWebFormsSqlServer:SchemaName` in same file.
- Storage root defaults to `<SampleApp content root>/App_Data/uploads` but can be set in `Program.cs` or via options in `AddBlazorWebFormsSqlServer`.

Migrations
- Migration files live in `src/BlazorWebForms.Infrastructure.SqlServer/Migrations/`.

Tests

```powershell
dotnet run --project tests/BlazorWebForms.Infrastructure.SqlServer.Tests/BlazorWebForms.Infrastructure.SqlServer.Tests.csproj -c Debug
```

Notes
- Sample app `Program.cs` binds `ConnectionStrings:BlazorWebForms` automatically when present.
- If using a remote SQL Server, update connection string accordingly and ensure firewall access.
