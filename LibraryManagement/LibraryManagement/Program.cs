
using LibraryManagement.Data;
using LibraryManagement.Exceptions;
using LibraryManagement.Helpers;
using LibraryManagement.Repositories.Implementations;
using LibraryManagement.Repositories.Interfaces;
using LibraryManagement.Services.Implementations;
using LibraryManagement.Services.Interfaces;
using LibraryManagement.Realtime;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Write application logs to stdout.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ==================================================
// CONTROLLERS
// ==================================================

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            ReferenceHandler.IgnoreCycles;
    });

// ==================================================
// CORS
// ==================================================


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:3000",
                "http://127.0.0.1:5173",
                "https://astounding-quokka-a67be2.netlify.app",
                "https://librarymangementborrows.netlify.app")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ==================================================
// DATABASE
// ==================================================

if (builder.Environment.IsProduction())
{
    // Production: Neon PostgreSQL on Render
    var connectionString =
        DatabaseConnection.Resolve(builder.Configuration);

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(
            connectionString,
            npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null)));
}
else
{
    // Local development: SQLite
    var connectionString =
        builder.Configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "SQLite connection string is missing.");
    }

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
}

// ==================================================
// REPOSITORIES
// ==================================================

builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<ILoginHistoryRepository, LoginHistoryRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IAuthorRepository, AuthorRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();

builder.Services.AddScoped<
    IBorrowTransactionRepository,
    BorrowTransactionRepository>();

// ==================================================
// SERVICES
// ==================================================

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddSingleton<ILoginActivityEventBus, LoginActivityEventBus>();
builder.Services.AddScoped<ILoginHistoryService, LoginHistoryService>();
builder.Services.AddScoped<IBorrowService, BorrowService>();

// ==================================================
// JWT TOKEN GENERATOR
// ==================================================

builder.Services.AddScoped<JwtTokenGenerator>();
builder.Services.AddHttpContextAccessor();

// ==================================================
// JWT AUTHENTICATION
// ==================================================

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var key = builder.Configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "JWT Key is missing from appsettings.json.");
        }

        // A valid signature is not enough: the session named by the jti claim
        // must still be active (not revoked, not expired). This is what makes
        // a revoked device lose access on its very next request.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                var sessionId = principal?.FindFirst(
                    System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;

                if (!int.TryParse(
                        principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                        out var userId) ||
                    string.IsNullOrWhiteSpace(sessionId) ||
                    !await context.HttpContext.RequestServices
                        .GetRequiredService<ISessionService>()
                        .ValidateAsync(userId, sessionId))
                {
                    context.Fail("The session is no longer active.");
                }
            }
        };

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],

                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(key))
            };
    });

// ==================================================
// AUTHORIZATION
// ==================================================

builder.Services.AddAuthorization();

// ==================================================
// SWAGGER
// ==================================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token. Example: Bearer {your-token}"
        });

    // Updated OpenAPI security reference syntax
    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});

// ==================================================
// BUILD APPLICATION
// ==================================================

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    try
    {
        Console.WriteLine("===== DATABASE DIAGNOSTICS =====");

        Console.WriteLine(
            $"Environment: {app.Environment.EnvironmentName}");

        Console.WriteLine(
            $"Provider: {db.Database.ProviderName}");

        var connection = db.Database.GetDbConnection();

        Console.WriteLine(
            $"Database: {connection.Database}");

        Console.WriteLine(
            $"Host: {connection.DataSource}");

        var connected = await db.Database.CanConnectAsync();

        Console.WriteLine(
            $"Database Connected: {connected}");

        Console.WriteLine("===== END DIAGNOSTICS =====");
    }
    catch (Exception ex)
    {
        Console.WriteLine("DATABASE ERROR:");
        Console.WriteLine(ex.ToString());
    }
}

var trustedProxyAddresses = builder.Configuration
    .GetSection("ForwardedHeaders:KnownProxies")
    .Get<string[]>() ?? [];

var trustedProxyIps = new HashSet<System.Net.IPAddress>
{
    System.Net.IPAddress.Loopback,
    System.Net.IPAddress.IPv6Loopback
};

foreach (var address in trustedProxyAddresses)
{
    if (System.Net.IPAddress.TryParse(address, out var proxyAddress))
    {
        trustedProxyIps.Add(proxyAddress);
    }
}

static System.Net.IPAddress NormalizeIp(
    System.Net.IPAddress address)
    => address.IsIPv4MappedToIPv6
        ? address.MapToIPv4()
        : address;

var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = 
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1
};

foreach (var proxyAddress in trustedProxyIps)
{
    forwardedHeaders.KnownProxies.Add(proxyAddress);

    if (proxyAddress.AddressFamily ==
        System.Net.Sockets.AddressFamily.InterNetwork)
    {
        forwardedHeaders.KnownProxies.Add(
            proxyAddress.MapToIPv6());
    }
}

// X-Forwarded-For takes precedence. Accept X-Real-IP only from
// the directly connected, explicitly trusted proxy.
app.Use(async (context, next) =>
{
    var peer = context.Connection.RemoteIpAddress;

    var realIp =
        context.Request.Headers["X-Real-IP"].FirstOrDefault();

    var hasForwardedFor =
        !string.IsNullOrWhiteSpace(
            context.Request.Headers["X-Forwarded-For"].ToString());

    if (peer is not null &&
        trustedProxyIps.Contains(NormalizeIp(peer)) &&
        !hasForwardedFor &&
        System.Net.IPAddress.TryParse(realIp, out var clientIp))
    {
        context.Request.Headers["X-Forwarded-For"] =
            clientIp.ToString();
    }

    await next();
});

app.UseForwardedHeaders(forwardedHeaders);

// Fail fast with a clear message when no database is configured.
if (app.Environment.IsProduction())
{
    DatabaseConnection.Resolve(app.Configuration);
}

// PostgreSQL: applies pending EF Core migrations (additive; never drops or
// recreates tables). SQLite (local development) has no migrations: its schema
// is only created when the file is empty. Set Database:MigrateOnStartup=false
// to disable.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    if (db.Database.IsNpgsql())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }
}

// ==================================================
// SWAGGER
// ==================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ==================================================
// HTTPS
// ==================================================

app.UseHttpsRedirection();

// ==================================================
// CORS
// ==================================================

app.UseRouting();
app.UseCors("AllowFrontend");

// ==================================================
// GLOBAL EXCEPTION HANDLER
// ==================================================

app.UseMiddleware<GlobalExceptionHandler>();

// ==================================================
// AUTHENTICATION
// ==================================================

app.UseAuthentication();

// ==================================================
// AUTHORIZATION
// ==================================================

app.UseAuthorization();

// ==================================================
// MAP CONTROLLERS
// ==================================================

app.MapControllers();

// ==================================================
// RUN APPLICATION
// ==================================================

app.Run();

// Exposed for WebApplicationFactory in integration tests
public partial class Program { }
