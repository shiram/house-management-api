# House Management — AI Development Status

Last updated: 2026-08-28T00:00:00+00:00 — Booking lifecycle and integration validation

## Inspection summary

Performed a focused review of:
- database setup and HouseContext
- User model and authentication DTOs
- Password hashing implementation
- JWT generation and validation
- AuthController endpoints
- Program.cs (DI, JWT, Serilog)
- Serilog configuration and appsettings

Notes below list what is correct, what to preserve, what should change eventually, security issues, architectural risks, and dependencies for next tasks.

## What is already correct

- .NET 8 Web API scaffold and minimal Program.cs startup pattern.
- EF Core DbContext (HouseContext) and Users DbSet exist.
- User model includes expected fields (Id, UserName, Email, PasswordHash, Role, timestamps).
- Authentication DTOs (RegisterRequest, LoginRequest, AuthResponse) present and used.
- PasswordHasher uses PBKDF2 (Rfc2898DeriveBytes) with SHA256, 100k iterations, per-user salt and fixed-time comparison — good practice.
- TokenService issues JWTs with standard claims and role claim; JwtBearer is configured in Program.cs.
- Serilog is configured in appsettings.json and integrated (UseSerilog, UseSerilogRequestLogging).
- Dependency injection registrations for IPasswordHasher and ITokenService are present.

## What should be preserved

- PasswordHasher implementation (do not replace with a weaker approach).
- TokenService claim generation (subject, unique name, email, role) and signing with a symmetric key.
- AuthController endpoints and basic flow (register, login) while tightening validation/authorization around role assignment.
- Serilog configuration and request-logging integration.

## What should eventually change (planned improvements)

- Move all secrets (JWT key, DB passwords) out of committed appsettings into environment/config or secret store; treat appsettings.json as non-secret.
- Use the options pattern (IOptions<T>) for JWT and hashing configuration rather than reading IConfiguration directly in multiple places.
- Enforce production-safe JwtBearer options (RequireHttpsMetadata = true in prod) and validate token parameters (clock skew, token lifetime policy).
- Add refresh-token support and token revocation strategy if long-lived sessions are needed.
- Store hashing parameters/version in config and make iterations/salt length configurable for future upgrades.
- Introduce EF entity configurations, explicit migrations, and DB constraints (unique indexes on Email and UserName) with migration scripts checked into source.
- Replace DateTime with DateTimeOffset for persisted timestamps where the architecture demands it (AGENTS.md prefers DateTimeOffset).
- Harden register endpoint so arbitrary role assignment is not allowed (only admin can set roles).
- Add validation (request body validation), rate limiting on auth endpoints, and account lockout policies for repeated failures.
- Add global exception handling, ProblemDetails responses, request/correlation ID middleware, and integrate correlation ID with Serilog.

## Potential security issues (high priority)

- appsettings.json contains a default JWT key string placeholder; if not overridden via env var this is weak and dangerous.
- Register endpoint allows client-supplied Role — privilege escalation risk. Must validate or restrict to safe defaults.
- RequireHttpsMetadata is set to false — acceptable for local development only; must be true in production.
- No rate limiting, no account lockout, no brute-force protections on login/register.
- No refresh token / revocation mechanism — tokens cannot be revoked until they expire.
- No explicit uniqueness constraints on Users (Email/UserName) at DB level — potential for duplicates/race conditions.
- No validation of password strength or complexity.

## Potential architectural risks

- Project currently uses top-level Controllers/Services layout; ARCHITECTURE.md prefers modular vertical-slices for long-term scalability and parallel work.
- Minimal HouseContext with no entity configurations or migrations increases risk of schema drift and brittle deployments.
- TokenService and PasswordHasher read configuration/parameters directly; moving to options pattern will improve testability and maintainability.
- Using LocalDB in appsettings.json is not suitable for production deployments.
- No authorization policies are defined (only AddAuthorization call) — role policies (Admin/Manager/HouseHelp) need to be codified.

## Dependencies for next tasks

- Add EF Core Migrations, initial migration, and DB deployment guidance (T040/T041).
- Introduce configuration for secrets (env vars, CI secrets, or a secret store) and update README/docs (T003/T005).
- Add validation middleware (FluentValidation optional) and ProblemDetails/global error handling (T010/T014/T015).
- Add request/correlation ID middleware and integrate with Serilog (T012/T013).
- Design and implement role-based authorization policies (T024/T025).
- Add rate limiting and account lockout mechanisms for auth endpoints (T280/T289).

## Recommended immediate actions (small, high-value)

1. Ensure JWT secret is provided via environment (CI/host) and not relying on appsettings default. Document required env vars.
2. Add unique constraints/indexes for Users.Email and Users.UserName via a migration.
3. Disallow client-specified Role in RegisterRequest (or validate allowed values and require admin to create elevated users).
4. Enable RequireHttpsMetadata in production and document development exceptions.
5. Add basic request validation and password complexity checks on RegisterRequest.

---

Update performed by automated inspection task. Do not modify application code in this pass; follow-up tasks should implement the changes above incrementally and include tests/migrations.

Recent actions (2026-08-11T16:15+03:00):
- T002 completed: PROJECT-STRUCTURE.md added documenting repository layout and authentication extension points.
- T003 completed: README.local.md added with local dev instructions.
- T006 completed: Project builds after resolving package version downgrades.
- T010 started/completed (minimal): Added Common.Api.ApiResponse<T> and CommonServiceExtensions.AddCommonServices() and wired into Program.cs to introduce a safe common infra extension point.
- T011 completed: Added ApiResponseFactory and standardized success responses for AuthController and HouseHelpsController to return an envelope with statusCode, message, data and requestId.
- T016 completed: Added ValidationResponseFactory plus ApiBehaviorOptions.InvalidModelStateResponseFactory so invalid requests return the same ApiResponse envelope with field errors.

Next recommended actions: None immediate; T014 and T015 implemented: global exception handling (ExceptionHandlingMiddleware) returning ProblemDetails for unexpected errors. Request/correlation ID middleware (T012) and Serilog integration (T013) implemented.

Follow-ups:
- Harden Register endpoint to disallow client-supplied Role (security) — implemented (self-registration now forces role = 'househelp').
- Create initial business-domain EF migration — added by setup branch and present in src/HouseManagement.Api/Migrations (marking T040 complete).
- Add unique DB constraints and migrations for Users.Email and Users.UserName — configured in HouseContext and covered by migrations.
- Dependency audit completed: the project resolves System.IdentityModel.Tokens.Jwt 7.1.2, plus transitive advisories remain for Azure.Identity, Microsoft.Data.SqlClient, Microsoft.Extensions.Caching.Memory, System.Formats.Asn1, and System.Text.Json; these require a dedicated dependency upgrade review.

Recent actions (2026-08-27):
- T005 completed: README.local.md documents environment variables and CI JWT_KEY requirements.
- T011/T016 completed: global API response and validation filters added.
- T020 completed: EF conventions and migration workflow docs added.
- T021 completed: liveness and database readiness health endpoints added.
- T022 completed: Swagger JWT bearer security configuration added.
- T023 completed: WebApplicationFactory integration test infrastructure added for health, Swagger, and authentication.
- T291 completed: dotnet list package --vulnerable --include-transitive audit executed.
- T328 completed: GitHub Actions build/test workflow added with JWT_KEY secret validation.
- T017 completed: API versioning configured with default version 1.0, backward-compatible unversioned routes, and API version response headers.
- T018 completed: documented UTC and DateTimeOffset conventions; new persisted timestamps use DateTimeOffset.UtcNow while legacy User timestamps remain unchanged for schema compatibility.
- T019 completed: moved entity mappings into dedicated IEntityTypeConfiguration classes and enabled assembly scanning from HouseContext.
- T024 completed: added named AdminOnly, ManagerOrAdmin, and HouseHelpOnly authorization policies while preserving existing lowercase JWT role values.
- Follow-up completed: aligned HouseHelp controller authorization attributes with the registered ManagerOrAdmin policy and corrected typed API envelope detection; the full backend suite now passes 35 tests.
- T031 completed: added the Service domain model and explicit EF configuration with code/name metadata, decimal pricing, active state, timestamps, uniqueness, and operational indexing. A future domain migration is still required before deployment.
- T032-T039 completed: added anonymous-capable Client, Booking and BookingStatus models, weekly HouseHelp availability, ServiceAddress, valid booking transitions, explicit relationship configurations, business uniqueness constraints, and operational indexes. A domain migration is required before these new tables are deployed.
- T050 completed: added feature-specific service response, create, and update DTOs.

Recent actions (2026-08-30):
- T286 completed: reviewed all protected controller actions. AdminOnly covers users/settings/audit logs; ManagerOrAdmin covers service, HouseHelp, booking, and manager-availability operations; HouseHelpOnly covers assigned bookings and self-availability; notifications and client bookings are authenticated owner-scoped. No authorization-semantic changes were required.
- T285 completed: documented mandatory sensitive-data logging rules in `docs/SECURITY.md`. Passwords, hashes, tokens, authorization headers, keys, connection strings, contact data, addresses, and system-setting values must not be written to logs or audit details; request/response bodies remain excluded by default.
- T284 completed: AdminOnly user role/status changes and system-setting upserts now create audit events with JWT-derived actors. User audits record prior-to-next role/status values; system-setting audits intentionally omit setting keys and values to avoid recording configuration data.
- T283 completed: successful HouseHelp assignment now writes a `booking.assigned` audit event before the existing assignment transaction commits. The audit record identifies the booking, JWT-derived assigning actor when present, and assigned HouseHelp ID without recording contact data.
- T282 completed: every successful booking status transition now records a `booking.status_changed` audit event with the booking ID, JWT-derived Manager/Admin actor ID when present, and the non-sensitive prior-to-next status transition. Failed validation and missing-booking requests do not produce audit records.
- T281 completed: successful and failed login attempts now write `authentication.login_succeeded` and `authentication.login_failed` audit events. Successful events identify the authenticated user; failures deliberately contain no account identifier, credentials, email address, or token.
- T280 completed: the persisted audit event model was already established by T249 (`AuditLog`, configuration, and `IAuditLogService`). Added `AuditEventTypes` as the canonical stable action taxonomy for authentication, booking, and user-administration audit wiring in T281-T284.
- T270 completed: added end-to-end coverage verifying an anonymous booking request reaches an active Manager's authenticated notification feed with the correct `booking.created` type and booking link. This complements focused notification service/API and booking-trigger tests from T260-T269.
- T269 completed: added provider-neutral email and SMS notification contracts under `Infrastructure/Notifications`. `IEmailNotificationSender` and `ISmsNotificationSender` accept typed message records and cancellation tokens; no provider package, credentials, implementation, or delivery wiring is hard-coded.
- T267 completed: added owner-scoped notification read state: `PATCH /api/notifications/me/{id}/read` marks a notification read idempotently, and `GET /api/notifications/me/unread-count` returns the current user's unread total. Both use the authenticated user claim and return not found for another user's notification.
- T266 completed: linked registered clients now receive `booking.status_changed` notifications for successful non-confirmation status transitions and assignment. Confirmation continues to use its dedicated `booking.confirmed` notification from T264, preventing duplicate notices.
- T265 completed: assigning a booking now creates an in-app `booking.assigned` notification for the linked HouseHelp user, before the existing assignment transaction commits. Unlinked HouseHelp profiles retain existing assignment behavior without an in-app recipient.
- T264 completed: confirming a booking now creates an in-app `booking.confirmed` notification for its linked registered client. Anonymous bookings have no authenticated notification recipient, so confirmation remains unchanged for them.
- T263 completed: anonymous booking creation now creates an in-app `booking.created` notification for every active Manager account, linked to the new booking. Inactive managers and users in other roles are excluded.
- T262 completed: added `INotificationService`/`NotificationService` (create + own-user-scoped list/get with unread filter and pagination) and an authenticated `NotificationsController` (`GET /api/notifications/me`, `GET /api/notifications/me/{id}`), claim-derived like the existing `/api/bookings/mine`. `CreateAsync` has no public HTTP endpoint by design — it's meant to be called from other feature services (bookings, assignment) once T263-T266 wire up the actual triggers. Mark-as-read/unread-count endpoints are deliberately deferred to T267.
- T261 completed: added `NotificationTypes` (Common/NotificationTypes.cs) defining the canonical string values for `Notification.Type` — `booking.created` (Manager/Admin, T263), `booking.confirmed` (client, T264), `booking.assigned` (HouseHelp, T265), and `booking.status_changed` (client, general status changes, T266) — plus an `All` set for future validation, following the same string-constants convention as `Roles`. Tests added confirming uniqueness/non-empty values. No wiring into booking/status-change flows yet; that begins at T262 (notification infrastructure) and continues through T263-T266.
- T260 completed: added the `Notification` foundation model (UserId recipient, Type, Title, Message, optional RelatedEntityType/RelatedEntityId, IsRead/ReadAt, CreatedAt) with EF configuration/indexes and a persistence test. `Type` is a plain string placeholder deliberately left unconstrained here, since T261 (next) defines the concrete set of supported notification type values. No service/controller/migration yet — those are T262+ and out of scope for the model-definition task.
- T250 completed: added reflection-based authorization tests (`AdminAuthorizationTests`) confirming `AdministrationController`, `SystemSettingsController`, and `AuditLogsController` all require the `AdminOnly` policy (checking method-level attributes, falling back to the class-level attribute where a controller doesn't repeat `[Authorize]` per action), plus a companion check that `AdminServicesController`/`AdminHouseHelpsController` are correctly `ManagerOrAdmin` (not accidentally anonymous or Admin-only). This completes Phase 12 (Administration, T241-T250).
- T249 completed: added the `AuditLog` model (Action, EntityType, EntityId, UserId, Details, CreatedAt) with EF configuration/indexes, an `IAuditLogService` (`LogAsync` write + filterable/paginated `GetListAsync` read), and an Admin-only `GET /api/admin/audit-logs` endpoint (filters: action, entityType, userId). This is foundation-only, as scoped: no existing controllers were wired to call `LogAsync` yet — actually recording specific domain events (auth, booking status changes, administrative changes) is explicitly deferred to T281/T282/T284 in Phase 14. No EF migration generated yet (same precedent as T091/T248).
- T248 completed: added the `SystemSetting` key-value model (unique `Key`, `Value`, optional `Description`, `UpdatedAt`/`UpdatedByUserId` audit fields) with EF configuration, plus Admin-only `GET /api/admin/settings`, `GET /api/admin/settings/{key}`, and `PUT /api/admin/settings/{key}` (upsert) via a new `ISystemSettingsService`. Scoped to Admin-only (not Manager) per ARCHITECTURE.md's "system configuration" being an Admin responsibility. No EF migration was generated yet — consistent with the existing precedent from T091 (HouseHelp availability exceptions), a migration is required before this reaches a real SQL Server environment; tests use the InMemory provider.
- T247 completed: extended the existing Manager/Admin `GET /api/bookings` listing with `houseHelpId` and `clientId` filters (alongside the existing `status` filter and pagination) via `IBookingService.GetListAsync`, giving administration visibility into a booking's assignment/client without adding new endpoints, since booking read/lifecycle management (`GET /{id}`, assign/cancel/reject/confirm/complete) was already Manager/Admin-protected from earlier phases. Added an integration test covering both new filters.
- T246 completed: added Manager/Admin-only `GET /api/admin/househelps` (with city/skill/isActive/userId filters and pagination) and `GET /api/admin/househelps/{id}` (`AdminHouseHelpsController`), plus a `userId` filter added to `IHouseHelpService.GetFilteredAsync`. Scoped narrowly per explicit user decision: the pre-existing public `/api/househelps` endpoints (which already expose full details/inactive profiles without authentication) were left untouched — flagged as a latent data-exposure gap for a future security review task (T287), not fixed here.
- T245 completed: added Manager/Admin-only `GET /api/admin/services` (paginated, includes inactive services) and `GET /api/admin/services/{id}` (`AdminServicesController`), plus `IServiceCatalogService.GetAllAsync` to support full-catalog administration without changing the public active-only endpoints. Integration tests cover inactive-service visibility for managers, unauthenticated/non-manager rejection, and not-found handling.
- T244 completed: added Admin-only `PUT /api/admin/users/{id}/activate?active=bool` endpoint to activate/deactivate a user, protected against an admin deactivating their own account. Also hardened `AuthController.Login` to reject login for deactivated (`IsActive == false`) accounts — this was a pre-existing gap tightly coupled to T244's purpose (deactivation had no effect on login without it) and was scoped narrowly to a single boolean check with no changes to token issuance, hashing, or claims. Added integration tests for admin success, self-deactivation rejection, not-found, non-admin rejection, and a unit test confirming deactivated accounts cannot log in.
- T243 completed: added Admin-only `PUT /api/admin/users/{id}/role` role management endpoint with allowed-role validation, protection against an admin demoting their own account, and integration tests for success, invalid role, self-demotion, and non-admin rejection cases.
- T242 completed: added Admin-only `GET /api/admin/users/{id}` user details endpoint with not-found handling, plus integration tests for admin success, non-admin rejection, and missing-user cases.
- T241 completed: added an Admin-only `GET /api/admin/users` endpoint (`AdministrationController`) with a feature-specific `UserDto`, pagination support, and integration tests covering unauthenticated, non-admin, and admin access.
- Fixed flaky T140 concurrency regression test: added an in-process per-HouseHelp semaphore in `BookingService.AssignHouseHelpAsync` to serialize concurrent assignment attempts for the same househelp, complementing the existing database-level serializable transaction (the EF InMemory provider used in tests does not enforce real transaction isolation).
- T090 completed: the weekly availability model is implemented and validated through `HouseHelpAvailability`, `HouseHelpAvailabilityException`, the availability service, and the existing availability test suite.
- T211 completed: manager-facing endpoints are now covered by authorization reflection tests to ensure management operations require the `ManagerOrAdmin` policy on HouseHelp, Availability, and Booking actions.
- T228 completed: HouseHelp-only endpoints are protected by authorization reflection tests to ensure self-service availability and assigned-booking views require the `HouseHelpOnly` policy.
- T130-T139 completed: assignment workflow, validation guards, transaction safety, audit metadata, and integration test coverage are all in place.
- T140 completed: added a concurrent assignment regression test that verifies only one request can assign the same househelp to overlapping confirmed bookings under parallel execution.
- T051 completed: added DataAnnotations validation for service code, name, description, and non-negative decimal pricing, with focused tests.
- T052 completed: added the public `GET /api/services` endpoint, active-service filtering, deterministic ordering, DI registration, and coverage for inactive services.
- T053 completed: added public `GET /api/services/{id}` details with active-service filtering and not-found behavior.
- T054 completed: added Manager/Admin-protected service creation with normalization and duplicate-code conflict handling.
- T055 completed: added Manager/Admin-protected service updates with inactive-service support, normalization, duplicate-code checks, and update timestamps.
- T056 completed: added Manager/Admin-protected service activation/deactivation with status timestamps and not-found handling.
- T057 completed: added authorization tests confirming public reads and Manager/Admin-only service mutations.
- T058 completed: added integration coverage for public service reads, Manager/Admin lifecycle operations, and inactive-service hiding.
- Frontend tasks were removed from `TASKS.md` per user request; the queue now retains backend-focused work only.
- T042 completed: added development-only, idempotent role user seeding controlled by `DEV_SEED_PASSWORD` and documented the local setup.
- T043 completed: added explicit-enable, development-only, idempotent sample service seeding and documented the required configuration.
- T044 completed: added development-only, idempotent HouseHelp profile and skill seeding without creating authentication accounts.
- T041 completed: verified `HouseManagerDB` contains both migrations and the new Services, Clients, Bookings, ServiceAddresses, and HouseHelpAvailabilities tables with expected indexes and unique constraints.
- T091 completed: added HouseHelp unavailability/leave exception model, EF configuration, relationship, and range-query indexes. A follow-up migration is required before deployment.
- T092 completed: added public availability querying for active weekly slots and date-overlapping active exceptions, with validation and tests.
- T093 completed: added Manager/Admin-protected weekly availability replacement at `PUT /api/househelps/{houseHelpId}/availability`, including clear/replace behavior and tests.
- T094 completed: added HouseHelp-only `PUT /api/availability/me`, deriving the linked profile from the JWT subject claim.
- T095 completed: added server-side validation preventing overlapping or zero-length weekly availability slots.
- T096 completed: added integration coverage for public availability reads, Manager/Admin updates, overlap rejection, and unauthorized mutations.
- T100 completed: added shared booking create and response DTOs, including scheduling, service, address, status, and notes fields.
- T101 completed: added anonymous booking request DTO with contact name, phone, and optional email fields.
- T102 completed: added authenticated booking request DTO without client or user identifiers, preserving claim-derived identity.
- T103-T109 completed: added the public anonymous booking endpoint with active-service, schedule, contact, and address validation, transactional persistence, and unique booking reference generation.
- T110 completed: added Manager/Admin-protected `GET /api/bookings/{id}` details retrieval with service and address loading.
- T111 completed: added Manager/Admin-protected `GET /api/bookings` with optional status filtering, deterministic newest-first ordering, and bounded pagination.
- T115 completed: added the booking status transition service with persistence, timestamps, not-found handling, and transition-map enforcement.
- T116 completed: added explicit booking transition validation for undefined, same-status, and disallowed transitions.
- T117 completed: added Manager/Admin booking cancellation through `POST /api/bookings/{id}/cancel`, enforcing cancellable statuses and returning the updated booking.
- T118 completed: added Manager/Admin booking rejection through `POST /api/bookings/{id}/reject`, allowing rejection only from the requested state.
- T119 completed: added Manager/Admin booking confirmation through `POST /api/bookings/{id}/confirm`, allowing confirmation only from the requested state.
- T120 completed: added Manager/Admin booking completion through `POST /api/bookings/{id}/complete`, allowing completion only from the in-progress state.
- T112 completed: added HouseHelp-only `GET /api/bookings/assigned/me` for assigned booking lists and claim-derived user resolution.
- T113 completed: added authenticated client `GET /api/bookings/mine` and tied it to the current user’s client record.
- T114 completed: added anonymous `GET /api/bookings/track/{reference}` to safely look up booking status by tracking reference without exposing internal data.
- T130 completed: added the eligible `HouseHelpService.GetEligibleAsync` query, filtering to active househelps with matching service skills and optional city, to support the upcoming assignment workflow without exposing assignment logic yet.
- T131 completed: added the manager/admin assignment endpoint at `POST /api/bookings/{id}/assign`, including booking confirmation gating, househelp eligibility checks, availability enforcement, and double-booking protection before the booking is moved to the `Assigned` state.
- T132 completed: added explicit inactive-househelp validation to the assignment flow and a booking integration test covering the rejection path for inactive selectors.
- T133 completed: added service-skill validation to the assignment flow and a booking integration test covering the rejection path when a househelp is not trained for the requested service.
- T134 completed: added availability-window validation to the assignment flow and a booking integration test covering a househelp who is otherwise eligible but unavailable during the requested schedule.
- T135 completed: added explicit overlapping-booking detection to the assignment path and an integration test covering a conflicting assignment attempt for an already-booked househelp.
- T136 completed: wrapped assignment updates in a relational database transaction with a serializable isolation level so the househelp assignment check-and-save sequence runs atomically and reduces race conditions during concurrent assignment requests.
- T137 completed: added a double-booking regression test to confirm a second overlapping assignment for the same househelp is rejected once the first assignment has been accepted, enforcing the assignment safety rule under the application’s serializable transaction path.
- T138 completed: recorded assignment audit metadata (`AssignedByUserId` and `AssignedAt`) on bookings when a manager/admin assigns a househelp and exposed those values on the booking DTO so assignment history remains traceable.
- T139 completed: added explicit integration coverage for the assignment workflow, including successful assignment, inactive househelp rejection, missing service-skill rejection, availability-window rejection, overlap rejection, and assignment audit validation.
- T121 completed: added booking integration coverage for anonymous booking creation, manager/admin access, in-progress completion, assigned HouseHelp access, authenticated client access, and anonymous tracking through the public API, all passing under the existing app/test configuration.
- T287 completed: public HouseHelp directory and detail responses now return only active profiles through a dedicated safe DTO (name, city, and skills), excluding linked user IDs, contact details, addresses, and active-state fields. Full operational data remains Manager/Admin-only under `/api/admin/househelps`.
- T288 completed: confirmed no current file-upload surface and documented mandatory boundaries for planned profile images: allow-listed image formats, server-side size/signature/decoding validation, opaque storage names, metadata stripping, storage outside public paths, claim-derived authorization, and public DTO redaction. Added Phase 18 to scope future HouseHelp profile and media work.
