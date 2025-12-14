using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using VehicleRental.Server.Models;
using VehicleRental.Shared.Contracts;

namespace VehicleRental.Server.Services;

/// <summary>
/// Service for authenticating clients and issuing JWT tokens.
/// Implements best practices for JWT authentication.
/// </summary>
public sealed class ClientAuthenticationService
{
    private readonly List<AcceptedClient> _acceptedClients;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<ClientAuthenticationService> _logger;

    public ClientAuthenticationService(
        IEnumerable<AcceptedClient> acceptedClients,
        JwtSettings jwtSettings,
        ILogger<ClientAuthenticationService> logger)
    {
        _acceptedClients = acceptedClients.ToList();
        _jwtSettings = jwtSettings;
        _logger = logger;

        // Validate JWT settings on startup
        if (string.IsNullOrWhiteSpace(_jwtSettings.SecretKey))
            throw new InvalidOperationException("JWT SecretKey is required");

        if (_jwtSettings.SecretKey.Length < 32)
            throw new InvalidOperationException("JWT SecretKey must be at least 32 characters for HS256");

        _logger.LogInformation("ClientAuthenticationService initialized with {Count} accepted clients",
            _acceptedClients.Count);
    }

    /// <summary>
    /// Authenticates a client and issues a JWT token if credentials are valid.
    /// </summary>
    public ClientAuthResponse Authenticate(ClientAuthRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ApiKey))
        {
            _logger.LogWarning("Authentication attempt with missing credentials");
            return new ClientAuthResponse
            {
                Authenticated = false,
                ErrorMessage = "ClientId and ApiKey are required"
            };
        }

        // Find client (case-insensitive clientId)
        var client = _acceptedClients.FirstOrDefault(c =>
            string.Equals(c.ClientId, request.ClientId, StringComparison.OrdinalIgnoreCase));

        if (client == null)
        {
            _logger.LogWarning("Authentication attempt with unknown client ID: {ClientId}", request.ClientId);
            return new ClientAuthResponse
            {
                Authenticated = false,
                ErrorMessage = "Invalid credentials"
            };
        }

        // Check if client is enabled
        if (!client.Enabled)
        {
            _logger.LogWarning("Authentication attempt by disabled client: {ClientId}", request.ClientId);
            return new ClientAuthResponse
            {
                Authenticated = false,
                ErrorMessage = "Client is disabled"
            };
        }

        // Verify API key (constant-time comparison to prevent timing attacks)
        if (!SlowEquals(client.ApiKey, request.ApiKey))
        {
            _logger.LogWarning("Authentication attempt with invalid API key for client: {ClientId}", request.ClientId);
            return new ClientAuthResponse
            {
                Authenticated = false,
                ErrorMessage = "Invalid credentials"
            };
        }

        // Generate JWT token
        var token = GenerateJwtToken(client);

        _logger.LogInformation("Client authenticated successfully: {ClientId} ({ClientName})",
            client.ClientId, client.ClientName);

        return new ClientAuthResponse
        {
            Authenticated = true,
            ClientName = client.ClientName,
            AccessToken = token,
            ExpiresInSeconds = _jwtSettings.ExpirationMinutes * 60
        };
    }

    /// <summary>
    /// Generates a JWT token for an authenticated client.
    /// </summary>
    private string GenerateJwtToken(AcceptedClient client)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, client.ClientId),
            new(ClaimTypes.Name, client.ClientName),
            new("client_id", client.ClientId)
        };

        // Add admin role if applicable
        if (client.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Constant-time string comparison to prevent timing attacks.
    /// Best practice for comparing secrets.
    /// </summary>
    private static bool SlowEquals(string a, string b)
    {
        if (a == null || b == null || a.Length != b.Length)
            return false;

        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }

        return diff == 0;
    }
}
