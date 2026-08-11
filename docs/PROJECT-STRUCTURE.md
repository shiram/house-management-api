# Project structure — House Management API

This document describes the current repository layout and where authentication extension points exist.

Repository layout (current):

- src/HouseManagement.Api/: main Web API project
  - Program.cs: DI, Serilog, JWT configuration
  - Data/HouseContext.cs: EF Core DbContext
  - Models/: EF entities (User.cs)
  - Controllers/: API controllers (AuthController.cs)
  - Services/: TokenService, PasswordHasher, service interfaces
  - DTOs/: Authentication DTOs (RegisterRequest, LoginRequest, AuthResponse)

- docs/: architecture and project docs
- .devswarm-temp/: workspace metadata

Authentication extension points:

- TokenService (src/.../Services/TokenService.cs): central JWT creation. Replace/extend for refresh tokens, token revocation, or custom claims.
- Program.cs: JwtBearer configuration and DI registration. Use this to change validation parameters, issuer/audience, or require HTTPS.
- PasswordHasher (src/.../Services/PasswordHasher.cs): PBKDF2 implementation. Swap or extend for versioning or different hashing strategies.
- AuthController (src/.../Controllers/AuthController.cs): registration and login endpoints — control role assignment and validation here.
- HouseContext/Users DbSet: storage location for user records and any login/refresh token tables.

Notes:
- Do not rewrite the current JWT flow without explicit review (T025). Prefer adding features (refresh tokens, policies) around these extension points.
