namespace VehicleRental.Core.Domain;

/// <summary>
/// Represents a vehicle type with its associated pricing formula.
/// </summary>
public sealed record VehicleTypeDefinition
{
    /// <summary>
    /// Gets the unique identifier for this vehicle type (e.g., "small-car").
    /// Should be lowercase and trimmed.
    /// </summary>
    public required string VehicleTypeId { get; init; }

    /// <summary>
    /// Gets the human-readable display name for this vehicle type (e.g., "Small Car").
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the pricing formula expression that will be evaluated at runtime.
    /// Formula supports: + - * / ( ), decimal literals, and variables: days, km, baseDayRate, baseKmPrice
    /// </summary>
    public required string PricingFormula { get; init; }

    /// <summary>
    /// Gets an optional description of this vehicle type.
    /// </summary>
    public string? Description { get; init; }
}
