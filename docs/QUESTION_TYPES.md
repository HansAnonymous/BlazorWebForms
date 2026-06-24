# Question Types Reference

Each `FormFieldDefinition` has a `Kind` property (enum `FormFieldKind`) that determines the type of input rendered for that field. This page describes every available question type, its purpose, configurable properties, and the answer format stored in `EntryRecord.Answers`.

---

## Table of Contents

- [Text](#text)
- [TextArea](#textarea)
- [Number](#number)
- [Select](#select)
- [Checkbox](#checkbox)
- [Radio](#radio)
- [Date](#date)
- [File](#file)
- [RichText](#richtext)
- [Signature](#signature)
- [RepeatableList](#repeatablelist)
- [RankedChoice](#rankedchoice)

---

## Text

**Kind:** `FormFieldKind.Text`

Single-line free-text input. The default field kind when no `Kind` is specified.

### Configurable properties

| Property | Type | Description |
|---|---|---|
| `Label` | `string` | Field label displayed to the submitter. |
| `Placeholder` | `string` | Hint text shown inside the input when empty. |
| `HelpText` | `string` | Additional guidance shown below the field. |
| `Required` | `bool` | Whether the field must have a non-empty value on submit. |
| `ReadOnly` | `bool` | Renders the field as non-editable; still included in the answer payload. |
| `RegexPattern` | `string?` | Optional ECMAScript-compatible regex the answer must satisfy. |
| `DefaultValue` | `string?` | Pre-populated value on form load. |
| `ValidationHint` | `string?` | User-facing message shown when regex validation fails. |
| `Searchable` | `bool` | Whether the answer is included in the entry search index. |
| `Prefill` | `FormFieldPrefillDefinition` | Auto-populate from claim, employee profile, or custom provider. |

### Answer format

Plain string. Example: `"Jane Doe"`

### Example definition

```csharp
new FormFieldDefinition
{
	Id = "firstName",
	Kind = FormFieldKind.Text,
	Label = "First name",
	Required = true,
	Searchable = true
}
```

---

## TextArea

**Kind:** `FormFieldKind.TextArea`

Multi-line free-text input. Use for long-form prose, notes, or descriptions.

### Configurable properties

Same as [Text](#text). No additional properties.

### Answer format

Plain string, may contain newlines. Example: `"Line one\nLine two"`

### Example definition

```csharp
new FormFieldDefinition
{
	Id = "notes",
	Kind = FormFieldKind.TextArea,
	Label = "Additional notes",
	Placeholder = "Describe your request in detail..."
}
```

---

## Number

**Kind:** `FormFieldKind.Number`

Numeric input with optional display mode, unit label, and range constraints.

### Configurable properties

| Property | Type | Default | Description |
|---|---|---|---|
| `NumberDisplayKind` | `NumberDisplayKind` | `Plain` | Controls how the value is displayed: `Plain`, `Unit`, or `Percentage`. |
| `NumberUnit` | `string` | `""` | Unit label shown alongside the input when `NumberDisplayKind` is `Unit` (e.g. `"km"`, `"kg"`, `"USD"`). Required when kind is `Unit`. |
| `MinValue` | `decimal?` | `null` | Minimum accepted value. `null` means no lower bound. |
| `MaxValue` | `decimal?` | `null` | Maximum accepted value. `null` means no upper bound. |
| `NumberStep` | `decimal?` | `null` | Increment step for range inputs (e.g. `0.1`, `5`). Must be greater than zero when set. |

All standard field properties (`Label`, `Required`, `Placeholder`, etc.) also apply.

### Display kinds

| Value | Description |
|---|---|
| `Plain` | Bare numeric input with no unit decoration. |
| `Unit` | Numeric input with a unit label appended (requires `NumberUnit`). |
| `Percentage` | Numeric input displayed as a percentage; typically `MinValue = 0`, `MaxValue = 100`. |

### Publish validation

- `NumberDisplayKind.Unit` requires a non-empty `NumberUnit`.
- `MinValue` must not exceed `MaxValue` when both are set.
- `NumberStep` must be greater than zero when set.

### Answer format

Stored as a string representation of the numeric value. Example: `"42.5"`

### Example definitions

```csharp
// Distance with unit
new FormFieldDefinition
{
	Id = "distance",
	Kind = FormFieldKind.Number,
	Label = "Distance",
	NumberDisplayKind = NumberDisplayKind.Unit,
	NumberUnit = "km",
	MinValue = 0m,
	MaxValue = 10000m,
	NumberStep = 0.1m
}

// Percentage completion
new FormFieldDefinition
{
	Id = "completion",
	Kind = FormFieldKind.Number,
	Label = "Completion",
	NumberDisplayKind = NumberDisplayKind.Percentage,
	MinValue = 0m,
	MaxValue = 100m,
	NumberStep = 1m
}
```

---

## Select

**Kind:** `FormFieldKind.Select`

Dropdown single-select. The submitter picks one value from a predefined list.

### Configurable properties

| Property | Type | Description |
|---|---|---|
| `Options` | `List<FormFieldOption>` | The list of choices. At least one option is required at publish. |
| `DefaultValue` | `string?` | Pre-selected option value; must match an option `Value`. |

All standard field properties also apply.

### `FormFieldOption` properties

| Property | Type | Description |
|---|---|---|
| `Value` | `string` | Machine-readable key stored in the answer. Must be unique and non-empty. |
| `Label` | `string` | Human-readable display text. |
| `LocalizedLabels` | `Dictionary<string, string>` | Culture-specific display text. |

### Publish validation

- `Options` must contain at least one entry.
- All option `Value` entries must be non-empty and unique.
- `DefaultValue`, when set, must match one of the option values.

### Answer format

The `Value` string of the selected option. Example: `"approved"`

### Example definition

```csharp
new FormFieldDefinition
{
	Id = "status",
	Kind = FormFieldKind.Select,
	Label = "Status",
	Options =
	[
		new FormFieldOption { Value = "draft", Label = "Draft" },
		new FormFieldOption { Value = "review", Label = "In review" },
		new FormFieldOption { Value = "approved", Label = "Approved" }
	],
	DefaultValue = "draft"
}
```

---

## Checkbox

**Kind:** `FormFieldKind.Checkbox`

Boolean toggle. Renders as a single checkbox.

### Configurable properties

All standard field properties apply. No additional properties.

### Answer format

`"true"` or `"false"` (case-insensitive). Example: `"true"`

### Example definition

```csharp
new FormFieldDefinition
{
	Id = "agreeToTerms",
	Kind = FormFieldKind.Checkbox,
	Label = "I agree to the terms and conditions",
	Required = true
}
```

---

## Radio

**Kind:** `FormFieldKind.Radio`

Single-select rendered as a set of radio buttons. Functionally identical to [Select](#select) but presented inline rather than as a dropdown.

### Configurable properties

Same as [Select](#select), including `Options` and `DefaultValue`.

### Publish validation

Same as [Select](#select).

### Answer format

The `Value` string of the selected option. Example: `"yes"`

### Example definition

```csharp
new FormFieldDefinition
{
	Id = "needsHotel",
	Kind = FormFieldKind.Radio,
	Label = "Hotel needed?",
	Options =
	[
		new FormFieldOption { Value = "yes", Label = "Yes" },
		new FormFieldOption { Value = "no", Label = "No" }
	]
}
```

---

## Date

**Kind:** `FormFieldKind.Date`

Calendar date picker. Stores an ISO 8601 date string.

### Configurable properties

All standard field properties apply. No additional properties.

### Answer format

ISO 8601 date string. Example: `"2026-07-15"`

### Example definition

```csharp
new FormFieldDefinition
{
	Id = "travelDate",
	Kind = FormFieldKind.Date,
	Label = "Travel date",
	Required = true
}
```

---

## File

**Kind:** `FormFieldKind.File`

File upload field. Supports single or multiple file attachments with optional MIME type and extension restrictions.

### Configurable properties

| Property | Type | Default | Description |
|---|---|---|---|
| `MaxFileCount` | `int` | `1` | Maximum number of files that can be uploaded. |
| `MaxFileSizeBytes` | `long?` | `null` | Per-file size limit in bytes. `null` means no limit. |
| `AllowedMimeTypes` | `List<string>` | `[]` | Permitted MIME types (e.g. `"application/pdf"`). Empty means all types allowed. |
| `AllowedExtensions` | `List<string>` | `[]` | Permitted file extensions (e.g. `".pdf"`, `".png"`). Empty means all extensions allowed. |

### Answer format

Comma-separated list of uploaded file names. The actual file bytes are stored separately via `IFileStorage` and linked through `EntryRecord.Files`.

Example: `"receipt.pdf, estimate.xlsx"`

### Example definition

```csharp
new FormFieldDefinition
{
	Id = "attachments",
	Kind = FormFieldKind.File,
	Label = "Supporting documents",
	MaxFileCount = 3,
	MaxFileSizeBytes = 10 * 1024 * 1024, // 10 MB
	AllowedMimeTypes = ["application/pdf", "image/png", "image/jpeg"],
	AllowedExtensions = [".pdf", ".png", ".jpg", ".jpeg"]
}
```

---

## RichText

**Kind:** `FormFieldKind.RichText`

Rich-text editor. Allows submitters to compose formatted content (bold, italic, lists, links, etc.).

### Configurable properties

All standard field properties apply. No additional properties.

### Answer format

HTML string produced by the rich-text editor. Example: `"<p><strong>Important:</strong> Please review.</p>"`

> **Security note:** Always sanitize the stored HTML before rendering it in untrusted contexts.

### Example definition

```csharp
new FormFieldDefinition
{
	Id = "projectDescription",
	Kind = FormFieldKind.RichText,
	Label = "Project description",
	Required = true
}
```

---

## Signature

**Kind:** `FormFieldKind.Signature`

Captures a typed name and a confirmation timestamp, providing a lightweight e-signature.

### Configurable properties

All standard field properties apply. No additional properties.

### Answer format

JSON-serialized `SignatureFieldValue`. Example:

```json
{
  "signerName": "Jane Doe",
  "confirmed": true,
  "signedUtc": "2026-07-15T10:30:00Z"
}
```

### `SignatureFieldValue` model

| Property | Type | Description |
|---|---|---|
| `SignerName` | `string` | The name entered by the signer. |
| `Confirmed` | `bool` | Whether the signer checked the confirmation checkbox. |
| `SignedUtc` | `DateTimeOffset?` | UTC timestamp of when the signature was confirmed. |

### Example definition

```csharp
new FormFieldDefinition
{
	Id = "managerSignature",
	Kind = FormFieldKind.Signature,
	Label = "Manager signature",
	Required = true
}
```

---

## RepeatableList

**Kind:** `FormFieldKind.RepeatableList`

A dynamic list where submitters can add, edit, and remove rows. Each row can be a simple single value **or** a structured set of named attribute columns.

### Configurable properties

| Property | Type | Default | Description |
|---|---|---|---|
| `RepeatableItemLabel` | `string` | `"Item"` | Singular label for each row (e.g. `"Member"`, `"Task"`). |
| `RepeatableAddButtonText` | `string` | `"Add item"` | Text of the button that adds a new row. |
| `MinItems` | `int?` | `null` | Minimum number of rows required. Must be ≥ 0. |
| `MaxItems` | `int?` | `null` | Maximum number of rows allowed. Must be ≥ 1. |
| `RepeatableColumns` | `List<RepeatableListColumnDefinition>` | `[]` | Column definitions for multi-attribute rows. When empty, each row is a plain string value. |

### `RepeatableListColumnDefinition` properties

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Unique column identifier within the field. Must be non-empty and unique across columns. |
| `Label` | `string` | Column header label. Must be non-empty. |
| `Kind` | `RepeatableColumnKind` | Input type for this column. |
| `Placeholder` | `string` | Hint text shown inside the column input when empty. |
| `Required` | `bool` | Whether this column must have a non-empty value in each row. |
| `Options` | `List<FormFieldOption>` | Option list when `Kind` is a future selectable type. |
| `LocalizedLabels` | `Dictionary<string, string>` | Culture-specific column labels. |
| `LocalizedPlaceholders` | `Dictionary<string, string>` | Culture-specific placeholder text. |

### `RepeatableColumnKind` values

| Value | Description |
|---|---|
| `Text` | Single-line text input. |
| `TextArea` | Multi-line text input. |
| `Number` | Numeric input. |
| `Date` | Date picker. |
| `Checkbox` | Boolean toggle. |

### Publish validation

- `MinItems` must be ≥ 0 when set.
- `MaxItems` must be ≥ 1 when set.
- `MinItems` must not exceed `MaxItems` when both are set.
- When `RepeatableColumns` is non-empty: all column `Id` values must be non-empty and unique; all column `Label` values must be non-empty.

### Answer format

**Simple (no columns):** JSON array of strings.

```json
["Task one", "Task two", "Task three"]
```

**Multi-attribute (with columns):** JSON array of objects keyed by column `Id`.

```json
[
  { "memberName": "Alice", "memberRole": "Engineer", "memberHours": "40" },
  { "memberName": "Bob",   "memberRole": "Designer",  "memberHours": "32" }
]
```

### Example definitions

```csharp
// Simple list (no columns)
new FormFieldDefinition
{
	Id = "deliverables",
	Kind = FormFieldKind.RepeatableList,
	Label = "Deliverables",
	RepeatableItemLabel = "Deliverable",
	RepeatableAddButtonText = "Add deliverable",
	MinItems = 1,
	MaxItems = 20
}

// Multi-attribute list
new FormFieldDefinition
{
	Id = "teamMembers",
	Kind = FormFieldKind.RepeatableList,
	Label = "Team members",
	RepeatableItemLabel = "Member",
	RepeatableAddButtonText = "Add member",
	MaxItems = 10,
	RepeatableColumns =
	[
		new RepeatableListColumnDefinition
		{
			Id = "memberName",
			Label = "Name",
			Kind = RepeatableColumnKind.Text,
			Required = true,
			Placeholder = "Full name"
		},
		new RepeatableListColumnDefinition
		{
			Id = "memberRole",
			Label = "Role",
			Kind = RepeatableColumnKind.Text,
			Placeholder = "Job title"
		},
		new RepeatableListColumnDefinition
		{
			Id = "memberHours",
			Label = "Hours per week",
			Kind = RepeatableColumnKind.Number
		}
	]
}
```

---

## RankedChoice

**Kind:** `FormFieldKind.RankedChoice`

Ranked-preference selection. The submitter orders their top **X** choices from a larger pool of options by dragging or selecting items in priority order.

### Configurable properties

| Property | Type | Default | Description |
|---|---|---|---|
| `Options` | `List<FormFieldOption>` | `[]` | The full pool of items to rank. At least two options are required at publish. |
| `RankCount` | `int?` | `null` | How many items the submitter must rank. `null` means rank all options. Must be ≥ 1 and ≤ `Options.Count`. |

All standard field properties also apply.

### Publish validation

- `Options` must contain at least two entries.
- All option `Value` entries must be non-empty and unique.
- `RankCount`, when set, must be ≥ 1 and ≤ `Options.Count`.

### Answer format

JSON array of option `Value` strings in ranked order (index 0 = highest rank).

```json
["blue", "green", "red"]
```

When `RankCount` is set, the array length equals `RankCount`. When `RankCount` is `null`, the array contains all options.

### Example definition

```csharp
new FormFieldDefinition
{
	Id = "topFeatures",
	Kind = FormFieldKind.RankedChoice,
	Label = "Rank your top 3 features",
	Required = true,
	RankCount = 3,
	Options =
	[
		new FormFieldOption { Value = "perf",     Label = "Performance" },
		new FormFieldOption { Value = "ux",       Label = "User experience" },
		new FormFieldOption { Value = "security", Label = "Security" },
		new FormFieldOption { Value = "cost",     Label = "Cost" },
		new FormFieldOption { Value = "support",  Label = "Support" }
	]
}
```

### Reading the answer

```csharp
// entry.Answers["topFeatures"] => """["ux","perf","security"]"""
var ranked = JsonSerializer.Deserialize<string[]>(entry.Answers["topFeatures"]!);
// ranked[0] = "ux"   (1st choice)
// ranked[1] = "perf" (2nd choice)
// ranked[2] = "security" (3rd choice)
```

---

## Common Properties Reference

All field types share the following base properties defined on `FormFieldDefinition`:

| Property | Type | Default | Description |
|---|---|---|---|
| `Id` | `string` | auto | Unique identifier within the form (auto-generated if not set). |
| `Kind` | `FormFieldKind` | `Text` | The question type (see above). |
| `Label` | `string` | `"New field"` | Display label shown to the submitter. |
| `Placeholder` | `string` | `""` | Hint text inside the input. |
| `HelpText` | `string` | `""` | Secondary guidance below the field. |
| `Required` | `bool` | `false` | Whether a non-null answer is mandatory on submit. |
| `Searchable` | `bool` | `false` | Whether the answer is indexed for full-text search. |
| `ReadOnly` | `bool` | `false` | Prevents the submitter from changing the value. |
| `RegexPattern` | `string?` | `null` | Validation regex (ECMAScript-compatible). |
| `DefaultValue` | `string?` | `null` | Pre-populated value. |
| `ValidationHint` | `string?` | `null` | Message shown when regex validation fails. |
| `Prefill` | `FormFieldPrefillDefinition` | — | Auto-populate strategy. |
| `VisibilityRules` | `VisibilityConditionDefinition?` | `null` | Conditional show/hide logic. |
| `Layout` | `FormFieldLayoutDefinition?` | `new()` | Width hint and group for column layout. |

### Localization dictionaries

Every field also carries culture-keyed overrides:

| Property | Overrides |
|---|---|
| `LocalizedLabels` | `Label` |
| `LocalizedPlaceholders` | `Placeholder` |
| `LocalizedHelpTexts` | `HelpText` |
| `LocalizedValidationHints` | `ValidationHint` |

See [CORE_LOCALIZATION.md](CORE_LOCALIZATION.md) for culture resolution and fallback rules.
