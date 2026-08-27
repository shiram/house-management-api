# Migrations and Development Workflow

This document describes the recommended workflow for EF Core migrations.

1. Create a feature branch for schema changes.
2. Add necessary entity/configuration changes in DbContext and entities.
3. Scaffold a migration locally:

   dotnet ef migrations add <DescriptiveName> --project src\HouseManagement.Api\HouseManagement.Api.csproj --startup-project src\HouseManagement.Api\HouseManagement.Api.csproj -o src\HouseManagement.Api\Migrations

4. Inspect the generated migration files in src/HouseManagement.Api/Migrations. Ensure they reflect intended changes and do not remove important indexes or constraints.
5. Run unit and integration tests locally.
6. Commit migration files and push branch.
7. In CI, run migrations against a staging DB (use ephemeral DB or explicit approval).
8. Apply migrations to production only after database backup and maintenance window scheduling.

Safety notes:
- Do not apply destructive migrations (DROP TABLE, DROP COLUMN) on production without explicit approval and backup.
- Prefer additive migrations when possible.
- Use transaction-safe operations for large data migrations or perform them out-of-band.
