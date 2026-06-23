# BlazorWebForms.Core — Extending & Implementing Interfaces

Guide to implementing and extending the Core package interfaces for custom functionality.

---

## Table of Contents

1. [Overview](#overview)
2. [Required: ICurrentUserContext](#required-icurrentusercontext)
3. [Optional: IPermissionEvaluator](#optional-ipermissionevaluator)
4. [Optional: IConditionEvaluator](#optional-iconditionevaluator)
5. [Optional: Serialization](#optional-serialization)
6. [Service Registration Pattern](#service-registration-pattern)
7. [Common Implementation Examples](#common-implementation-examples)

---

## Overview

The Core package is designed for extensibility. Your application must implement **one required interface** and can optionally override default implementations for advanced customization.

### Required vs. optional

| Interface | Required | Purpose |
|---|---|---|
| `ICurrentUserContext` | **Yes** | Provide current authenticated user info |
| `IPermissionEvaluator` | No | Custom permission/role logic |
| `IConditionEvaluator` | No | Custom field visibility rules |
| `IFormDefinitionSerializer` | No | Custom form schema serialization |

---

## Required: ICurrentUserContext

This is the **only required implementation**. It tells the service layer who the current user is.

### Interface

```csharp
public interface ICurrentUserContext
{
	UserProfile GetCurrentUser();
}
```

### UserProfile contract

```csharp
public sealed class UserProfile
{
	public Guid UserId { get; set; }
	public bool IsAuthenticated { get; set; }
	public string DisplayName { get; set; }
	public string Email { get; set; }
	public List<FormPermissionRole> Roles { get; set; }
}
```

| Property | Description |
|---|---|
| `UserId` | Stable GUID. Used for ownership and permission checks. |
| `IsAuthenticated` | Is user signed in? Drives access-mode enforcement. |
| `DisplayName` | Display name shown in UI and stored on entries. |
| `Email` | Email for notifications. |
| `Roles` | `FormPermissionRole` values (Admin, Owner, Manager, Approver, Submitter, Viewer, SelfViewer). |

### Implementation example (cookie auth)

```csharp
using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

public sealed class ClaimsCurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
	public UserProfile GetCurrentUser()
	{
		var principal = httpContextAccessor.HttpContext?.User;
		if (principal?.Identity is null || !principal.Identity.IsAuthenticated)
		{
			return new UserProfile
			{
				IsAuthenticated = false,
				DisplayName = "Anonymous"
			};
		}

		// Resolve a stable GUID user id
		var rawId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
				 ?? principal.FindFirst("sub")?.Value
				 ?? principal.FindFirst("oid")?.Value;

		var userId = Guid.TryParse(rawId, out var parsed)
			? parsed
			: DeterministicGuid(rawId ?? principal.Identity.Name ?? "anonymous");

		return new UserProfile
		{
			UserId = userId,
			IsAuthenticated = true,
			DisplayName = principal.FindFirst(ClaimTypes.Name)?.Value
						?? principal.Identity.Name
						?? "Authenticated User",
			Email = principal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
			Roles = ResolveRoles(principal)
		};
	}

	private static List<FormPermissionRole> ResolveRoles(ClaimsPrincipal principal)
	{
		var claims = principal.FindAll(ClaimTypes.Role)
			.Concat(principal.FindAll("role"))
			.Select(c => c.Value)
			.Distinct(StringComparer.OrdinalIgnoreCase);

		var mapped = new List<FormPermissionRole>();
		foreach (var role in claims)
		{
			if (Enum.TryParse<FormPermissionRole>(role, ignoreCase: true, out var r))
				mapped.Add(r);
		}
		return mapped;
	}

	private static Guid DeterministicGuid(string value)
	{
		var bytes = System.Security.Cryptography.MD5.HashData(
			System.Text.Encoding.UTF8.GetBytes(value));
		return new Guid(bytes);
	}
}
```

### Implementation example (OIDC / OAuth2)

```csharp
public sealed class OidcCurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
	public UserProfile GetCurrentUser()
	{
		var principal = httpContextAccessor.HttpContext?.User;
		if (principal?.Identity is null || !principal.Identity.IsAuthenticated)
		{
			return new UserProfile { IsAuthenticated = false, DisplayName = "Anonymous" };
		}

		// Extract OIDC claims
		var sub = principal.FindFirst("sub")?.Value;
		if (string.IsNullOrEmpty(sub))
			return new UserProfile { IsAuthenticated = false, DisplayName = "Anonymous" };

		var userId = Guid.TryParse(sub, out var guid) ? guid : DeterministicGuid(sub);

		return new UserProfile
		{
			UserId = userId,
			IsAuthenticated = true,
			DisplayName = principal.FindFirst("name")?.Value ?? "User",
			Email = principal.FindFirst("email")?.Value ?? string.Empty,
			Roles = ResolveScopeRoles(principal)
		};
	}

	private static List<FormPermissionRole> ResolveScopeRoles(ClaimsPrincipal principal)
	{
		// Example: extract roles from OIDC "roles" claim
		var roles = principal.FindFirst("roles")?.Value ?? string.Empty;
		var mapped = new List<FormPermissionRole>();

		foreach (var role in roles.Split(',', StringSplitOptions.RemoveEmptyEntries))
		{
			if (Enum.TryParse<FormPermissionRole>(role.Trim(), ignoreCase: true, out var r))
				mapped.Add(r);
		}

		return mapped;
	}

	private static Guid DeterministicGuid(string value)
	{
		var bytes = System.Security.Cryptography.MD5.HashData(
			System.Text.Encoding.UTF8.GetBytes(value));
		return new Guid(bytes);
	}
}
```

### Registration

In `Program.cs`, after `AddBlazorWebFormsCore()`:

```csharp
builder.Services.AddBlazorWebFormsCore();
// ... other services ...
builder.Services.AddScoped<ICurrentUserContext, ClaimsCurrentUserContext>();
```

---

## Optional: IPermissionEvaluator

Override permission logic to customize role-based access control.

### Interface

```csharp
public interface IPermissionEvaluator
{
	bool CanManageForm(FormAggregate form, UserProfile user);
	bool CanSubmitForm(FormAggregate form, UserProfile user);
	bool CanViewEntry(EntryRecord entry, FormAggregate form, UserProfile user);
	bool CanApproveEntry(EntryRecord entry, ApprovalStepRecord step, UserProfile user);
}
```

### Default implementation logic

The built-in implementation uses `FormPermissionRole`:

- **CanManageForm**: Admin, Owner, Manager, or has `Owner` grant on form
- **CanSubmitForm**: Admin, Owner, Manager, Submitter, or form is public/invitation-only
- **CanViewEntry**: Admin, Owner, Manager, Viewer, submitter themselves, or assigned approver
- **CanApproveEntry**: Assigned approver or Admin/Owner/Manager

### Custom implementation example

```csharp
public sealed class CustomPermissionEvaluator : IPermissionEvaluator
{
	public bool CanManageForm(FormAggregate form, UserProfile user)
	{
		// Example: Only admins or form owner can manage
		if (user.Roles.Contains(FormPermissionRole.Admin))
			return true;

		var ownerGrant = form.Permissions.FirstOrDefault(
			p => p.UserId == user.UserId && p.Role == FormPermissionRole.Owner);

		return ownerGrant != null;
	}

	public bool CanSubmitForm(FormAggregate form, UserProfile user)
	{
		// Example: Authenticated users can submit to authenticated forms
		if (form.Publication.AccessMode == FormAccessMode.Authenticated && user.IsAuthenticated)
			return true;

		// Public forms are open to anyone
		if (form.Publication.AccessMode == FormAccessMode.Public)
			return true;

		// Invitation-only requires an accepted invite
		if (form.Publication.AccessMode == FormAccessMode.InvitationOnly)
		{
			var grant = form.Permissions.FirstOrDefault(
				p => p.UserId == user.UserId &&
					 (p.Role == FormPermissionRole.Submitter ||
					  p.Role == FormPermissionRole.Admin));
			return grant != null;
		}

		return false;
	}

	public bool CanViewEntry(EntryRecord entry, FormAggregate form, UserProfile user)
	{
		// Admin, Owner, Manager can view any entry
		if (user.Roles.Contains(FormPermissionRole.Admin) ||
			user.Roles.Contains(FormPermissionRole.Owner) ||
			user.Roles.Contains(FormPermissionRole.Manager))
			return true;

		// Submitter can view their own entry
		if (entry.SubmittedByUserId == user.UserId)
			return true;

		// Assigned approver can view
		if (entry.ApprovalSteps.Any(s => s.ApproverEmail == user.Email))
			return true;

		return false;
	}

	public bool CanApproveEntry(EntryRecord entry, ApprovalStepRecord step, UserProfile user)
	{
		// Assigned approver can approve
		if (step.ApproverEmail == user.Email && step.Status == ApprovalStepStatus.Pending)
			return true;

		// Admin can always approve
		if (user.Roles.Contains(FormPermissionRole.Admin))
			return true;

		return false;
	}
}
```

### Registration

```csharp
builder.Services.AddSingleton<IPermissionEvaluator, CustomPermissionEvaluator>();
builder.Services.AddBlazorWebFormsCore();
```

---

## Optional: IConditionEvaluator

Override condition evaluation to implement custom visibility logic.

### Interface

```csharp
public interface IConditionEvaluator
{
	bool IsVisible(
		VisibilityConditionDefinition? condition,
		IReadOnlyDictionary<string, string?> answers);
}
```

### Default implementation

The built-in `DefaultConditionEvaluator` evaluates rules based on `VisibilityRuleOperator`:
- `Equals` — exact match
- `NotEquals` — not equal
- `Contains` — substring (case-insensitive)
- `Empty` — null or empty string

Combines rules with AND/OR logic per `VisibilityJoinOperator`.

### Custom implementation example

```csharp
public sealed class AdvancedConditionEvaluator : IConditionEvaluator
{
	public bool IsVisible(
		VisibilityConditionDefinition? condition,
		IReadOnlyDictionary<string, string?> answers)
	{
		if (condition is null || condition.Rules.Count == 0)
			return true;

		// Evaluate each rule
		var ruleResults = condition.Rules.Select(rule => EvaluateRule(rule, answers)).ToList();

		// Combine with Join operator
		return condition.Join == VisibilityJoinOperator.And
			? ruleResults.All(r => r)
			: ruleResults.Any(r => r);
	}

	private bool EvaluateRule(VisibilityRuleDefinition rule, IReadOnlyDictionary<string, string?> answers)
	{
		if (!answers.TryGetValue(rule.FieldId, out var answer))
			answer = null;

		return rule.Operator switch
		{
			VisibilityRuleOperator.Equals =>
				string.Equals(answer, rule.Value, StringComparison.Ordinal),

			VisibilityRuleOperator.NotEquals =>
				!string.Equals(answer, rule.Value, StringComparison.Ordinal),

			VisibilityRuleOperator.Contains =>
				!string.IsNullOrEmpty(answer) &&
				answer.Contains(rule.Value, StringComparison.OrdinalIgnoreCase),

			VisibilityRuleOperator.Empty =>
				string.IsNullOrEmpty(answer),

			_ => false
		};
	}
}
```

### Registration

```csharp
builder.Services.AddSingleton<IConditionEvaluator, AdvancedConditionEvaluator>();
builder.Services.AddBlazorWebFormsCore();
```

---

## Optional: Serialization

The `IFormDefinitionSerializer` interface controls how form definitions are serialized to/from JSON.

### Interface

```csharp
public interface IFormDefinitionSerializer
{
	string Serialize(FormDefinition definition);
	FormDefinition Deserialize(string json);
}
```

### Default implementation

The built-in `JsonFormDefinitionSerializer` uses `System.Text.Json` with custom property naming conventions and schema versioning.

### Custom implementation example

```csharp
using System.Text.Json;

public sealed class CustomFormDefinitionSerializer : IFormDefinitionSerializer
{
	private static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true
	};

	public string Serialize(FormDefinition definition)
	{
		return JsonSerializer.Serialize(definition, Options);
	}

	public FormDefinition Deserialize(string json)
	{
		var definition = JsonSerializer.Deserialize<FormDefinition>(json, Options)
			?? throw new InvalidOperationException("Failed to deserialize form definition.");

		// Validate schema version
		if (definition.SchemaVersion > FormDefinition.CurrentSchemaVersion)
			throw new InvalidOperationException("Unsupported schema version.");

		// Upgrade legacy versions if needed
		if (definition.SchemaVersion < FormDefinition.CurrentSchemaVersion)
			UpgradeLegacyDefinition(definition);

		return definition;
	}

	private void UpgradeLegacyDefinition(FormDefinition definition)
	{
		// Example: Upgrade v1 to v2
		if (definition.SchemaVersion == 1)
		{
			// Apply migrations
			definition.SchemaVersion = FormDefinition.CurrentSchemaVersion;
		}
	}
}
```

### Registration

```csharp
builder.Services.AddSingleton<IFormDefinitionSerializer, CustomFormDefinitionSerializer>();
builder.Services.AddBlazorWebFormsCore();
```

---

## Service Registration Pattern

Follow this pattern when registering custom implementations:

```csharp
var builder = WebApplicationBuilder.CreateBuilder(args);

// 1. Register UI framework (if using Blazor)
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

// 2. Register custom implementations (before AddBlazorWebFormsCore)
builder.Services.AddSingleton<IPermissionEvaluator, CustomPermissionEvaluator>();
builder.Services.AddSingleton<IConditionEvaluator, AdvancedConditionEvaluator>();
builder.Services.AddSingleton<IFormDefinitionSerializer, CustomFormDefinitionSerializer>();

// 3. Register Core (uses TryAdd* so custom implementations are not overridden)
builder.Services.AddBlazorWebFormsCore();

// 4. Register required ICurrentUserContext implementation (must be scoped)
builder.Services.AddScoped<ICurrentUserContext, ClaimsCurrentUserContext>();

// 5. Build and run
var app = builder.Build();
app.Run();
```

**Important**: `AddBlazorWebFormsCore()` uses `TryAddSingleton` / `TryAddScoped`, so your implementations registered before it will be used. If you register after, the defaults will take precedence.

---

## Common Implementation Examples

### Example: Database-backed permissions

```csharp
public sealed class DatabasePermissionEvaluator : IPermissionEvaluator
{
	private readonly IPermissionRepository _repository;

	public DatabasePermissionEvaluator(IPermissionRepository repository)
	{
		_repository = repository;
	}

	public bool CanManageForm(FormAggregate form, UserProfile user)
	{
		var permission = _repository.GetPermission(user.UserId, form.Id, "Manage");
		return permission != null && permission.IsActive;
	}

	// ... other methods using database queries ...
}
```

### Example: Azure AD group-based permissions

```csharp
public sealed class AzureAdPermissionEvaluator : IPermissionEvaluator
{
	private readonly GraphServiceClient _graphClient;

	public AzureAdPermissionEvaluator(GraphServiceClient graphClient)
	{
		_graphClient = graphClient;
	}

	public bool CanManageForm(FormAggregate form, UserProfile user)
	{
		var groups = _graphClient.Users[user.Email].MemberOf
			.GetAsync(x => x.QueryParameters.Select = new[] { "id" })
			.Result;

		return groups.Value.Any(g =>
			g.Id == "admin-group-id" ||
			g.Id == form.Id.ToString());
	}

	// ... other methods querying Azure AD ...
}
```

### Example: Time-based field visibility

```csharp
public sealed class ScheduledConditionEvaluator : IConditionEvaluator
{
	public bool IsVisible(
		VisibilityConditionDefinition? condition,
		IReadOnlyDictionary<string, string?> answers)
	{
		if (condition is null)
			return true;

		// Custom rule: "after-date" operator
		var timeBasedRule = condition.Rules.FirstOrDefault(
			r => r.Operator == VisibilityRuleOperator.Contains &&
				 r.FieldId == "__schedule__");

		if (timeBasedRule != null && DateTime.TryParse(timeBasedRule.Value, out var afterDate))
		{
			if (DateTime.UtcNow < afterDate)
				return false;
		}

		// Delegate to default for other rules
		var defaultEvaluator = new DefaultConditionEvaluator();
		return defaultEvaluator.IsVisible(condition, answers);
	}
}
```

---

## Next Steps

- See [CORE_API_REFERENCE.md](./CORE_API_REFERENCE.md) for FormsApplicationService methods
- See [CORE_MODELS.md](./CORE_MODELS.md) for domain model reference
- See [CORE_LOCALIZATION.md](./CORE_LOCALIZATION.md) for localization and validation
