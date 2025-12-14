using VehicleRental.Core.Domain;

namespace VehicleRental.Core.Ports;

/// <summary>
/// Catalog for looking up vehicle information.
/// </summary>
public interface IVehicleCatalog
{
    /// <summary>
    /// Looks up a vehicle by its registration number.
    /// </summary>
    /// <param name="registrationNumber">The registration number (license plate) to search for.</param>
    /// <returns>The vehicle if found; otherwise, null.</returns>
    Task<Vehicle?> GetByRegistrationNumberAsync(string registrationNumber);

    /// <summary>
    /// Gets all vehicles in the catalog.
    /// </summary>
    /// <returns>A collection of all vehicles.</returns>
    Task<IReadOnlyCollection<Vehicle>> GetAllAsync();

    /// <summary>
    /// Updates the odometer reading for a vehicle.
    /// </summary>
    /// <param name="registrationNumber">The registration number of the vehicle to update.</param>
    /// <param name="newOdometer">The new odometer reading in kilometers.</param>
    /// <returns>True if the vehicle was found and updated; otherwise, false.</returns>
    Task<bool> UpdateOdometerAsync(string registrationNumber, decimal newOdometer);

    /// <summary>
    /// Adds a new vehicle to the catalog.
    /// </summary>
    /// <param name="vehicle">The vehicle to add.</param>
    /// <returns>True if the vehicle was added successfully; false if a vehicle with the same registration number already exists.</returns>
    Task<bool> AddVehicleAsync(Vehicle vehicle);

    /// <summary>
    /// Removes a vehicle from the catalog.
    /// </summary>
    /// <param name="registrationNumber">The registration number of the vehicle to remove.</param>
    /// <returns>True if the vehicle was found and removed; otherwise, false.</returns>
    Task<bool> RemoveVehicleAsync(string registrationNumber);
}
