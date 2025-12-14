namespace VehicleRental.Core.Domain;

/// <summary>
/// Represents a physical vehicle in the rental fleet.
/// </summary>
public sealed record Vehicle
{
    /// <summary>
    /// Gets the unique registration number (license plate) of this vehicle.
    /// </summary>
    public required string RegistrationNumber { get; init; }

    /// <summary>
    /// Gets the vehicle type identifier that references a VehicleTypeDefinition.
    /// </summary>
    public required string VehicleTypeId { get; init; }

    /// <summary>
    /// Gets the current odometer reading in kilometers.
    /// </summary>
    public decimal CurrentOdometer { get; init; }

    /// <summary>
    /// Gets the location (client ID) where this vehicle is assigned.
    /// Corresponds to the clientId used for authentication.
    /// </summary>
    public required string Location { get; init; }
}
