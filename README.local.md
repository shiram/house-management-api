# Local development — House Management API (backend)

This README explains how to build and run the backend locally.

Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB or Docker container)
- Optional: Visual Studio / VS Code

Environment variables (recommended):
- JWT_KEY: symmetric key for signing JWTs (override appsettings for safety)
- ConnectionStrings__DefaultConnection: SQL Server connection string

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
