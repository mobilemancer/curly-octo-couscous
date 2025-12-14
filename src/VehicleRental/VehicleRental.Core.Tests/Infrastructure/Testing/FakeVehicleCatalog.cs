using VehicleRental.Core.Domain;
using VehicleRental.Core.Ports;

namespace VehicleRental.Core.Infrastructure.Testing;

public class FakeVehicleCatalog : IVehicleCatalog
{
    private readonly Dictionary<string, Vehicle> _vehicles = new(StringComparer.OrdinalIgnoreCase);

    public void AddVehicle(Vehicle vehicle)
    {
        _vehicles[vehicle.RegistrationNumber] = vehicle;
    }

    public Task<Vehicle?> GetByRegistrationNumberAsync(string registrationNumber)
    {
        _vehicles.TryGetValue(registrationNumber, out var vehicle);
        return Task.FromResult(vehicle);
    }

    public Task<IReadOnlyCollection<Vehicle>> GetAllAsync()
    {
        IReadOnlyCollection<Vehicle> vehicles = _vehicles.Values.ToList();
        return Task.FromResult(vehicles);
    }

    public Task<bool> UpdateOdometerAsync(string registrationNumber, decimal newOdometer)
    {
        if (_vehicles.TryGetValue(registrationNumber, out var vehicle))
        {
            // Create a new vehicle record with the updated odometer
            var updatedVehicle = vehicle with { CurrentOdometer = newOdometer };
            _vehicles[registrationNumber] = updatedVehicle;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
