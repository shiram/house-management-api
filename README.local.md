# Local development — House Management API (backend)

This README explains how to build and run the backend locally.

Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB or Docker container)
- Optional: Visual Studio / VS Code

Environment variables (recommended):
- JWT_KEY: required production symmetric key for signing JWTs; it takes precedence over appsettings
- ConnectionStrings__DefaultConnection: SQL Server connection string
- DEV_SEED_PASSWORD: optional password used to create development admin, manager, and househelp users
- DevelopmentSeed__Enabled: set to `true` to enable development data seeding explicitly
- PaymentProviders__GenericHttp__Enabled: set to `true` only when using a sandbox payment gateway adapter
- PaymentProviders__GenericHttp__EndpointUrl: sandbox payment-initiation endpoint for the generic HTTP adapter
- PaymentProviders__GenericHttp__ApiKey: optional sandbox provider key; do not commit it to source control
- PaymentProviders__Pesapal__Enabled: set to `true` to use the Pesapal adapter (selected production provider, see `docs/PAYMENT-PROVIDER-DECISION.md`)
- PaymentProviders__Pesapal__BaseUrl: Pesapal API host, e.g. `https://cybqa.pesapal.com/pesapalv3` (sandbox) or `https://pay.pesapal.com/v3` (production)
- PaymentProviders__Pesapal__ConsumerKey / PaymentProviders__Pesapal__ConsumerSecret: Pesapal merchant credentials; never commit these to source control
- PaymentProviders__Pesapal__CallbackUrl: this API's public IPN/redirect callback endpoint
- PaymentProviders__Pesapal__IpnId: the `notification_id` returned by a one-time Pesapal `RegisterIPN` call against CallbackUrl

CI / Deployment notes:
- The repository CI workflow requires a repository secret JWT_KEY to be configured (see .github/workflows/ci.yml). The workflow fails if JWT_KEY is not set.
- In production, set a non-placeholder JWT_KEY of at least 32 characters in your host/provider. Startup rejects missing, placeholder, and short signing keys.
- Recommended additional env vars: ASPNETCORE_ENVIRONMENT (Development/Production), and any DB credentials.
- When running in Development with `DevelopmentSeed__Enabled=true` and DEV_SEED_PASSWORD configured, startup idempotently seeds `dev-admin`, `dev-manager`, and `dev-househelp`.
- Development startup also idempotently seeds the sample `HOUSE_CLEANING` and `LAUNDRY` services.
- Development startup also idempotently seeds two sample HouseHelp profiles without creating authentication accounts.
- Payment provider secrets must be supplied via environment variables or a secret store. The committed generic HTTP provider config is disabled by default and contains no credentials.

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
