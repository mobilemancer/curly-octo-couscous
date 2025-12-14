namespace VehicleRental.Shared.Contracts;

/// <summary>
/// Data transfer object for vehicle information.
/// </summary>
public sealed record VehicleDto
{
    /// <summary>
    /// Gets or initializes the unique registration number (license plate).
    /// </summary>
    public required string RegistrationNumber { get; init; }

    /// <summary>
    /// Gets or initializes the vehicle type identifier.
    /// </summary>
    public required string VehicleTypeId { get; init; }

    /// <summary>
    /// Gets or initializes the current odometer reading in kilometers.
    /// </summary>
    public decimal CurrentOdometer { get; init; }

    /// <summary>
    /// Gets or initializes the location (client ID) where this vehicle is assigned.
    /// </summary>
    public required string Location { get; init; }
}
