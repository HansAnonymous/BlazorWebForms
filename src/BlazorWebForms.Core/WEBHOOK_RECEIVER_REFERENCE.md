# BlazorWebForms - Webhook Receiver Reference

BlazorWebForms delivers webhook payloads as **HTTP POST** requests with a `Content-Type: application/json` header. This document describes the payload structure, security model, and delivery behaviour.

---

## Delivery Behaviour

| Property | Detail |
|---|---|
| Method | `POST` |
| Content-Type | `application/json` |
| Timeout | 10 seconds per attempt |
| Retries | Up to 3 attempts with exponential back-off (2s, 4s, 8s) |
| Failure handling | Failures are logged but do not affect the form submission lifecycle |

---

## Security - HMAC-SHA256 Signature

If a **Secret** is configured on the webhook definition, every request will include a signature header:

```
X-BlazorWebForms-Signature: sha256=<hex-digest>
```

The digest is computed over the **raw UTF-8 request body** using HMAC-SHA256 with the configured secret. Receivers should verify this before processing.

### Verification example (C#)

```csharp
using System.Security.Cryptography;
using System.Text;

bool IsValidSignature(string rawBody, string signatureHeader, string secret)
{
	if (!signatureHeader.StartsWith("sha256=")) return false;
	var receivedHex = signatureHeader["sha256=".Length..];

	var key = Encoding.UTF8.GetBytes(secret);
	var data = Encoding.UTF8.GetBytes(rawBody);
	var hash = HMACSHA256.HashData(key, data);
	var computedHex = Convert.ToHexString(hash).ToLowerInvariant();

	return CryptographicOperations.FixedTimeEquals(
		Encoding.UTF8.GetBytes(computedHex),
		Encoding.UTF8.GetBytes(receivedHex));
}
```

### Verification example (Node.js)

```js
const crypto = require('crypto');

function isValidSignature(rawBody, signatureHeader, secret) {
	const expected = 'sha256=' + crypto
		.createHmac('sha256', secret)
		.update(rawBody, 'utf8')
		.digest('hex');
	return crypto.timingSafeEqual(
		Buffer.from(expected),
		Buffer.from(signatureHeader));
}
```

---

## Standard Request Headers

Every webhook request includes the following headers in addition to any custom headers configured on the definition:

| Header | Value |
|---|---|
| `Content-Type` | `application/json` |
| `X-BlazorWebForms-Event` | The trigger event name (see below) |
| `X-BlazorWebForms-FormId` | GUID of the form |
| `X-BlazorWebForms-EntryId` | GUID of the entry |
| `X-BlazorWebForms-Delivery` | A unique GUID for this delivery attempt |
| `X-BlazorWebForms-Signature` | `sha256=<hex>` - only present when a secret is configured |

---

## Trigger Events

| `X-BlazorWebForms-Event` | When it fires |
|---|---|
| `EntrySubmitted` | A form entry is submitted (or transitions from Draft → Submitted / NeedsApproval) |
| `EntryApproved` | All approval steps are completed and the entry reaches `Approved` status |
| `EntryRejected` | An approver rejects a step, moving the entry to `Rejected` |
| `StepApproved` | An individual approval step is approved (fires before `EntryApproved`) |
| `StepRejected` | An individual approval step is rejected |
| `StepDelegated` | An approval step is delegated to a different person |
| `EntryResubmitted` | A previously rejected entry is resubmitted by the submitter |

---

## Payload Schema

All events share the same top-level envelope. The `entry` and `step` objects are populated according to the event type.

```jsonc
{
  // ── Envelope ──────────────────────────────────────────────────────────────
  "event": "EntrySubmitted",           // WebhookTriggerEvent name
  "deliveryId": "3fa85f64-...",        // Unique per delivery attempt
  "occurredUtc": "2026-06-01T10:30:00Z",

  // ── Form ──────────────────────────────────────────────────────────────────
  "form": {
	"id": "3fa85f64-...",
	"key": "employee-onboarding",
	"name": "Employee Onboarding",
	"description": "..."
  },

  // ── Entry ─────────────────────────────────────────────────────────────────
  "entry": {
	"id": "7c9e6679-...",
	"status": "Submitted",            // EntryStatus: Draft | Submitted | NeedsApproval | Approved | Rejected
	"submittedBy": "Jane Smith",
	"submittedByEmail": "jane@example.com",
	"submittedUtc": "2026-06-01T10:30:00Z",

	// Key/value map of field ID → answer value.
	// File fields contain a reference token, not the file content.
	"answers": {
	  "field-abc123": "Jane",
	  "field-def456": "Engineering",
	  "field-ghi789": "file-ref:upload/2026/06/abc.pdf"
	}
  },

  // ── Approval step (only present for Step* and Entry*Approved/Rejected events) ──
  "step": {
	"id": "8d9e8f00-...",
	"order": 1,
	"approverEmail": "manager@example.com",
	"approverName": "Bob Manager",
	"acceptorMode": "Single",         // Single | AnyOf
	"status": "Approved",             // Pending | Approved | Rejected
	"signature": "I approve this.",
	"rejectionReason": null,
	"completedUtc": "2026-06-01T11:00:00Z",

	// Delegation info — present only when the step was delegated
	"delegation": {
	  "delegatedToEmail": "deputy@example.com",
	  "delegatedToName": "Alice Deputy",
	  "delegatedUtc": "2026-06-01T10:45:00Z"
	}
  }
}
```

### Notes

- `step` is `null` for `EntrySubmitted` and `EntryResubmitted` events.
- Answer values are always strings. Multi-select fields encode their values as a comma-separated string. Signature fields encode a JSON object as a string.
- File fields contain a reference token in the form `file-ref:<relativePath>`. Receivers that need file content should call the BlazorWebForms file download API using the entry ID and file ID.

---

## Responding to a Webhook

Receivers **must** return an HTTP `2xx` status code within the timeout window. Any non-2xx response or a network timeout is treated as a failure and triggers the retry schedule. Receivers should respond quickly and process payloads asynchronously.

---

## Testing Webhooks

During development, tools such as [webhook.site](https://webhook.site) or [smee.io](https://smee.io) can be used to inspect live payloads without deploying a receiver endpoint.
