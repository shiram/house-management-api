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
- [ ] T007 — Create initial Angular 21 workspace under `frontend/house-management-web`
- [ ] T008 — Verify Angular strict mode, routing and standalone component configuration

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
- [ ] T041 — Verify migration against a clean SQL Server database
- [ ] T042 — Add seed data for development roles
- [ ] T043 — Add seed data for sample services
- [ ] T044 — Add safe development seed data for househelps

---

# PHASE 3 — Services / Service Catalog

- [ ] T050 — Create service DTOs
- [ ] T051 — Create service validation
- [ ] T052 — Implement public active-service listing
- [ ] T053 — Implement service details
- [ ] T054 — Implement Manager/Admin create service
- [ ] T055 — Implement Manager/Admin update service
- [ ] T056 — Implement activate/deactivate service
- [ ] T057 — Add service authorization tests
- [ ] T058 — Add service integration tests
- [ ] T059 — Add Angular service catalog API service
- [ ] T060 — Add Angular service list
- [ ] T061 — Add Angular service create/edit forms
- [ ] T062 — Add service status management UI

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
- [ ] T081 — Create Angular HouseHelp directory
- [ ] T082 — Create Angular HouseHelp profile form
- [ ] T083 — Create Angular HouseHelp detail page
- [ ] T084 — Add enterprise search/filter/pagination
- [ ] T085 — Add HouseHelp status and action controls

---

# PHASE 5 — Availability

- [ ] T090 — Design weekly availability model
- [ ] T091 — Design unavailable/leave exceptions
- [ ] T092 — Implement availability query
- [ ] T093 — Implement Manager/Admin availability management
- [ ] T094 — Implement HouseHelp self-service availability management
- [ ] T095 — Validate overlapping availability ranges
- [ ] T096 — Add availability integration tests
- [ ] T097 — Build Angular availability editor
- [ ] T098 — Build Angular availability calendar/list
- [ ] T099 — Add availability conflict indicators

---

# PHASE 6 — Booking / Service Request Core

- [ ] T100 — Define booking request DTOs
- [ ] T101 — Define anonymous client request DTO
- [ ] T102 — Define authenticated client request DTO
- [ ] T103 — Implement public service request endpoint
- [ ] T104 — Validate service exists and is active
- [ ] T105 — Validate requested service date/time
- [ ] T106 — Validate customer contact details
- [ ] T107 — Validate service address
- [ ] T108 — Implement booking creation transaction
- [ ] T109 — Implement booking reference generation
- [ ] T110 — Implement booking details endpoint
- [ ] T111 — Implement booking list for Manager/Admin
- [ ] T112 — Implement booking list for assigned HouseHelp
- [ ] T113 — Implement booking list for authenticated client
- [ ] T114 — Add safe anonymous booking tracking mechanism
- [ ] T115 — Add booking status transition service
- [ ] T116 — Add booking transition validation
- [ ] T117 — Add booking cancellation rules
- [ ] T118 — Add booking rejection rules
- [ ] T119 — Add booking confirmation rules
- [ ] T120 — Add booking completion rules
- [ ] T121 — Add booking integration tests

---

# PHASE 7 — HouseHelp Assignment & Concurrency

- [ ] T130 — Implement eligible HouseHelp query
- [ ] T131 — Implement assignment endpoint
- [ ] T132 — Validate HouseHelp is active
- [ ] T133 — Validate HouseHelp supports requested service
- [ ] T134 — Validate HouseHelp availability
- [ ] T135 — Implement overlapping booking detection
- [ ] T136 — Implement concurrency-safe assignment
- [ ] T137 — Prevent double booking under concurrent requests
- [ ] T138 — Add assignment audit information
- [ ] T139 — Add assignment integration tests
- [ ] T140 — Add concurrency test for double-booking protection
- [ ] T141 — Build Angular assignment screen
- [ ] T142 — Build eligible HouseHelp selection UI
- [ ] T143 — Add conflict/availability indicators

---

# PHASE 8 — Client Experience

- [ ] T150 — Design public client service request flow
- [ ] T151 — Build Angular public service catalog
- [ ] T152 — Build service request wizard/form
- [ ] T153 — Add date/time selection
- [ ] T154 — Add service address form
- [ ] T155 — Add customer contact form
- [ ] T156 — Add optional sign-in path
- [ ] T157 — Add request confirmation page
- [ ] T158 — Add anonymous request tracking
- [ ] T159 — Add authenticated client booking history
- [ ] T160 — Add responsive/mobile-first client screens
- [ ] T161 — Add client-side validation and server error display
- [ ] T162 — Add loading, empty and error states

---

# PHASE 9 — Angular Enterprise Shell

- [ ] T170 — Create enterprise design tokens
- [ ] T171 — Create application shell
- [ ] T172 — Create navbar
- [ ] T173 — Create sidebar
- [ ] T174 — Create responsive sidebar behavior
- [ ] T175 — Create notification menu
- [ ] T176 — Create user/account menu
- [ ] T177 — Create logout flow
- [ ] T178 — Create authenticated route guard
- [ ] T179 — Create HTTP auth interceptor
- [ ] T180 — Create centralized API error handling
- [ ] T181 — Create reusable toast/notification service
- [ ] T182 — Create reusable confirmation modal
- [ ] T183 — Create reusable loading/skeleton components
- [ ] T184 — Create reusable empty state
- [ ] T185 — Create reusable enterprise data table
- [ ] T186 — Create reusable form styling
- [ ] T187 — Create responsive design rules
- [ ] T188 — Accessibility pass for keyboard/focus/labels

---

# PHASE 10 — Manager Operations

- [ ] T200 — Manager dashboard
- [ ] T201 — Booking queue
- [ ] T202 — Booking detail
- [ ] T203 — Booking assignment workflow
- [ ] T204 — Booking status workflow UI
- [ ] T205 — HouseHelp management
- [ ] T206 — Availability management
- [ ] T207 — Service catalog management
- [ ] T208 — Operational search/filtering
- [ ] T209 — Dashboard KPI cards
- [ ] T210 — Dashboard recent activity
- [ ] T211 — Manager authorization tests

---

# PHASE 11 — HouseHelp Experience

- [ ] T220 — HouseHelp dashboard
- [ ] T221 — Assigned booking list
- [ ] T222 — Booking details
- [ ] T223 — Accept/acknowledge assignment if required by policy
- [ ] T224 — Start-work action
- [ ] T225 — Complete-work action
- [ ] T226 — Availability management
- [ ] T227 — Profile view/edit within permitted fields
- [ ] T228 — HouseHelp authorization tests

---

# PHASE 12 — Administration

- [ ] T240 — Admin dashboard
- [ ] T241 — User list
- [ ] T242 — User details
- [ ] T243 — Role management
- [ ] T244 — Activate/deactivate user
- [ ] T245 — Service administration
- [ ] T246 — HouseHelp administration
- [ ] T247 — Booking administration
- [ ] T248 — System settings foundation
- [ ] T249 — Audit log foundation
- [ ] T250 — Admin authorization tests

---

# PHASE 13 — Notifications

- [ ] T260 — Define notification model
- [ ] T261 — Define notification types
- [ ] T262 — Create in-app notification infrastructure
- [ ] T263 — Notify Manager when a new booking is created
- [ ] T264 — Notify client when booking is confirmed
- [ ] T265 — Notify HouseHelp when assigned
- [ ] T266 — Notify client when booking status changes
- [ ] T267 — Add notification read/unread state
- [ ] T268 — Build Angular notification center
- [ ] T269 — Add email/SMS abstraction without hard-coding a provider
- [ ] T270 — Add notification tests

---

# PHASE 14 — Audit & Security

- [ ] T280 — Define audit event model
- [ ] T281 — Audit authentication events
- [ ] T282 — Audit booking status changes
- [ ] T283 — Audit HouseHelp assignment
- [ ] T284 — Audit administrative changes
- [ ] T285 — Add sensitive-data logging rules
- [ ] T286 — Review authorization on every protected endpoint
- [ ] T287 — Review anonymous endpoints for data leakage
- [ ] T288 — Review file upload/security boundaries if uploads are introduced
- [ ] T289 — Add rate limiting strategy for public booking endpoints
- [ ] T290 — Add security headers/CORS policy
- [x] T291 — Run dependency/security audit

---

# PHASE 15 — Quality & Performance

- [ ] T300 — Backend unit test coverage review
- [ ] T301 — Backend integration test coverage review
- [ ] T302 — Angular service test review
- [ ] T303 — Critical Angular workflow test review
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
- [ ] T323 — Create Angular production build configuration
- [ ] T324 — Configure environment-specific Angular API URLs
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
- [ ] T347 — Payment provider integration
- [ ] T348 — Revenue/reporting dashboard
- [ ] T349 — HouseHelp earnings/reporting
- [ ] T350 — Advanced reporting/export

---

# Agent operating rules

## Parallel-safe work

These can usually run in parallel after their dependencies are satisfied:

```text
Service Catalog
HouseHelp Directory
Angular Shell
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
Manager operations UI
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
