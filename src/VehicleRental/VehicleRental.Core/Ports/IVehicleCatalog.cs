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
}
