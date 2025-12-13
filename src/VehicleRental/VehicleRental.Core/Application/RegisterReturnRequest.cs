namespace VehicleRental.Core.Application;

/// <summary>
/// Request to register a vehicle return.
/// </summary>
public sealed record RegisterReturnRequest
{
    /// <summary>
    /// Gets the booking number of the rental to complete.
    /// </summary>
    public required string BookingNumber { get; init; }

    /// <summary>
    /// Gets the return timestamp with timezone offset.
    /// </summary>
    public required DateTimeOffset ReturnTimestamp { get; init; }

    /// <summary>
    /// Gets the odometer reading at return in kilometers.
    /// </summary>
    public required decimal ReturnOdometer { get; init; }

    /// <summary>
    /// Gets the pricing parameters for calculating the rental cost.
    /// </summary>
    public required Domain.PricingParameters PricingParameters { get; init; }
}
