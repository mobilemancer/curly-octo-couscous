namespace VehicleRental.Shared.Contracts;

/// <summary>
/// Data transfer object for vehicle type definitions.
/// Used for communication between server and clients.
/// </summary>
public sealed record VehicleTypeDto
{
    /// <summary>
    /// Unique identifier for the vehicle type (e.g., "small-car", "truck").
    /// Case-insensitive, normalized to lowercase.
    /// </summary>
    public required string VehicleTypeId { get; init; }

    /// <summary>
    /// Human-readable display name for the vehicle type.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Pricing formula expression supporting variables: days, km, baseDayRate, baseKmPrice.
    /// Example: "(baseDayRate * days * 1.5) + (baseKmPrice * km * 1.5)"
    /// </summary>
    public required string PricingFormula { get; init; }

    /// <summary>
    /// Optional description of the vehicle type.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Version number for tracking updates. Incremented on each modification.
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// Timestamp when this vehicle type was last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; init; }
}
