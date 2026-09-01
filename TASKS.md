# House Management — Autonomous Development Task Queue

> Status markers:
> `[ ]` pending
> `[~]` in progress
> `[x]` complete
> `[!]` blocked
>
> Rule: an agent may only claim a task whose dependencies are complete.
> Parallel tasks must not edit the same shared files at the same time.

---

# PHASE 0 — Project Baseline

- [x] T001 — Inspect and preserve existing .NET 8 project, SQL Server setup, Serilog, User model, DTOs, JWT and AuthController
- [x] T002 — Document the current project structure and identify existing authentication extension points
- [x] T003 — Create a repeatable local development README
- [x] T004 — Add `.gitignore` entries for .NET, Angular, VS Code, DevSwarm and local secrets
- [x] T005 — Add environment/configuration documentation without committing secrets
- [x] T006 — Verify `dotnet build` and existing tests

---

# PHASE 1 — Backend Foundation

- [x] T010 — Introduce Common API infrastructure without breaking existing endpoints
- [x] T011 — Standardize API response envelope
- [x] T012 — Add request/correlation ID middleware
- [x] T013 — Integrate request ID with Serilog structured logging
- [x] T014 — Add global exception handling
- [x] T015 — Add ProblemDetails for unexpected errors
- [x] T016 — Add consistent validation error response
- [x] T017 — Establish API versioning strategy
- [x] T018 — Establish UTC/DateTimeOffset conventions
- [x] T019 — Add EF Core entity configuration conventions
- [x] T020 — Add database migration/development workflow documentation
- [x] T021 — Add health checks for API and SQL Server
- [x] T022 — Add OpenAPI/Swagger conventions and authentication support
- [x] T023 — Add integration test infrastructure
- [x] T024 — Add authorization policy conventions for Admin, Manager and HouseHelp
- [x] T025 — Review existing JWT claims and role authorization for compatibility with the new policies (hardened: self-registration forces 'househelp')

---

# PHASE 2 — Domain & Database

- [x] T030 — Design HouseHelp domain model
- [x] T031 — Design Service domain model
- [x] T032 — Design Client/customer model supporting anonymous requests
- [x] T033 — Design Booking domain model
- [x] T034 — Design BookingStatus and valid status transitions
- [x] T035 — Design HouseHelp availability model
- [x] T036 — Design service address model
- [x] T037 — Add entity configurations and relationships
- [x] T038 — Add unique constraints for required business identifiers
- [x] T039 — Add operational indexes for booking and directory queries
- [x] T040 — Create initial business-domain EF migration
- [x] T041 — Verify migration against a clean SQL Server database
- [x] T042 — Add seed data for development roles
- [x] T043 — Add seed data for sample services
- [x] T044 — Add safe development seed data for househelps

---

# PHASE 3 — Services / Service Catalog

- [x] T050 — Create service DTOs
- [x] T051 — Create service validation
- [x] T052 — Implement public active-service listing
- [x] T053 — Implement service details
- [x] T054 — Implement Manager/Admin create service
- [x] T055 — Implement Manager/Admin update service
- [x] T056 — Implement activate/deactivate service
- [x] T057 — Add service authorization tests
- [x] T058 — Add service integration tests

---

# PHASE 4 — HouseHelp Directory

- [x] T070 — Create HouseHelp profile DTOs
- [x] T071 — Create HouseHelp validation
- [x] T072 — Implement public HouseHelp directory listing
- [x] T073 — Implement HouseHelp profile details
- [x] T074 — Implement Manager/Admin create HouseHelp
- [x] T075 — Implement Manager/Admin update HouseHelp
- [x] T076 — Implement HouseHelp activation/deactivation
- [x] T077 — Add HouseHelp skills/services eligibility
- [x] T078 — Add location/search/filter support
- [x] T079 — Add HouseHelp authorization tests
- [x] T080 — Add HouseHelp integration tests

---

# PHASE 5 — Availability

- [x] T090 — Design weekly availability model
- [x] T091 — Design unavailable/leave exceptions
- [x] T092 — Implement availability query
- [x] T093 — Implement Manager/Admin availability management
- [x] T094 — Implement HouseHelp self-service availability management
- [x] T095 — Validate overlapping availability ranges
- [x] T096 — Add availability integration tests

---

# PHASE 6 — Booking / Service Request Core

- [x] T100 — Define booking request DTOs
- [x] T101 — Define anonymous client request DTO
- [x] T102 — Define authenticated client request DTO
- [x] T103 — Implement public service request endpoint
- [x] T104 — Validate service exists and is active
- [x] T105 — Validate requested service date/time
- [x] T106 — Validate customer contact details
- [x] T107 — Validate service address
- [x] T108 — Implement booking creation transaction
- [x] T109 — Implement booking reference generation
- [x] T110 — Implement booking details endpoint
- [x] T111 — Implement booking list for Manager/Admin
- [x] T112 — Implement booking list for assigned HouseHelp
- [x] T113 — Implement booking list for authenticated client
- [x] T114 — Add safe anonymous booking tracking mechanism
- [x] T115 — Add booking status transition service
- [x] T116 — Add booking transition validation
- [x] T117 — Add booking cancellation rules
- [x] T118 — Add booking rejection rules
- [x] T119 — Add booking confirmation rules
- [x] T120 — Add booking completion rules
- [x] T121 — Add booking integration tests

---

# PHASE 7 — HouseHelp Assignment & Concurrency

- [x] T130 — Implement eligible HouseHelp query
- [x] T131 — Implement assignment endpoint
- [x] T132 — Validate HouseHelp is active
- [x] T133 — Validate HouseHelp supports requested service
- [x] T134 — Validate HouseHelp availability
- [x] T135 — Implement overlapping booking detection
- [x] T136 — Implement concurrency-safe assignment
- [x] T137 — Prevent double booking under concurrent requests
- [x] T138 — Add assignment audit information
- [x] T139 — Add assignment integration tests
- [x] T140 — Add concurrency test for double-booking protection

---

# PHASE 10 — Manager Operations

- [x] T211 — Manager authorization tests

---

# PHASE 11 — HouseHelp Experience

- [x] T228 — HouseHelp authorization tests

---

# PHASE 12 — Administration

- [x] T241 — User list
- [x] T242 — User details
- [x] T243 — Role management
- [x] T244 — Activate/deactivate user
- [x] T245 — Service administration
- [x] T246 — HouseHelp administration
- [x] T247 — Booking administration
- [x] T248 — System settings foundation
- [x] T249 — Audit log foundation
- [x] T250 — Admin authorization tests

---

# PHASE 13 — Notifications

- [x] T260 — Define notification model
- [x] T261 — Define notification types
- [x] T262 — Create in-app notification infrastructure
- [x] T263 — Notify Manager when a new booking is created
- [x] T264 — Notify client when booking is confirmed
- [x] T265 — Notify HouseHelp when assigned
- [x] T266 — Notify client when booking status changes
- [x] T267 — Add notification read/unread state
- [x] T269 — Add email/SMS abstraction without hard-coding a provider
- [x] T270 — Add notification tests

---

# PHASE 14 — Audit & Security

- [x] T280 — Define audit event model
- [x] T281 — Audit authentication events
- [x] T282 — Audit booking status changes
- [x] T283 — Audit HouseHelp assignment
- [x] T284 — Audit administrative changes
- [x] T285 — Add sensitive-data logging rules
- [x] T286 — Review authorization on every protected endpoint
- [x] T287 — Review anonymous endpoints for data leakage
- [x] T288 — Review file upload/security boundaries if uploads are introduced
- [x] T289 — Add rate limiting strategy for public booking endpoints
- [x] T290 — Add security headers/CORS policy
- [x] T291 — Run dependency/security audit

---

# PHASE 15 — Quality & Performance

- [x] T300 — Backend unit test coverage review
- [ ] T301 — Backend integration test coverage review
- [ ] T304 — Database query performance review
- [ ] T305 — Booking conflict query performance review
- [ ] T306 — Add pagination to large datasets
- [ ] T307 — Add server-side filtering where required
- [ ] T308 — Add structured logging review
- [ ] T309 — Add health/readiness checks
- [ ] T310 — Add production configuration review
- [ ] T311 — Run full build/test pipeline

---

# PHASE 16 — Deployment

- [ ] T320 — Create production configuration documentation
- [ ] T321 — Create SQL Server deployment/migration process
- [ ] T322 — Create backend Dockerfile if container deployment is selected
- [ ] T325 — Configure reverse proxy/HTTPS
- [ ] T326 — Configure Serilog production sinks
- [ ] T327 — Configure health monitoring
- [x] T328 — Create CI build/test workflow
- [ ] T329 — Create CI migration/deployment strategy
- [ ] T330 — Create rollback procedure

---

# PHASE 17 — Product Enhancements

- [ ] T340 — HouseHelp ratings/reviews
- [ ] T341 — Customer booking history
- [ ] T342 — Repeat booking
- [ ] T343 — Favorite/preferred HouseHelp
- [ ] T344 — Pricing rules
- [ ] T345 — Promotions/discounts
- [ ] T346 — Payment integration abstraction
- [ ] T347 — Payment provider integration(clients will pay with cards(visa), mobile money (mtn and airtel for uganda))
- [ ] T349 — HouseHelp earnings/reporting
- [ ] T350 — Advanced reporting/export

---

# PHASE 18 — HouseHelp Profiles & Media

- [ ] T360 — Define HouseHelp profile fields, visibility contract, and authorization rules
- [ ] T361 — Add private HouseHelp profile fields and an EF Core migration
- [ ] T362 — Create profile-image storage and safe image-processing abstraction
- [ ] T363 — Implement authorized HouseHelp profile update endpoints
- [ ] T364 — Implement secure profile-image upload and replacement workflow
- [ ] T365 — Implement safe profile-image retrieval and public-profile image projection
- [ ] T366 — Audit HouseHelp profile and media changes
- [ ] T367 — Add HouseHelp profile and media unit/integration coverage

---

# Agent operating rules

## Parallel-safe work

These can usually run in parallel after their dependencies are satisfied:

```text
Service Catalog
HouseHelp Directory
Test Infrastructure
Documentation
```

## Sequential work

These require coordination:

```text
Database model
    ->
Availability
    ->
Booking
    ->
Assignment
    ->
Manager operations
```

## High-risk tasks requiring human review

Do not auto-merge without review:

```text
T025  JWT/authorization changes
T040  database migration
T136  concurrency
T137  double-booking protection
T286  authorization review
T289  public endpoint rate limiting
T290  CORS/security
T329  deployment
```
