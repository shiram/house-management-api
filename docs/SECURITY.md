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

## Authorization policy matrix

Protected endpoints use these centralized policies:

- `AdminOnly`: user administration, system settings, and audit-log access.
- `ManagerOrAdmin`: service and HouseHelp management, booking management and lifecycle actions, and manager availability management.
- `HouseHelpOnly`: a HouseHelp's assigned bookings and self-service availability management.
- Authenticated owner scope: notification reads and updates, plus a client's own booking list.

Controllers must derive the authenticated user ID from JWT claims for owner-scoped behavior. Public endpoints must be explicitly intended for anonymous use and are reviewed separately for data leakage.

## Public booking rate limits

Anonymous booking submission is limited per direct client IP to five requests per 60-second fixed window. Anonymous booking tracking is limited independently to 30 requests per 60-second fixed window. Limits are configured in `RateLimiting:PublicBooking`; rejected requests receive `429 Too Many Requests`, an API response envelope, and a `Retry-After` header.

The API intentionally uses the direct connection IP and does not trust forwarded-client headers. Deployments behind a reverse proxy must configure trusted proxy handling before relying on forwarded addresses for rate-limit partitioning.

## Profile image uploads

No upload endpoint exists yet. Before profile-image uploads are implemented, use a provider-neutral storage abstraction and keep uploaded files outside the application content root and direct static-file serving paths.

- Accept only JPEG, PNG, and WebP profile images. Reject SVG, GIF, PDFs, archives, and all other file types.
- Enforce a configurable maximum upload size with a server-side hard cap, validate file signatures and decodability rather than trusting the extension or declared MIME type, and constrain decoded image dimensions.
- Generate opaque server-side storage names; never use a client-provided filename or path. Store only an image reference in the database.
- Re-encode accepted images and strip EXIF metadata before storage so embedded location and device data cannot be exposed.
- Authorize every upload, replacement, deletion, and private-image retrieval using the authenticated subject and the linked HouseHelp profile. Managers/Admins may be granted operational access only through an explicit authorization rule.
- Do not expose exact locations, profile notes, source filenames, storage paths, or image metadata in public HouseHelp DTOs. Public image delivery, if approved, must use a safe projected image reference only.
- Do not log file contents, image metadata, client filenames, storage paths, or full profile locations. Clean up superseded media only after a successful replacement is persisted.
