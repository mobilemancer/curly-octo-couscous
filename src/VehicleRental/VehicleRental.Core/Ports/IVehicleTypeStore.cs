using VehicleRental.Core.Domain;

namespace VehicleRental.Core.Ports;

/// <summary>
/// Store for retrieving vehicle type definitions and their pricing formulas.
/// </summary>
public interface IVehicleTypeStore
{
    /// <summary>
    /// Retrieves a vehicle type definition by its unique identifier.
    /// </summary>
    /// <param name="vehicleTypeId">The vehicle type identifier (e.g., "small-car").</param>
    /// <returns>The vehicle type definition if found; otherwise, null.</returns>
    Task<VehicleTypeDefinition?> GetByIdAsync(string vehicleTypeId);

    /// <summary>
    /// Retrieves all available vehicle type definitions.
    /// </summary>
    /// <returns>A collection of all vehicle type definitions.</returns>
    Task<IReadOnlyCollection<VehicleTypeDefinition>> GetAllAsync();
}
