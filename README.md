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
- Development auth: sample app now uses cookie authentication. Use the top-right "Dev login" controls in the app shell to sign in as `Manager`, `Owner`, `Approver`, or `Admin`.
- Auth assumptions: stable user id is derived from NameIdentifier/sub/oid claim (or deterministic fallback), display name from `name`/identity name, email from `email` claim.
- Unauthorized route handling: protected routes now show explicit `Sign in required` for anonymous users and `Access denied` for authenticated users without required roles.
- Invitation endpoints (optional module):
  - `POST /invitations/create` (authorized: Admin/Owner/Manager)
  - `POST /invitations/accept` (authorized: authenticated user)
  - `POST /invitations/revoke/{invitationId}` (authorized: Admin/Owner/Manager)
- Invitation notifications: invitation create/accept/revoke events now flow through `IEmailNotifier` integration points.

Migrations
- Migration files live in `src/BlazorWebForms.Infrastructure.SqlServer/Migrations/`.
- Generate and review SQL script before apply:

```powershell
dotnet ef migrations script --project src/BlazorWebForms.Infrastructure.SqlServer/BlazorWebForms.Infrastructure.SqlServer.csproj --startup-project src/BlazorWebForms.SampleApp/BlazorWebForms.SampleApp.csproj
```

- SQL compatibility note: current migrations use SQL Server/Azure SQL compatible types (`uniqueidentifier`, `nvarchar`, `datetimeoffset`, `rowversion`) and are validated in local integration runs.

Tests

```powershell
dotnet run --project tests/BlazorWebForms.Infrastructure.SqlServer.Tests/BlazorWebForms.Infrastructure.SqlServer.Tests.csproj -c Debug
```

Notes
- Sample app `Program.cs` binds `ConnectionStrings:BlazorWebForms` automatically when present.
- If using a remote SQL Server, update connection string accordingly and ensure firewall access.
