namespace VehicleRental.CLI.Configuration;

/// <summary>
/// Configuration settings for connecting to the Vehicle Rental Server.
/// </summary>
public sealed record ServerConfiguration
{
    /// <summary>
    /// Base URL of the Vehicle Rental Server (e.g., "https://localhost:5001").
    /// </summary>
    public required string BaseUrl { get; init; }

    /// <summary>
    /// Client ID for authentication.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// API key for authentication.
    /// </summary>
    public required string ApiKey { get; init; }
}
