using Microsoft.EntityFrameworkCore;
using Serilog;
using HouseManagement.Api.Data;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using HouseManagement.Api.Common;
using HouseManagement.Api.Common.Security;
using Asp.Versioning;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using HouseManagement.Api.Common.Api;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog from configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers(options =>
{
    // Global validation & result-wrapping
    options.Filters.Add<HouseManagement.Api.Common.Api.ValidationFilter>();
    options.Filters.Add<HouseManagement.Api.Common.Api.ApiResultFilter>();
});
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1.0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});
// Common API infrastructure (minimal, non-breaking)
builder.Services.AddCommonServices();

builder.Services.AddEndpointsApiExplorer();
// Swagger with JWT bearer support will be added below
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});

// Configure DbContext (SQL Server)
builder.Services.AddDbContext<HouseContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<HouseManagement.Api.Common.Health.DbHealthCheck>("database");

// Password hasher and token service
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();

// HouseHelp domain service
builder.Services.AddScoped<IHouseHelpService, HouseHelpService>();
builder.Services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IBookingStatusService, BookingStatusService>();
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<IBookingTransitionValidator, BookingTransitionValidator>();

// JWT configuration
var jwtSection = builder.Configuration.GetSection("Jwt");
var key = JwtConfiguration.GetSigningKey(builder.Configuration);
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
        policy.RequireRole(AuthorizationPolicies.AdminRole));
    options.AddPolicy(AuthorizationPolicies.ManagerOrAdmin, policy =>
        policy.RequireRole(AuthorizationPolicies.AdminRole, AuthorizationPolicies.ManagerRole));
    options.AddPolicy(AuthorizationPolicies.HouseHelpOnly, policy =>
        policy.RequireRole(AuthorizationPolicies.HouseHelpRole));
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()?
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim())
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var publicBookingRateLimitSection = builder.Configuration.GetSection("RateLimiting:PublicBooking");
var submissionPermitLimit = publicBookingRateLimitSection.GetValue<int?>("SubmissionPermitLimit") ?? 5;
var trackingPermitLimit = publicBookingRateLimitSection.GetValue<int?>("TrackingPermitLimit") ?? 30;
var windowSeconds = publicBookingRateLimitSection.GetValue<int?>("WindowSeconds") ?? 60;

if (submissionPermitLimit <= 0 || trackingPermitLimit <= 0 || windowSeconds <= 0)
{
    throw new InvalidOperationException("Public booking rate limit configuration values must be greater than zero.");
}

var publicBookingWindow = TimeSpan.FromSeconds(windowSeconds);
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(RateLimitPolicyNames.PublicBookingSubmission, httpContext =>
        CreateFixedWindowPartition(httpContext, submissionPermitLimit, publicBookingWindow));
    options.AddPolicy(RateLimitPolicyNames.PublicBookingTracking, httpContext =>
        CreateFixedWindowPartition(httpContext, trackingPermitLimit, publicBookingWindow));
    options.OnRejected = async (context, cancellationToken) =>
    {
        var httpContext = context.HttpContext;
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.Headers.RetryAfter = windowSeconds.ToString();

        var requestId = httpContext.Items.TryGetValue("RequestId", out var requestIdValue)
            ? requestIdValue?.ToString()
            : httpContext.TraceIdentifier;
        await httpContext.Response.WriteAsJsonAsync(
            new ApiResponse<object?>
            {
                StatusCode = StatusCodes.Status429TooManyRequests,
                Message = "Too many requests. Please try again later.",
                RequestId = requestId
            },
            cancellationToken);
    };
});

var app = builder.Build();

// Validate runtime JWT configuration: in production require a real key
if (app.Environment.IsProduction())
{
    var effectiveKey = JwtConfiguration.GetSigningKey(app.Configuration);
    if (!JwtConfiguration.IsProductionSafeSigningKey(effectiveKey))
    {
        Log.Fatal("A secure JWT signing key is not configured. Set JWT_KEY in production.");
        throw new InvalidOperationException("A secure JWT signing key is required in production.");
    }
}

if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<HouseContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DevelopmentDataSeeder");
        if (app.Configuration.GetValue<bool>("DevelopmentSeed:Enabled"))
        {
            await DevelopmentDataSeeder.SeedRolesAsync(
                context,
                passwordHasher,
                app.Configuration,
                logger);
            await DevelopmentDataSeeder.SeedServicesAsync(context, logger);
            await DevelopmentDataSeeder.SeedHouseHelpsAsync(context, logger);
        }
        else
        {
            logger.LogInformation("Development data seeding is disabled.");
        }
    }

    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Request/correlation ID middleware must run before Serilog request logging so the RequestId is included in logs
app.UseMiddleware<HouseManagement.Api.Common.Middleware.RequestIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

// Global exception handling -> ProblemDetails
app.UseMiddleware<HouseManagement.Api.Common.Middleware.ExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// Liveness endpoint (quick check)
app.MapGet("/health/live", () => Results.Ok(new { status = "Alive" }));

// Readiness endpoint (includes DB health check)
app.MapHealthChecks("/health/ready");

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

static RateLimitPartition<string> CreateFixedWindowPartition(
    HttpContext httpContext,
    int permitLimit,
    TimeSpan window)
{
    var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-client";
    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0,
            AutoReplenishment = true
        });
}
