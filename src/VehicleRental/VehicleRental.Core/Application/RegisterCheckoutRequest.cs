namespace VehicleRental.Core.Application;

/// <summary>
/// Request to register a vehicle checkout.
/// </summary>
public sealed record RegisterCheckoutRequest
{
    /// <summary>
    /// Gets the unique booking number for this rental.
    /// </summary>
    public required string BookingNumber { get; init; }

    /// <summary>
    /// Gets the customer ID for this rental.
    /// </summary>
    public required string CustomerId { get; init; }

    /// <summary>
    /// Gets the registration number of the vehicle to checkout.
    /// </summary>
    public required string RegistrationNumber { get; init; }

    /// <summary>
    /// Gets the vehicle type ID (optional - will be resolved from vehicle catalog if not provided).
    /// </summary>
    public string? VehicleTypeId { get; init; }

    /// <summary>
    /// Gets the checkout timestamp with timezone offset.
    /// </summary>
    public required DateTimeOffset CheckoutTimestamp { get; init; }

    /// <summary>
    /// Gets the odometer reading at checkout in kilometers.
    /// </summary>
    public required decimal CheckoutOdometer { get; init; }
}
