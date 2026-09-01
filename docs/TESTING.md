# Testing Baseline

## Unit coverage review (T300)

Measured on 2026-09-01 with:

```text
dotnet test src\HouseManagement.Api.Tests\HouseManagement.Api.Tests.csproj --no-restore --filter "FullyQualifiedName!~Integration" --collect:"XPlat Code Coverage"
```

The unit-focused suite contains 107 passing tests and covers 1,206 of 3,935 lines (30.64%) and 176 of 578 branches (30.44%). No coverage threshold is currently enforced. The measurement includes controllers, DTOs, models, middleware, and generated async state machines, so it must not be used alone as a measure of business-rule coverage.

The full 173-test suite covers 2,325 of 3,935 lines (59.08%) and 394 of 578 branches (68.16%). T301 will assess that integration coverage independently.

### Well-covered business rules

- Booking state transitions, availability replacement and overlap validation.
- Anonymous booking validation, HouseHelp assignment eligibility, and assignment conflict protection.
- Audit logging, notification read-state ownership, and service catalog mutations.
- Token generation and password hashing behavior.

### Prioritized unit-test gaps

1. Add unit coverage for booking lookup and list query paths, including client and HouseHelp ownership filtering.
2. Add unit coverage for HouseHelp and service catalog read/filter query paths, including inactive and pagination cases.
3. Add unit coverage for notification pagination and unread filtering.
4. Add branch coverage for malformed password hashes and remaining booking-assignment rejection paths.

Controller routing, CORS, headers, and authorization behavior are deliberately covered by integration tests because their behavior depends on the ASP.NET Core pipeline. New domain features must add unit tests for business rules and failure paths rather than relying only on line coverage.

## Integration coverage review (T301)

Measured on 2026-09-01 with:

```text
dotnet test src\HouseManagement.Api.Tests\HouseManagement.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~Integration" --collect:"XPlat Code Coverage"
```

The HTTP integration suite contains 66 passing tests and covers 2,029 of 3,935 lines (51.56%) and 324 of 578 branches (56.05%). This is a focused subset, not an additive percentage: its coverage overlaps the unit suite and the full-suite baseline remains the authoritative aggregate.

### Covered API behavior

- Registration and login, role/ownership enforcement, administration, audit logs, system settings, services, HouseHelps, availability, notifications, and public booking creation/tracking.
- Booking assignment eligibility and conflict failure paths, public-endpoint rate limiting, public HouseHelp redaction, configured CORS behavior, and browser security headers.

### Prioritized integration-test gaps

1. Add HTTP coverage for booking cancellation and rejection, including invalid transitions and audit/notification effects.
2. Add HTTP coverage for HouseHelp self-service availability replacement and Manager/Admin HouseHelp profile updates.
3. Add production-safe unhandled-exception response coverage to confirm no internal details are returned.
4. Add a separately provisioned SQL Server integration suite before relying on relational constraints, transactions, and concurrency behavior; the current integration factories use EF Core's in-memory provider.

Future profile-image work must add multipart upload, ownership, rejected-content, replacement, and public-redaction integration coverage before the feature is released.
