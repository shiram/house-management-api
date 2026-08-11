# House Management Copilot Instructions

Read `AGENTS.md` and `ARCHITECTURE.md` before making code changes.

Use `TASKS.md` as the source of truth for planned work.

This project uses:
- .NET 8
- ASP.NET Core
- SQL Server
- EF Core
- Serilog
- JWT authentication
- Angular 21
- Bootstrap 5

Architecture:
- modular monolith
- vertical slices
- feature-oriented organization
- thin controllers
- feature-specific DTOs
- server-side business rules

Existing authentication/JWT work is already implemented. Preserve it unless the assigned task explicitly modifies authentication.

Never implement unrelated tasks.

Always:
- inspect before editing
- build
- test
- review the diff
- update task status
- avoid secrets
- avoid destructive database operations
- keep changes small and reviewable
