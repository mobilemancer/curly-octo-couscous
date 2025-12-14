namespace VehicleRental.Shared.Contracts;

/// <summary>
/// Request for client authentication.
/// </summary>
public sealed record ClientAuthRequest
{
    /// <summary>
    /// Unique identifier for the client/rental location (e.g., "location-stockholm-001").
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Secret API key for authentication.
    /// </summary>
    public required string ApiKey { get; init; }
}

/// <summary>
/// Response from authentication attempt.
/// </summary>
public sealed record ClientAuthResponse
{
    /// <summary>
    /// Indicates whether authentication was successful.
    /// </summary>
    public required bool Authenticated { get; init; }

    /// <summary>
    /// Human-readable name of the authenticated client.
    /// </summary>
    public string? ClientName { get; init; }

    /// <summary>
    /// JWT access token for subsequent API requests.
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// Token expiration time in seconds.
    /// </summary>
    public int? ExpiresInSeconds { get; init; }

    /// <summary>
    /// Reason for authentication failure (if any).
    /// </summary>
    public string? ErrorMessage { get; init; }
}
