namespace VehicleRental.Core.Domain;

/// <summary>
/// Represents a vehicle rental transaction with checkout and optional return information.
/// </summary>
public sealed record Rental
{
    /// <summary>
    /// Gets the unique booking number for this rental.
    /// </summary>
    public required string BookingNumber { get; init; }

    /// <summary>
    /// Gets the customer ID associated with this rental.
    /// </summary>
    public required string CustomerId { get; init; }

    /// <summary>
    /// Gets the registration number of the rented vehicle.
    /// </summary>
    public required string RegistrationNumber { get; init; }

    /// <summary>
    /// Gets the vehicle type ID at the time of checkout.
    /// </summary>
    public required string VehicleTypeId { get; init; }

    /// <summary>
    /// Gets the checkout timestamp with timezone offset.
    /// </summary>
    public required DateTimeOffset CheckoutTimestamp { get; init; }

    /// <summary>
    /// Gets the odometer reading at checkout in kilometers.
    /// </summary>
    public required decimal CheckoutOdometer { get; init; }

    /// <summary>
    /// Gets the return timestamp with timezone offset. Null if vehicle not yet returned.
    /// </summary>
    public DateTimeOffset? ReturnTimestamp { get; init; }

    /// <summary>
    /// Gets the odometer reading at return in kilometers. Null if vehicle not yet returned.
    /// </summary>
    public decimal? ReturnOdometer { get; init; }

    /// <summary>
    /// Gets the calculated rental price. Null if vehicle not yet returned.
    /// </summary>
    public decimal? RentalPrice { get; init; }

    /// <summary>
    /// Gets whether this rental is currently active (vehicle checked out but not returned).
    /// </summary>
    public bool IsActive => ReturnTimestamp is null;
}
