# BlazorWebForms.Core — Technical Reference

Core package documentation and quick-start guide.

## Documentation

The Core package includes comprehensive technical documentation:

- **[CORE_API_REFERENCE.md](../../docs/CORE_API_REFERENCE.md)** — Complete `FormsApplicationService` API reference and service interfaces
- **[CORE_MODELS.md](../../docs/CORE_MODELS.md)** — Domain model reference (FormAggregate, EntryRecord, FormDefinition, etc.)
- **[CORE_LOCALIZATION.md](../../docs/CORE_LOCALIZATION.md)** — Localization, culture resolution, condition evaluation, and validation
- **[CORE_EXTENDING.md](../../docs/CORE_EXTENDING.md)** — Implementing required and optional interfaces
- **[QUESTION_TYPES.md](../../docs/QUESTION_TYPES.md)** — Every `FormFieldKind` explained with properties, validation rules, answer format, and examples

For integration guide and usage patterns: [docs/USING_BlazorWebForms.md](../../docs/USING_BlazorWebForms.md)

---

## Quick Start

### Prerequisites

- .NET 10 SDK
- (Optional) SQL Server or LocalDB

### Package Installation

```powershell
dotnet add package BlazorWebForms.Core --version 1.0.0
```

Only add additional packages if needed:
- `BlazorWebForms.Blazor` — for Blazor UI components
- `BlazorWebForms.Infrastructure.SqlServer` — for SQL persistence

### Service Registration

In your `Program.cs`:

```csharp
using BlazorWebForms.Core.Services;

var builder = WebApplicationBuilder.CreateBuilder(args);

// Register Core services
builder.Services.AddBlazorWebFormsCore();

// Implement the required ICurrentUserContext interface
builder.Services.AddScoped<ICurrentUserContext, YourCurrentUserContext>();

var app = builder.Build();
app.Run();
```

### Required: Implement ICurrentUserContext

This is the only required implementation. It tells the service who the current user is:

```csharp
using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

public sealed class YourCurrentUserContext(IHttpContextAccessor httpContextAccessor) 
	: ICurrentUserContext
{
	public UserProfile GetCurrentUser()
	{
		var principal = httpContextAccessor.HttpContext?.User;
		if (principal?.Identity is null || !principal.Identity.IsAuthenticated)
		{
			return new UserProfile { IsAuthenticated = false };
		}

		return new UserProfile
		{
			UserId = Guid.Parse(principal.FindFirst("sub")?.Value ?? Guid.NewGuid().ToString()),
			IsAuthenticated = true,
			DisplayName = principal.FindFirst("name")?.Value ?? "User",
			Email = principal.FindFirst("email")?.Value ?? string.Empty,
			Roles = new() { FormPermissionRole.Submitter }
		};
	}
}
```

See [CORE_EXTENDING.md](../../docs/CORE_EXTENDING.md) for detailed implementation examples (cookie auth, OIDC, Azure AD).

### Using FormsApplicationService

```csharp
public sealed class MyComponent : ComponentBase
{
	[Inject]
	private FormsApplicationService FormsService { get; set; } = null!;

	private async Task SubmitForm()
	{
		var entry = await FormsService.SubmitEntryAsync(formId, new SubmitEntryRequest
		{
			Answers = new() { ["name"] = "John Doe" },
			Approvers = new() { new ApproverInput { Name = "Jane", Email = "jane@example.com" } },
			Files = new()
		});

		Console.WriteLine($"Entry created: {entry.Id}");
	}
}
```

---

## Package Contents

```
BlazorWebForms.Core/
├── Abstractions/
│   ├── Contracts.cs              # Service interfaces (IFormsRepository, IFileStorage, etc.)
│   └── ICoreMetadataCache.cs     # Caching interface
├── Models/
│   ├── DomainModels.cs           # FormAggregate, EntryRecord, etc.
│   ├── Enums.cs                  # FormFieldKind, EntryStatus, FormAccessMode, etc.
│   ├── FormDefinitionModels.cs   # FormDefinition, FormSectionDefinition, etc.
│   └── ViewModels.cs             # DashboardViewModel, PublishedFormViewModel, etc.
└── Services/
	├── FormsApplicationService.cs # Main entry point for all operations
	├── DefaultImplementations.cs  # Built-in IPermissionEvaluator, IConditionEvaluator
	├── FormLocalizationResolver.cs
	├── FormLayoutResolver.cs
	├── ConditionEvaluationHelper.cs
	├── InMemoryCoreMetadataCache.cs
	├── BlazorWebFormsServiceCollectionExtensions.cs
	└── DemoFormFactory.cs
```

---

## Architecture

### No external dependencies

BlazorWebForms.Core has **zero dependencies** on UI frameworks, databases, or external services. It is pure business logic and domain models.

### Interfaces for extensibility

The package provides interfaces for:
- Permission evaluation (`IPermissionEvaluator`)
- Condition evaluation (`IConditionEvaluator`)
- Form serialization (`IFormDefinitionSerializer`)
- And many more...

Default implementations are provided. Override them by registering your own before calling `AddBlazorWebFormsCore()`.

### Validation & error handling

All business logic errors throw `InvalidOperationException` with descriptive messages. These are intended to be caught and logged by the caller.

**Example error messages**:
- `"Default culture is not a valid culture."`
- `"User does not have permission to manage forms."`
- `"Field 'First name' is required."`

---

## Key APIs

### FormsApplicationService (scoped)

Single entry point for all operations:

```csharp
// Form management
await formsService.GetDashboardAsync();
await formsService.GetBuilderStateAsync(formId);
await formsService.SaveDraftAsync(request);
await formsService.PublishAsync(formId);

// Submissions
await formsService.SubmitEntryAsync(formId, request);
await formsService.SaveDraftSubmissionAsync(formId, request);
await formsService.GetDraftSubmissionAsync(formId);

// Queries
await formsService.QueryEntriesAsync(options);
await formsService.GetEntryDetailAsync(entryId);

// Approval workflow
await formsService.ApproveStepAsync(entryId, stepId, signature);
await formsService.RejectStepAsync(entryId, stepId, reason);

// Files
await formsService.StoreFileAsync(request);
await formsService.OpenEntryFileAsync(entryId, fileId);

// Invitations
await formsService.CreateInvitationAsync(request);
await formsService.AcceptInvitationAsync(token);
```

See [CORE_API_REFERENCE.md](../../docs/CORE_API_REFERENCE.md) for complete method reference.

### Domain Models

```csharp
// Form aggregate
FormAggregate form = await formsService.SaveDraftAsync(...);
form.Name;
form.Publication;
form.DraftDefinition;

// Entry submission
EntryRecord entry = await formsService.SubmitEntryAsync(...);
entry.Status;      // Draft, NeedsApproval, Approved, Rejected
entry.Answers;     // Dictionary<string, string?>
entry.ApprovalSteps;
entry.ApprovalAuditTrail;

// Form definition
FormDefinition definition = form.DraftDefinition;
definition.DefaultCulture;    // e.g., "en-US"
definition.Sections;          // FormSectionDefinition[]
definition.LocalizedTitles;   // Culture-specific titles
definition.Branding;          // Logo, colors, hero text, etc.
```

See [CORE_MODELS.md](../../docs/CORE_MODELS.md) for complete model reference.

---

## Localization & Validation

### Localization

Every form definition carries localization dictionaries:

```csharp
definition.Title = "Travel Request";
definition.LocalizedTitles["fr-FR"] = "Demande de voyage";

section.Title = "Trip Details";
section.LocalizedTitles["fr-FR"] = "Détails du voyage";

field.Label = "Destination";
field.LocalizedLabels["fr-FR"] = "Destination";
```

Culture resolution uses fallback: requested culture → language → default culture → base text.

### Condition Evaluation

Fields and sections can be conditionally shown/hidden based on other answers:

```csharp
field.VisibilityRules = new VisibilityConditionDefinition
{
	Join = VisibilityJoinOperator.And,
	Rules = new()
	{
		new VisibilityRuleDefinition
		{
			FieldId = "employment-status",
			Operator = VisibilityRuleOperator.Equals,
			Value = "employed"
		}
	}
};
```

Injected `IConditionEvaluator` evaluates rules at render time.

### Validation

All definitions are validated during save/publish:
- Cultures must be valid (e.g., `"en-US"`, not `"invalid-culture"`)
- Form must have at least one section
- Fields must have unique ids within a section
- Visibility conditions must reference existing fields
- Required fields must have non-null answers at submit time

---

## Testing

Run the test suite:

```powershell
# Core tests (executable test program)
dotnet run --project tests/BlazorWebForms.Core.Tests/BlazorWebForms.Core.Tests.csproj -c Debug
```

Test assertions cover:
- Form definition serialization/deserialization
- Culture validation
- Visibility condition evaluation
- Approval workflow state transitions
- Permission checks
- File uploads and constraints
- Email notifications
- Invitation lifecycle
- Large-form performance
- Localization fallback

---

## Performance Notes

- Forms are cached in-memory per user (short-lived, cleared on mutation)
- Entries are paged (default 50, max 1000 per query)
- Visibility evaluation is O(rules) — runs at render time, not expensive
- Condition/permission checks do not hit the database in the Core package

---

## Security Notes

- Cultures are validated against `CultureInfo.GetCultureInfo()` to prevent injection
- All permission checks are performed in `IPermissionEvaluator` — override for custom logic
- File uploads are constrained by size, extension, and MIME type
- All timestamps are in UTC
- Sensitive data (passwords, tokens) are never logged in validation errors

---

## Related Projects

- **BlazorWebForms.Blazor** — Razor components (FormBuilderWorkspace, PublishedFormView, FormsAdminDashboard, DynamicFormRenderer)
- **BlazorWebForms.Infrastructure.SqlServer** — EF Core persistence, email integration, PDF export, file storage
- **BlazorWebForms.SampleApp** — Full working application demonstrating Core, Blazor, and Infrastructure
