# House Management Platform — Architecture

## 1. Recommended architecture

Use a **modular monolith + vertical slices**.

This is preferable to microservices at this stage because the product is one business domain, the team is still establishing workflows, and the deployment/operations overhead of microservices would add complexity without enough benefit.

Microsoft's own .NET architecture guidance notes that a single deployable monolith is often easier to build, deploy and debug, while its architecture material also covers vertical slices and modular monoliths.

The goal is:

```text
ONE DEPLOYMENT
     |
     +-- Authentication
     +-- HouseHelp Management
     +-- Service Catalog
     +-- Availability
     +-- Booking
     +-- Client/Customer
     +-- Administration
     +-- Notifications
     +-- Audit
```

Each module has a clear boundary.

---

## 2. Repository layout

Recommended final repository:

```text
HouseManagement/
|
+-- src/
|   |
|   +-- HouseManagement.Api/
|   |   |
|   |   +-- Common/
|   |   |   +-- Api/
|   |   |   +-- Exceptions/
|   |   |   +-- Validation/
|   |   |   +-- Security/
|   |   |   +-- Results/
|   |   |
|   |   +-- Data/
|   |   |   +-- HouseContext.cs
|   |   |   +-- Configurations/
|   |   |   +-- Migrations/
|   |   |
|   |   +-- Infrastructure/
|   |   |   +-- Authentication/
|   |   |   +-- Email/
|   |   |   +-- Notifications/
|   |   |   +-- Files/
|   |   |
|   |   +-- Features/
|   |       |
|   |       +-- Authentication/
|   |       +-- HouseHelps/
|   |       +-- Services/
|   |       +-- Availability/
|   |       +-- Bookings/
|   |       +-- Clients/
|   |       +-- Administration/
|   |       +-- Notifications/
|   |       +-- Audit/
|   |
|   +-- HouseManagement.Tests/
|
+-- frontend/
|   |
|   +-- house-management-web/
|       +-- src/
|           +-- app/
|               +-- core/
|               +-- shared/
|               +-- layout/
|               +-- features/
|
+-- docs/
|   +-- ARCHITECTURE.md
|   +-- API.md
|   +-- BOOKING-WORKFLOW.md
|   +-- SECURITY.md
|
+-- scripts/
|
+-- AGENTS.md
+-- TASKS.md
+-- STATUS.md
+-- autonomous-dev.ps1
```

If moving the current API into this layout is disruptive, do it incrementally. Do not perform a large restructuring while simultaneously implementing business features.

---

## 3. Backend feature structure

Example:

```text
Features/
  Bookings/
    Booking.cs
    BookingStatus.cs
    BookingEndpoints.cs
    BookingDtos.cs
    BookingValidator.cs
    CreateBooking.cs
    GetBooking.cs
    AssignHouseHelp.cs
    CancelBooking.cs
```

The exact file structure may vary, but keep booking-related behavior together.

Avoid:

```text
Controllers/
Services/
Repositories/
DTOs/
Validators/
```

as one giant application-wide folder structure. It becomes harder for multiple AI agents to work safely because every feature touches the same folders.

---

## 4. Core modules

### Authentication

Existing implementation already exists.

Responsibilities:

- login
- password verification
- JWT generation
- refresh token if required
- role claims
- current user identity

Do not rewrite this during unrelated work.

### HouseHelps

Responsibilities:

- directory
- profile
- skills
- service eligibility
- status
- location
- availability

### Services

Responsibilities:

- cleaning
- laundry
- future service types
- pricing/configuration
- active/inactive state

### Availability

Responsibilities:

- working days
- working hours
- exceptions
- leave/unavailable periods

### Bookings

This is the core operational module.

Responsibilities:

- service request
- scheduling
- assignment
- confirmation
- cancellation
- completion
- status transitions
- conflict detection

### Clients

A client can be anonymous.

For registered clients, maintain a client profile linked to the user.

For anonymous bookings, store the contact information needed for fulfilment without forcing account creation.

### Administration

Responsibilities:

- users
- roles
- system settings
- operational configuration
- audit

---

## 5. Booking model

Recommended lifecycle:

```text
REQUESTED
    |
    +---- REJECTED
    |
    +---- CANCELLED
    |
    v
CONFIRMED
    |
    v
ASSIGNED
    |
    v
IN_PROGRESS
    |
    v
COMPLETED
```

Assignment may occur before or after confirmation depending on operational policy.

Do not allow arbitrary status changes.

Implement a server-side transition map.

---

## 6. Booking conflict protection

The UI may check availability, but the API must enforce it.

For a househelp:

```text
requested start < existing end
AND
requested end > existing start
```

means an overlap.

Only statuses that reserve the worker should participate in the conflict query.

The final implementation should also account for SQL Server concurrency so two requests arriving at nearly the same time cannot both successfully reserve the same worker.

---

## 7. Anonymous client strategy

Anonymous clients are first-class booking requesters, but not application users.

A booking can contain:

```text
ClientUserId nullable
ClientName
ClientPhone
ClientEmail nullable
ServiceAddress
```

If a registered user submits the booking:

```text
ClientUserId = authenticated user id
```

If anonymous:

```text
ClientUserId = null
```

Do not create fake users just to represent anonymous clients.

---

## 8. API response strategy

Use a consistent API contract.

For example:

```json
{
  "statusCode": 200,
  "message": "Booking retrieved",
  "data": {},
  "requestId": "..."
}
```

For validation/business errors, use a consistent error contract.

Unexpected server failures should use ProblemDetails and should not expose stack traces.

---

## 9. Database strategy

SQL Server + EF Core.

Use:

- explicit indexes
- foreign keys
- unique constraints
- check constraints where useful
- transactions for multi-step business operations
- migrations checked into source control

Important indexes will likely include:

```text
HouseHelp(status)
HouseHelp(location)
Booking(houseHelpId, startDateTime, endDateTime)
Booking(status)
Booking(clientUserId)
Booking(createdAt)
Service(status)
```

Add indexes based on real query patterns rather than indexing every column.

---

## 10. Angular architecture

Angular 21 should use standalone components.

```text
app/
|
+-- core/
|   +-- auth/
|   +-- guards/
|   +-- interceptors/
|   +-- api/
|   +-- services/
|
+-- layout/
|   +-- shell/
|   +-- navbar/
|   +-- sidebar/
|
+-- shared/
|   +-- components/
|   +-- dialogs/
|   +-- tables/
|   +-- forms/
|   +-- models/
|
+-- features/
    +-- auth/
    +-- househelps/
    +-- services/
    +-- bookings/
    +-- clients/
    +-- administration/
```

Use route-level lazy loading for features.

Keep API calls in services, not directly in templates.

---

## 11. Enterprise UI

Use a restrained enterprise system:

Primary:
`#155EEF` / deep enterprise blue

Secondary:
`#0F766E` / teal

Success:
`#15803D`

Warning:
`#D97706`

Danger:
`#DC2626`

Page background:
`#F5F7FA`

Card:
`#FFFFFF`

Text:
`#111827`

Muted:
`#64748B`

Borders:
`#E2E8F0`

Use CSS variables so the palette can be changed globally.

Buttons:

- primary = create/save/confirm
- secondary = navigation/filter
- warning = import/attention
- danger = delete/cancel where destructive

Avoid excessive gradients, shadows and saturated colors.

---

## 12. DevSwarm workstream strategy

DevSwarm gives each workspace an isolated Git worktree/branch and can run multiple AI assistants in parallel.

Recommended workspaces:

### Workspace A — Lead/Architecture

Owns:

- task planning
- cross-feature coordination
- architectural decisions

### Workspace B — Backend Foundation

Owns:

- API infrastructure
- validation
- ProblemDetails
- authorization
- database conventions

### Workspace C — HouseHelp

Owns:

- directory
- profile
- availability

### Workspace D — Services

Owns:

- service catalog
- pricing/configuration

### Workspace E — Booking

Owns:

- booking lifecycle
- assignment
- conflict detection

### Workspace F — Angular Shell

Owns:

- login
- layout
- navbar
- sidebar
- routing
- design system

### Workspace G — Angular Operations

Owns:

- househelp UI
- service UI
- booking UI

### Workspace H — Quality

Owns:

- tests
- security review
- validation
- performance review

Do not let multiple workspaces edit the same core files at the same time.

---

## 13. Merge order

Recommended:

```text
Foundation
   |
   +--> Angular Shell
   |
   +--> Services
   |
   +--> HouseHelp
           |
           v
      Availability
           |
           v
        Booking
           |
           v
       Operations UI
           |
           v
       QA / Security
```

Some work can run in parallel, but database migrations and shared contracts require coordination.

---

## 14. Architectural rule for AI agents

The agent should prefer:

```text
small change
+
existing architecture
+
test
+
review
```

over:

```text
large rewrite
+
new framework
+
new architecture
```

If an agent believes a major architectural change is necessary, it should stop and document the proposal rather than silently performing it.
