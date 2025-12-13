namespace VehicleRental.Core.Application;

/// <summary>
/// Response from registering a vehicle return.
/// </summary>
public sealed record RegisterReturnResponse
{
    /// <summary>
    /// Gets the booking number of the completed rental.
    /// </summary>
    public required string BookingNumber { get; init; }

    /// <summary>
    /// Gets the number of days the vehicle was rented.
    /// </summary>
    public required int Days { get; init; }

    /// <summary>
    /// Gets the distance traveled in kilometers.
    /// </summary>
    public required decimal KilometersDriven { get; init; }

    /// <summary>
    /// Gets the final calculated rental price (rounded up to nearest whole unit).
    /// </summary>
    public required decimal RentalPrice { get; init; }
}
