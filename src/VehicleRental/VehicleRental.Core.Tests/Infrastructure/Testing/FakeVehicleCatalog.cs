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
}
