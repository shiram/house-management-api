# House Management API — AI Engineering Instructions

## 1. Mission

You are an engineering agent working on the House Management platform.

The product has two clients:

1. An Angular web application used by managers/admins/househelps.
2. A client-facing application where customers can request house cleaning or laundry services.

A client may submit a service request without signing in.

The platform manages:

- househelp directory
- househelp profiles
- service offerings
- availability
- service requests/bookings
- client/customer information
- assignment of househelps to bookings
- manager operations
- administration
- authentication and authorization

Roles:

- `Admin`
- `Manager`
- `HouseHelp`

Do not treat anonymous clients as an authenticated application role.

---

## 2. Existing implementation — DO NOT REBUILD IT

The current backend already contains initial work for:

- SQL Server database setup
- Entity Framework Core / `HouseContext`
- Serilog
- User model and authentication DTOs
- JWT token service
- password hashing
- authentication controller

Before changing any of these, inspect the existing implementation and preserve working behavior.

Do not replace the existing authentication implementation just because another pattern looks cleaner.

Make incremental changes.

---

## 3. Architecture

Use a **modular monolith with vertical slices**.

Do NOT start with microservices.

The application is expected to remain one deployable ASP.NET Core API while business boundaries are kept clear enough that a future extraction is possible.

Preferred backend shape:

```text
src/
  HouseManagement.Api/
    Common/
    Infrastructure/
    Features/
      Authentication/
      HouseHelps/
      Services/
      Bookings/
      Clients/
      Availability/
      Administration/
      Notifications/
```

Within each feature, keep related request/handler/validation/DTO logic close together.

Do not create generic repositories or generic services unless there is a demonstrated need.

Use EF Core directly in the feature/application layer where appropriate.

Keep controllers thin.

Preferred flow:

```text
HTTP
  -> Controller / Endpoint
  -> Feature application logic
  -> EF Core / infrastructure
  -> SQL Server
```

---

## 4. Backend technology standards

Target:

- .NET 8
- ASP.NET Core Web API
- EF Core
- SQL Server
- JWT authentication
- Serilog

Prefer:

- async/await
- cancellation tokens for I/O
- `DateTimeOffset` for timestamps
- UTC for persisted timestamps
- `decimal` for monetary values
- database constraints and indexes for important invariants
- nullable reference types
- dependency injection
- options pattern for configuration
- ProblemDetails for unexpected API errors
- consistent API response contracts
- FluentValidation if it is introduced; do not add duplicate validation frameworks

Do not use:

- synchronous EF calls in request paths
- static mutable state
- secrets committed to source control
- `DateTime.Now` for persisted business timestamps
- `float`/`double` for money

---

## 5. Domain principles

A househelp is a managed worker/profile.

A service is something a client can request, such as:

- house cleaning
- laundry

A booking/service request represents a customer's request for a service.

A booking may be:

- anonymous
- associated with a registered user/client
- unassigned
- assigned to a househelp
- pending
- confirmed
- in progress
- completed
- cancelled
- rejected

Do not assume a client must have an account.

Anonymous requests must capture the minimum contact information required to fulfil the request.

Househelp assignment must protect against double booking.

Availability and booking conflicts are business rules, not merely UI checks.

---

## 6. Authorization

Use role-based authorization for management operations.

Typical boundaries:

### Admin

Can manage:

- users
- roles
- system configuration
- househelps
- service definitions
- operational data
- audit data

### Manager

Can manage:

- househelp directory
- househelp availability
- bookings
- assignments
- operational status
- service requests

### HouseHelp

Can:

- view own profile
- manage permitted availability
- view assigned bookings
- update allowed booking/work status

### Anonymous client

Can:

- browse public services
- browse eligible househelps where business rules permit
- submit a service request
- query a request using a safe tracking mechanism if that feature is enabled

Never expose internal user data to anonymous clients.

Never trust role/user IDs sent by the browser. Derive authenticated identity from claims.

---

## 7. API design

Use RESTful endpoints.

Examples:

```text
GET    /api/services
POST   /api/bookings
GET    /api/bookings/{id}
PUT    /api/bookings/{id}
POST   /api/bookings/{id}/assign
POST   /api/bookings/{id}/confirm
POST   /api/bookings/{id}/cancel

GET    /api/househelps
GET    /api/househelps/{id}
POST   /api/househelps
PUT    /api/househelps/{id}

GET    /api/availability
PUT    /api/househelps/{id}/availability
```

Use feature-specific DTOs.

Do not expose EF entities directly from API endpoints.

Validate route IDs, request bodies, query parameters and authorization.

---

## 8. Angular standards

The frontend will use:

- Angular 21
- standalone components
- TypeScript strict mode
- Angular Router
- HttpClient
- Reactive Forms
- Bootstrap 5
- Bootstrap Icons

Angular 21 components should be standalone unless the existing project has a strong reason otherwise.

Use:

```text
core/
  auth/
  http/
  services/

shared/
  components/
  models/
  ui/

features/
  authentication/
  househelps/
  services/
  bookings/
  clients/
  administration/
```

Do not put business logic into templates.

Use reusable components for:

- tables
- loading states
- empty states
- confirmation dialogs
- notifications
- form fields where appropriate

---

## 9. Enterprise UX

The application should have a consistent enterprise visual language:

- restrained blue primary
- teal/green for success
- amber for warnings/import actions
- red only for destructive actions/errors
- white cards
- soft neutral page background
- strong typography hierarchy
- generous spacing
- accessible focus states
- clear loading states
- empty states
- confirmation before destructive operations

Do not introduce arbitrary colors per feature.

Prefer design tokens/CSS variables.

---

## 10. Data integrity

Important business rules must be enforced server-side.

Examples:

- service must exist and be active before booking
- househelp must exist and be eligible
- househelp cannot be double-booked for overlapping confirmed/in-progress work
- cancelled bookings cannot be completed
- completed bookings cannot be silently reassigned
- status transitions must be valid
- anonymous requests must have valid contact information
- codes/names that are required to be unique must have database uniqueness constraints

Where concurrency matters, use a transaction and/or appropriate SQL/EF concurrency strategy.

Never rely only on Angular validation.

---

## 11. Security

Never:

- log passwords
- log JWTs
- return password hashes
- trust client-supplied roles
- expose internal exception details in production
- store secrets in appsettings committed to Git
- allow arbitrary file paths from users

Validate uploads by:

- extension
- MIME type where useful
- file size
- content
- safe generated storage names

Authentication/authorization changes require extra review.

---

## 12. Testing

For backend features, prefer:

- unit tests for business rules
- integration tests for API behavior
- database/infrastructure tests where required

For Angular features, test:

- services
- important form validation
- important user flows
- route guards/interceptors where applicable

Every feature should include tests for its important failure paths, not only the happy path.

---

## 13. Agent workflow

For every task:

1. Read `AGENTS.md`.
2. Read `ARCHITECTURE.md`.
3. Read the relevant section of `TASKS.md`.
4. Inspect the existing code before editing.
5. Identify dependencies.
6. Implement the smallest coherent change.
7. Add/update tests.
8. Run the appropriate build/test commands.
9. Fix compilation/test failures.
10. Review the diff.
11. Update `TASKS.md`.
12. Update `STATUS.md` if the task changes project status.
13. Commit only the task's changes when the task is complete.

Do not silently implement unrelated tasks.

---

## 14. Git/workspace rules

DevSwarm workspaces are isolated Git worktrees.

Use one workspace per coherent workstream.

Recommended branch naming:

```text
feature/househelp-directory
feature/booking-workflow
feature/angular-shell
feature/service-catalog
test/booking-rules
fix/...
```

Do not work directly on `main`.

Do not reset or discard another agent's work.

Before committing:

```bash
git status
git diff --check
```

Keep commits small and meaningful.

---

## 15. Definition of done

A task is complete only when:

- implementation exists
- existing behavior still works
- validation exists
- authorization is correct
- important business rules are server-side
- tests are added/updated where appropriate
- backend builds
- frontend builds when relevant
- tests pass
- `TASKS.md` is updated
- no secrets are introduced
- diff is reviewed
- the task is committed

If blocked after reasonable investigation, stop and document the blocker rather than making speculative architectural changes.
