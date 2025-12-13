namespace VehicleRental.Core.Domain;

/// <summary>
/// Configuration parameters for pricing calculations.
/// </summary>
public sealed record PricingParameters
{
    /// <summary>
    /// Gets the base daily rate in the configured currency.
    /// </summary>
    public required decimal BaseDayRate { get; init; }

    /// <summary>
    /// Gets the base price per kilometer in the configured currency.
    /// </summary>
    public required decimal BaseKmPrice { get; init; }
}
