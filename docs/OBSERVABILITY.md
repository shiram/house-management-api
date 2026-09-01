# Observability

## Structured logging review (T308)

Reviewed on 2026-09-01. Serilog is configured as the application logging provider with console and rolling-file sinks, a stable application property, machine/thread enrichment, and request logging. `RequestIdMiddleware` establishes or propagates `X-Request-Id` before request logging and stores it in Serilog's `LogContext`; unhandled exceptions log this identifier for correlation.

The current logs use message templates and do not log request/response bodies. Authentication failures deliberately exclude account identifiers and credentials. Development seeding logs only operation outcomes.

### Required pattern for new operational events

Use structured message templates with stable, allow-listed properties:

```text
Booking assignment completed for {BookingId} and {HouseHelpId} by {ActorUserId}
```

Allowed properties are opaque entity IDs, safe status values, event/action names, request IDs, timing/duration values, and non-sensitive result counts. Use `Information` for completed business operations, `Warning` for expected rejected operations or degraded dependencies, and `Error` with the exception and request ID for unexpected failures.

Never interpolate arbitrary client input into a message template and never log contact details, addresses, profile notes, tokens, credentials, setting values, client filenames, storage paths, or provider payloads. Keep the logging policy in `SECURITY.md` authoritative for sensitive-data rules.

### Follow-up

Current domain services rely primarily on audit records rather than application logs. Add structured completion/rejection events only when a concrete operational-support requirement exists, starting with high-volume booking operations. Ensure every new event has an integration or unit test only when it affects observable application behavior; do not test sink implementation details.

## Health and readiness checks (T309)

- `GET /health/live` returns success when the API process can handle requests. It has no database or external dependency checks and is suitable for liveness probes.
- `GET /health/ready` runs the registered `database` health check and returns success only when EF Core can connect to the configured database. It is suitable for readiness probes.

Both endpoints are intentionally anonymous so platform probes do not require credentials. Keep liveness dependency-free; add only required startup dependencies to readiness, with a focused test for each new check.
