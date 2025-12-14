using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using VehicleRental.Core.Ports;
using VehicleRental.Core.Pricing;
using VehicleRental.Server.Hubs;
using VehicleRental.Server.Models;
using VehicleRental.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// =================================================================
// Configuration
// =================================================================

// Load accepted clients from configuration
var acceptedClients = builder.Configuration
    .GetSection("AcceptedClients")
    .Get<List<AcceptedClient>>() ?? new List<AcceptedClient>();

if (acceptedClients.Count == 0)
{
    throw new InvalidOperationException("No accepted clients configured. Add AcceptedClients section to appsettings.json");
}

// Load JWT settings
var jwtSettings = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtSettings>();

if (jwtSettings == null || string.IsNullOrWhiteSpace(jwtSettings.SecretKey))
{
    throw new InvalidOperationException("JWT settings not configured. Add Jwt section to appsettings.json");
}

// =================================================================
// Services Registration
// =================================================================

// Add controllers
builder.Services.AddControllers();

// Add SignalR
builder.Services.AddSignalR();

// Register Core services
builder.Services.AddSingleton<IPriceFormulaEvaluator, SafeFormulaEvaluator>();

// Register Server services
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(sp => acceptedClients.AsEnumerable());
builder.Services.AddSingleton<ClientAuthenticationService>();
builder.Services.AddSingleton<ServerVehicleTypeStore>();

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(1) // Reduce clock skew for better security
        };

        // Configure SignalR authentication
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

                var accessToken = context.Request.Query["access_token"];

                // If the request is for SignalR hub
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs/configuration"))
                {
                    context.Token = accessToken;
                    logger.LogDebug("Token extracted from query string for SignalR");
                }
                else
                {
                    // Check Authorization header
                    var authHeader = context.Request.Headers.Authorization.ToString();
                    logger.LogWarning("🔑 OnMessageReceived for {Path}: Auth header = '{Header}'",
                        path, string.IsNullOrEmpty(authHeader) ? "EMPTY" : authHeader);
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("JWT Token validated successfully for {User}", context.Principal?.Identity?.Name ?? "Unknown");
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(context.Exception, "JWT Authentication failed: {Message}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT Challenge triggered. Error: {Error}, ErrorDescription: {ErrorDescription}",
                    context.Error, context.ErrorDescription);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// CORS - Allow all origins for development (restrict in production)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// =================================================================
// Build Application
// =================================================================

var app = builder.Build();

// =================================================================
// Middleware Pipeline
// =================================================================

// HTTPS only for secure communication
app.UseHttpsRedirection();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map SignalR hub
app.MapHub<ConfigurationHub>("/hubs/configuration");

// =================================================================
// Startup Logging
// =================================================================

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Vehicle Rental Server starting...");
logger.LogInformation("Accepted clients: {Count}", acceptedClients.Count);
logger.LogInformation("JWT Issuer: {Issuer}", jwtSettings.Issuer);
logger.LogInformation("SignalR Hub available at: /hubs/configuration");

app.Run();
