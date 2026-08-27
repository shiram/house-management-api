# House Management — AI Development Status

Last updated: 2026-08-11T13:07:45+03:00 — Authentication & infra inspection

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
