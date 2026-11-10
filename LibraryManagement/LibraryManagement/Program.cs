
using LibraryManagement.Data;
using LibraryManagement.Exceptions;
using LibraryManagement.Helpers;
using LibraryManagement.Repositories.Implementations;
using LibraryManagement.Repositories.Interfaces;
using LibraryManagement.Services.Implementations;
using LibraryManagement.Services.Interfaces;
using LibraryManagement.Realtime;

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
                "https://astounding-quokka-a67be2.netlify.app")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ==================================================
// DATABASE
// ==================================================

var databaseDirectory = Path.Combine(
    builder.Environment.ContentRootPath, "Data");

Directory.CreateDirectory(databaseDirectory);

var configuredConnectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is missing.");

var sqliteConnectionString =
    new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(
        configuredConnectionString);

if (!Path.IsPathRooted(sqliteConnectionString.DataSource))
{
    sqliteConnectionString.DataSource = Path.GetFullPath(
        sqliteConnectionString.DataSource,
        builder.Environment.ContentRootPath);
}

sqliteConnectionString.ForeignKeys = true;
sqliteConnectionString.DefaultTimeout = 30;

builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new SqliteConnectionFactory(sqliteConnectionString.ToString()));

builder.Services.AddScoped<IDapperUnitOfWork, DapperUnitOfWork>();

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

await BookPreviewContentInitializer.InitializeAsync(
    app.Services.GetRequiredService<IDbConnectionFactory>(),
    app.Environment.ContentRootPath);

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
