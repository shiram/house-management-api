# EF Core Conventions

This project uses EF Core with the following conventions and expectations:

- Use explicit entity configuration in DbContext.OnModelCreating for indexes, column lengths and relationships.
- Prefer DateTimeOffset for persisted timestamps where timezone is important; DateTime.UtcNow is used in legacy code — prefer DateTimeOffset moving forward.
- Add unique constraints for natural keys (e.g., Users.Email, Users.UserName) using HasIndex(...).IsUnique().
- Keep migrations in src/HouseManagement.Api/Migrations and check them into source control.
- Use migrations for schema changes; avoid ad-hoc database modifications in production.
- Use transactions for multi-step business operations that must be atomic.
- Avoid cascade delete unless it's explicitly desired; prefer explicit deletes.
- Configure string property lengths to avoid NVARCHAR(MAX) surprises.

Migration commands (local dev):

# Add a migration (name it clearly)
dotnet ef migrations add <Name> --project src\HouseManagement.Api\HouseManagement.Api.csproj --startup-project src\HouseManagement.Api\HouseManagement.Api.csproj -o src\HouseManagement.Api\Migrations

# Apply migrations to local DB
dotnet ef database update --project src\HouseManagement.Api\HouseManagement.Api.csproj --startup-project src\HouseManagement.Api\HouseManagement.Api.csproj

Notes:
- Always review generated migrations before applying.
- Use a clean database or a branch-specific database when developing migrations to avoid conflicts.
