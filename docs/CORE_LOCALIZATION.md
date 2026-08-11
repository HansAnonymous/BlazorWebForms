# BlazorWebForms.Core — Localization & Validation Guide

Guide to multilingual form support, condition evaluation, and input validation in BlazorWebForms.Core.

---

## Table of Contents

1. [Localization Overview](#localization-overview)
2. [Culture Resolution](#culture-resolution)
3. [Localizable Elements](#localizable-elements)
4. [FormLocalizationResolver](#formlocationresolver)
5. [Condition Evaluation](#condition-evaluation)
6. [Form Validation](#form-validation)
7. [Runtime Validation](#runtime-validation)

---

## Localization Overview

BlazorWebForms supports multilingual forms through **localization dictionaries** on form definitions. Every `FormDefinition`, `FormSectionDefinition`, `FormFieldDefinition`, and `FormFieldOption` can carry culture-specific text.

### Key concepts

1. **Base language** — Default text in the property itself (e.g. `field.Label = "Name"`)
2. **Localized variants** — Culture-specific overrides in dictionary properties (e.g. `field.LocalizedLabels["fr-FR"] = "Nom"`)
3. **Culture fallback** — Multi-level fallback chain when a translation is missing
4. **DefaultCulture** — Form's default culture code (e.g. `"en-US"`)

---

## Culture Resolution

The `FormLocalizationResolver.ResolveText()` method implements culture fallback logic:

```csharp
public static string ResolveText(
	string baseText,
	Dictionary<string, string> localizedMap,
	string requestedCulture,
	string defaultCulture)
```

### Fallback chain

When resolving text for a requested culture:

1. **Exact match** — If `localizedMap[requestedCulture]` exists, use it
2. **Language fallback** — If requesting `"fr-CA"`, try `"fr"` (first part of culture code)
3. **Default culture** — Fall back to `localizedMap[defaultCulture]`
4. **Base text** — Use the base property (e.g. `field.Label`)

**Example**:

```csharp
var field = new FormFieldDefinition
{
	Label = "First name",  // base English
	LocalizedLabels = new()
	{
		["fr-FR"] = "Prénom",
		["es-ES"] = "Nombre"
	}
};
var definition = new FormDefinition
{
	DefaultCulture = "en-US"
};

// Requesting French (France)
var text = FormLocalizationResolver.ResolveText(
	field.Label,
	field.LocalizedLabels,
	requestedCulture: "fr-FR",
	definition.DefaultCulture);
// Result: "Prénom" (exact match)

// Requesting French (Canada)
text = FormLocalizationResolver.ResolveText(
	field.Label,
	field.LocalizedLabels,
	requestedCulture: "fr-CA",
	definition.DefaultCulture);
// Result: "Prénom" (language fallback to "fr-FR")

// Requesting German (not in dictionary)
text = FormLocalizationResolver.ResolveText(
	field.Label,
	field.LocalizedLabels,
	requestedCulture: "de-DE",
	definition.DefaultCulture);
// Result: "First name" (base text)
```

---

## Localizable Elements

### FormDefinition

```csharp
definition.Title = "Travel Request";
definition.LocalizedTitles["fr-FR"] = "Demande de voyage";
definition.LocalizedTitles["es-ES"] = "Solicitud de viaje";

definition.Description = "Request time off for business travel";
definition.LocalizedDescriptions["fr-FR"] = "Demander du temps pour les voyages d'affaires";
```

---

### FormSectionDefinition

```csharp
section.Title = "Trip Details";
section.LocalizedTitles["fr-FR"] = "Détails du voyage";

section.Description = "Enter information about your trip";
section.LocalizedDescriptions["fr-FR"] = "Entrez les informations de votre voyage";
```

---

### FormFieldDefinition

Every field can be localized across label, placeholder, help text, and validation hint:

```csharp
field.Label = "Destination";
field.LocalizedLabels["fr-FR"] = "Destination";
field.LocalizedLabels["es-ES"] = "Destino";

field.Placeholder = "e.g., New York";
field.LocalizedPlaceholders["fr-FR"] = "p. ex., New York";

field.HelpText = "Where will you travel?";
field.LocalizedHelpTexts["fr-FR"] = "Où voyagerez-vous?";

field.ValidationHint = "Please enter a valid destination";
field.LocalizedValidationHints["fr-FR"] = "Veuillez entrer une destination valide";
```

---

### FormFieldOption

Select and Radio fields have localizable options:

```csharp
var option = new FormFieldOption
{
	Value = "yes",
	Label = "Yes",
	LocalizedLabels = new() { ["fr-FR"] = "Oui", ["es-ES"] = "Sí" }
};

field.Options.Add(option);
```

---

## FormLocalizationResolver

Static utility for resolving localized text at render time.

```csharp
public static class FormLocalizationResolver
{
	public static string ResolveText(
		string baseText,
		Dictionary<string, string> localizedMap,
		string requestedCulture,
		string defaultCulture)
	{ ... }
}
```

### Usage in components

```razor
@using BlazorWebForms.Core.Services

@code {
	[Parameter]
	public FormDefinition Definition { get; set; }

	[Parameter]
	public string Culture { get; set; } = CultureInfo.CurrentUICulture.Name;

	private string GetLabel(FormFieldDefinition field) =>
		FormLocalizationResolver.ResolveText(
			field.Label,
			field.LocalizedLabels,
			Culture,
			Definition.DefaultCulture);
}
```

---

## Condition Evaluation

The `IConditionEvaluator` service evaluates visibility conditions at runtime, determining which sections and fields are displayed based on submitted answers.

### Injecting the evaluator

```csharp
public sealed class MyFormComponent : ComponentBase
{
	[Inject]
	private IConditionEvaluator ConditionEvaluator { get; set; }

	private Dictionary<string, string?> answers = new();

	private bool IsSectionVisible(FormSectionDefinition section) =>
		ConditionEvaluator.IsVisible(section.VisibilityRules, answers);

	private bool IsFieldVisible(FormFieldDefinition field) =>
		ConditionEvaluator.IsVisible(field.VisibilityRules, answers);
}
```

### Interface

```csharp
public interface IConditionEvaluator
{
	bool IsVisible(
		VisibilityConditionDefinition? condition,
		IReadOnlyDictionary<string, string?> answers);
}
```

### Logic

- If `condition` is null, returns `true` (always visible)
- If `Join = And`, all rules must be true
- If `Join = Or`, at least one rule must be true
- Each rule compares the answer for `FieldId` against `Value` using the specified `Operator`

### Operators

| Operator | Behavior |
|---|---|
| `Equals` | Answer equals value exactly |
| `NotEquals` | Answer does not equal value |
| `Contains` | Answer contains value (substring, case-insensitive) |
| `Empty` | Answer is null or empty string |

### Example

```csharp
var definition = new FormDefinition
{
	Sections = new()
	{
		new FormSectionDefinition
		{
			Id = "section-1",
			Title = "Employment",
			Fields = new()
			{
				new FormFieldDefinition
				{
					Id = "employment-status",
					Kind = FormFieldKind.Select,
					Label = "Employment status",
					Options = new()
					{
						new FormFieldOption { Value = "employed", Label = "Employed" },
						new FormFieldOption { Value = "unemployed", Label = "Unemployed" }
					}
				},
				new FormFieldDefinition
				{
					Id = "employer",
					Kind = FormFieldKind.Text,
					Label = "Employer name",
					VisibilityRules = new VisibilityConditionDefinition
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
					}
				}
			}
		}
	}
};

var answers = new Dictionary<string, string?> { ["employment-status"] = "employed" };
var evaluator = provider.GetRequiredService<IConditionEvaluator>();

// The "Employer name" field is visible because employment-status = "employed"
var visible = evaluator.IsVisible(
	definition.Sections[0].Fields[1].VisibilityRules,
	answers);
// Result: true

answers["employment-status"] = "unemployed";
visible = evaluator.IsVisible(
	definition.Sections[0].Fields[1].VisibilityRules,
	answers);
// Result: false (field is hidden)
```

---

## Form Validation

All form definition validation occurs during `SaveDraftAsync` and `PublishAsync`. Invalid definitions throw `InvalidOperationException`.

### Culture validation

- **Default culture** must be a valid `CultureInfo` (e.g. `"en-US"`)
- **All cultures in localized dictionaries** must be valid
- Empty culture keys are rejected
- **Error messages**:
  - `"Default culture is required."`
  - `"Default culture is not a valid culture."`
  - `"Form localized titles culture 'xyz' is not a valid culture."`

### Example

```csharp
var definition = new FormDefinition
{
	DefaultCulture = "invalid-culture",  // Invalid!
	Title = "My form",
	Sections = new() { ... }
};

try
{
	await formsService.SaveDraftAsync(new SaveDraftRequest
	{
		Name = "Test",
		Definition = definition
	});
}
catch (InvalidOperationException ex)
{
	Console.WriteLine(ex.Message);  // "Default culture is not a valid culture."
}
```

---

### Schema validation

- Form title must not be empty
- At least one section required
- Each section must have at least one field
- Field ids must be unique within a section
- Visibility conditions must reference existing fields
- **Error messages**:
  - `"Form must have at least one section."`
  - `"Section must have at least one field."`
  - `"Condition references field 'unknown-id' which does not exist."`

---

### Slug validation

- Must be 3-100 characters
- Alphanumeric, hyphens, underscores only
- Must be unique among published forms with the same `AccessMode`
- **Error messages**:
  - `"Slug must be unique."`
  - `"Slug must contain only alphanumeric characters, hyphens, and underscores."`

---

### Localized text validation

All localized text (in `LocalizedTitles`, `LocalizedLabels`, etc.) must be non-empty if present.

- **Error messages**:
  - `"Form localized titles contains an empty culture key."`
  - `"Form localized titles has empty localized text for culture 'fr-FR'."`

---

## Runtime Validation

When entries are submitted via `SubmitEntryAsync`, answers are validated against field definitions.

### Required fields

If `field.Required = true`, the answer must not be null or empty string.

**Error**: `"Field 'First name' is required."`

---

### Regex patterns

If `field.RegexPattern` is set, the answer must match the pattern.

```csharp
var emailField = new FormFieldDefinition
{
	Id = "email",
	Kind = FormFieldKind.Text,
	Label = "Email",
	RegexPattern = @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
	ValidationHint = "Please enter a valid email address"
};
```

**Error**: Uses `field.ValidationHint` if provided, else a generic pattern error.

---

### File constraints

When files are uploaded via `StoreFileAsync`, constraints are validated:

```csharp
var fileField = new FormFieldDefinition
{
	Id = "attachment",
	Kind = FormFieldKind.File,
	MaxFileSizeBytes = 5 * 1024 * 1024,      // 5 MB
	AllowedMimeTypes = new() { "application/pdf", "image/png" },
	AllowedExtensions = new() { ".pdf", ".png" }
};

var request = new FileUploadRequest
{
	FileName = "large-file.zip",
	ContentType = "application/zip",
	Content = fileBytes,
	MaxAllowedBytes = 5 * 1024 * 1024,
	AllowedMimeTypes = fileField.AllowedMimeTypes,
	AllowedExtensions = fileField.AllowedExtensions
};

try
{
	await formsService.StoreFileAsync(request);
}
catch (InvalidOperationException ex)
{
	// Could be:
	// "File size exceeds allowed maximum."
	// "File MIME type is not allowed."
	// "File extension is not allowed."
	Console.WriteLine(ex.Message);
}
```

---

## Best Practices

### 1. Always provide base text

Include text in the base property even if translations exist. This serves as a fallback if a culture is not translated:

```csharp
field.Label = "First Name";  // Always provide English base
field.LocalizedLabels["fr-FR"] = "Prénom";
field.LocalizedLabels["es-ES"] = "Nombre";
```

### 2. Validate cultures early

Call `SaveDraftAsync` or `PublishAsync` to catch culture errors before deployment:

```csharp
await formsService.SaveDraftAsync(new SaveDraftRequest
{
	Definition = definition
	// If definition has invalid cultures, this throws
});
```

### 3. Use standard culture codes

Use IETF language tags (e.g. `"en-US"`, `"fr-FR"`, `"de-DE"`). These are recognized by .NET's `CultureInfo.GetCultureInfo()`.

```csharp
// Valid
definition.DefaultCulture = "en-US";
definition.LocalizedTitles["fr-FR"] = "Titre";

// Invalid
definition.DefaultCulture = "English";  // Throws on save
definition.LocalizedTitles["french"] = "Titre";  // Throws on save
```

### 4. Resolve text in render components

Always use `FormLocalizationResolver` in render components to handle fallback:

```razor
@{
	var label = FormLocalizationResolver.ResolveText(
		Definition.Sections[0].Fields[0].Label,
		Definition.Sections[0].Fields[0].LocalizedLabels,
		Culture,
		Definition.DefaultCulture);
}
<label>@label</label>
```

### 5. Test visibility conditions

Before publishing, verify that visibility rules reference valid field ids and produce the expected results:

```csharp
var evaluator = provider.GetRequiredService<IConditionEvaluator>();
var testAnswers = new Dictionary<string, string?> { ["field-id"] = "value" };

var visible = evaluator.IsVisible(field.VisibilityRules, testAnswers);
Assert(visible == expected, "Visibility condition failed");
```

---

## Next Steps

- See [CORE_API_REFERENCE.md](./CORE_API_REFERENCE.md) for FormsApplicationService methods
- See [CORE_MODELS.md](./CORE_MODELS.md) for model reference
- See [CORE_EXTENDING.md](./CORE_EXTENDING.md) for implementing custom interfaces
