namespace VehicleRental.Core.Application;

/// <summary>
/// Response from registering a vehicle checkout.
/// </summary>
public sealed record RegisterCheckoutResponse
{
    /// <summary>
    /// Gets the booking number of the created rental.
    /// </summary>
    public required string BookingNumber { get; init; }

    /// <summary>
    /// Gets the registration number of the checked-out vehicle.
    /// </summary>
    public required string RegistrationNumber { get; init; }

    /// <summary>
    /// Gets the vehicle type ID for the rental.
    /// </summary>
    public required string VehicleTypeId { get; init; }

    /// <summary>
    /// Gets the checkout timestamp.
    /// </summary>
    public required DateTimeOffset CheckoutTimestamp { get; init; }
}
