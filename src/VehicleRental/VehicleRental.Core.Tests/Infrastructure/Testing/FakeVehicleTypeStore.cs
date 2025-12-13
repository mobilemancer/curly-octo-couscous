using VehicleRental.Core.Domain;
using VehicleRental.Core.Helpers;
using VehicleRental.Core.Ports;

namespace VehicleRental.Core.Infrastructure.Testing;

public class FakeVehicleTypeStore : IVehicleTypeStore
{
    private readonly Dictionary<string, VehicleTypeDefinition> _vehicleTypes = new(StringComparer.OrdinalIgnoreCase);

    public void AddVehicleType(VehicleTypeDefinition vehicleType)
    {
        var normalizedId = VehicleTypeIdNormalizer.Normalize(vehicleType.VehicleTypeId);
        _vehicleTypes[normalizedId] = vehicleType;
    }

    public Task<VehicleTypeDefinition?> GetByIdAsync(string vehicleTypeId)
    {
        try
        {
            var normalizedId = VehicleTypeIdNormalizer.Normalize(vehicleTypeId);
            _vehicleTypes.TryGetValue(normalizedId, out var vehicleType);
            return Task.FromResult(vehicleType);
        }
        catch (ArgumentException)
        {
            return Task.FromResult<VehicleTypeDefinition?>(null);
        }
    }

    public Task<IReadOnlyCollection<VehicleTypeDefinition>> GetAllAsync()
    {
        IReadOnlyCollection<VehicleTypeDefinition> types = _vehicleTypes.Values.ToList();
        return Task.FromResult(types);
    }
}
