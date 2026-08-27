# Local development — House Management API (backend)

This README explains how to build and run the backend locally.

Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB or Docker container)
- Optional: Visual Studio / VS Code

Environment variables (recommended):
- JWT_KEY: symmetric key for signing JWTs (override appsettings for safety)
- ConnectionStrings__DefaultConnection: SQL Server connection string

CI / Deployment notes:
- The repository CI workflow requires a repository secret JWT_KEY to be configured (see .github/workflows/ci.yml). The workflow fails if JWT_KEY is not set.
- In production, set JWT_KEY as a secret in your host/provider and avoid committing secrets to appsettings.json.
- Recommended additional env vars: ASPNETCORE_ENVIRONMENT (Development/Production), and any DB credentials.

Security:
- Do not commit secret keys to Git. Use environment variables or a secret store in CI and production.
- Audit NuGet dependencies regularly (dotnet list package --vulnerable).

Common commands

# Build the solution
dotnet build

# Run the API (reads appsettings.json by default)
dotnet run --project src\HouseManagement.Api

# Run tests (if present)
dotnet test

Notes
- Set JWT_KEY to a secure value in your shell before running the API in non-dev environments.
- The project currently uses appsettings.json defaults intended for development; do not commit production secrets.
- For migration work, create EF migrations in src/HouseManagement.Api and apply them against a dev database.

Support
If something fails during build or run, capture the dotnet output and open an issue or message the parent workspace with the error details.
