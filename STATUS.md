# House Management — AI Development Status

Last updated: initial setup

## Current state

### Backend

- .NET 8 API: existing
- SQL Server: existing
- EF Core/HouseContext: existing
- Serilog: existing
- User model: existing
- Authentication DTOs: existing
- JWT: existing
- AuthController: existing

### Frontend

- Angular: planned
- Angular workspace: pending T007
- Enterprise shell: pending T170+

### DevSwarm

- Repository: House Management API
- Strategy: modular monolith + vertical slices
- Workflow: isolated Git workspaces
- Human approval: required before merge

## Active workspaces

| Workspace | Branch | Purpose | Status |
|---|---|---|---|
| Primary | main/current | Coordination | idle |
| Backend Foundation | feature/backend-foundation | API infrastructure | pending |
| HouseHelp | feature/househelp-directory | HouseHelp module | pending |
| Services | feature/service-catalog | Service module | pending |
| Booking | feature/booking-workflow | Booking module | pending |
| Angular Shell | feature/angular-shell | Angular application shell | pending |
| Quality | test/quality | Tests/security | pending |

## Rules

Update this file only when a task materially changes project status.

Never claim a task is complete without build/test evidence.
