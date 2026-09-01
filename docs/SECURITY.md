# Security Guidelines

## Sensitive-data logging

Never write these values to Serilog, audit-log `Details`, exception messages, API responses, or external notification provider logs:

- Passwords, password hashes, password-reset values, or verification codes.
- JWTs, bearer authorization headers, refresh tokens, API keys, connection strings, or other secrets.
- Full payment data, government identifiers, or authentication claims beyond the internal user ID needed for auditing.
- System-setting values unless they have been explicitly classified as non-sensitive.

When logging authentication failures, use a generic event with no email address, username, password, token, or account ID. Audit successful authenticated actions using stable action names, entity IDs, and the actor's internal user ID only. Record only the minimum non-sensitive business context required to understand an operational change.

Do not log HTTP request or response bodies by default. Before adding a new structured log field or audit detail, verify it does not contain sensitive data; prefer an opaque internal ID or an allow-listed status value over client-provided text.

## Review checklist

- Confirm log and audit details exclude credentials, tokens, contact information, addresses, and system-setting values.
- Confirm exceptions returned to clients do not expose internal details in production.
- Confirm new integrations redact provider credentials and recipient contact data from logs.
