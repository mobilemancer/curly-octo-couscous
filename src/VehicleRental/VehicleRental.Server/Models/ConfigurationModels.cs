namespace VehicleRental.Server.Models;

/// <summary>
/// Configuration for an accepted client that can connect to the server.
/// </summary>
public sealed record AcceptedClient
{
    /// <summary>
    /// Unique identifier for the client (e.g., "location-stockholm-001").
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Human-readable name for the client location.
    /// </summary>
    public required string ClientName { get; init; }

    /// <summary>
    /// Secret API key for authentication.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Whether this client is currently enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Whether this client has admin privileges (can modify vehicle types).
    /// </summary>
    public bool IsAdmin { get; init; } = false;
}

/// <summary>
/// JWT configuration settings.
/// </summary>
public sealed record JwtSettings
{
    /// <summary>
    /// Secret key for signing JWT tokens (minimum 32 characters for HS256).
    /// </summary>
    public required string SecretKey { get; init; }

    /// <summary>
    /// Token issuer (typically the server URL).
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>
    /// Token audience (typically "VehicleRental.Clients").
    /// </summary>
    public required string Audience { get; init; }

    /// <summary>
    /// Token expiration time in minutes.
    /// </summary>
    public int ExpirationMinutes { get; init; } = 60;
}
