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
}
