# EF Core Conventions

This project uses EF Core with the following conventions and expectations:

- Use explicit entity configuration in DbContext.OnModelCreating for indexes, column lengths and relationships.
- Use DateTimeOffset for all new persisted timestamps and initialize them with DateTimeOffset.UtcNow.
- User.CreatedAt and User.LastLogin remain DateTime for compatibility with the existing schema/migration; do not change them without a dedicated migration and data review.
- Use DateTime.UtcNow only for legacy compatibility or framework APIs that require DateTime (for example JWT expiration).
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
