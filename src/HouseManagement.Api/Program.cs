using Microsoft.EntityFrameworkCore;
using Serilog;
using HouseManagement.Api.Data;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using HouseManagement.Api.Common;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog from configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers();
// Common API infrastructure (minimal, non-breaking)
builder.Services.AddCommonServices();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure DbContext (SQL Server)
builder.Services.AddDbContext<HouseContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Password hasher and token service
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();

// JWT configuration
var jwtSection = builder.Configuration.GetSection("Jwt");
var key = jwtSection["Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "PleaseSetASecretKeyInEnv";
var issuer = jwtSection["Issuer"] ?? "HouseManagement";
var audience = jwtSection["Audience"] ?? "HouseManagement";
var keyBytes = Encoding.UTF8.GetBytes(key);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ValidateLifetime = true,
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Request/correlation ID middleware must run before Serilog request logging so the RequestId is included in logs
app.UseMiddleware<HouseManagement.Api.Common.Middleware.RequestIdMiddleware>();

// Global exception handling -> ProblemDetails
app.UseMiddleware<HouseManagement.Api.Common.Middleware.ExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

try
{
    Log.Information("Starting web host");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
