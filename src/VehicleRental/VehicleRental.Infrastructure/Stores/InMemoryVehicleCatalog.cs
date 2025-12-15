using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Helpers;
using VehicleRental.Core.Ports;

namespace VehicleRental.Infrastructure.Stores;

/// <summary>
/// In-memory vehicle catalog loaded from JSON.
/// </summary>
public class InMemoryVehicleCatalog : IVehicleCatalog
{
    private readonly ConcurrentDictionary<string, Vehicle> _vehicles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<InMemoryVehicleCatalog> _logger;
    private readonly JsonSerializerOptions serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public InMemoryVehicleCatalog(string jsonFilePath, ILogger<InMemoryVehicleCatalog> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(jsonFilePath))
        {
            throw new ArgumentException("JSON file path cannot be null or whitespace.", nameof(jsonFilePath));
        }

        LoadFromFile(jsonFilePath);
    }

    public Task<Vehicle?> GetByRegistrationNumberAsync(string registrationNumber)
    {
        if (string.IsNullOrWhiteSpace(registrationNumber))
        {
            return Task.FromResult<Vehicle?>(null);
        }

        _vehicles.TryGetValue(registrationNumber, out var vehicle);
        return Task.FromResult(vehicle);
    }

    public Task<IReadOnlyCollection<Vehicle>> GetAllAsync()
    {
        var vehicles = _vehicles.Values.ToList();
        return Task.FromResult<IReadOnlyCollection<Vehicle>>(vehicles);
    }

    public Task<bool> UpdateOdometerAsync(string registrationNumber, decimal newOdometer)
    {
        if (string.IsNullOrWhiteSpace(registrationNumber))
        {
            return Task.FromResult(false);
        }

        if (_vehicles.TryGetValue(registrationNumber, out var existingVehicle))
        {
            var updatedVehicle = existingVehicle with { CurrentOdometer = newOdometer };
            _vehicles[registrationNumber] = updatedVehicle;
            _logger.LogInformation("Updated odometer for vehicle {RegistrationNumber} to {Odometer} km",
                registrationNumber, newOdometer);
            return Task.FromResult(true);
        }

        _logger.LogWarning("Vehicle {RegistrationNumber} not found for odometer update", registrationNumber);
        return Task.FromResult(false);
    }

    public Task<bool> AddVehicleAsync(Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        if (string.IsNullOrWhiteSpace(vehicle.RegistrationNumber))
        {
            _logger.LogWarning("Cannot add vehicle with empty registration number");
            return Task.FromResult(false);
        }

        var success = _vehicles.TryAdd(vehicle.RegistrationNumber, vehicle);

        if (success)
        {
            _logger.LogInformation("Added vehicle {RegistrationNumber} ({VehicleTypeId}) at {Location}",
                vehicle.RegistrationNumber, vehicle.VehicleTypeId, vehicle.Location);
        }
        else
        {
            _logger.LogWarning("Vehicle {RegistrationNumber} already exists in catalog",
                vehicle.RegistrationNumber);
        }

        return Task.FromResult(success);
    }

    public Task<bool> RemoveVehicleAsync(string registrationNumber)
    {
        if (string.IsNullOrWhiteSpace(registrationNumber))
        {
            return Task.FromResult(false);
        }

        var success = _vehicles.TryRemove(registrationNumber, out _);

        if (success)
        {
            _logger.LogInformation("Removed vehicle {RegistrationNumber} from catalog", registrationNumber);
        }
        else
        {
            _logger.LogWarning("Vehicle {RegistrationNumber} not found for removal", registrationNumber);
        }

        return Task.FromResult(success);
    }

    private void LoadFromFile(string filePath)
    {
        try
        {
            _logger.LogInformation("Loading vehicles from {FilePath}", filePath);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Vehicle catalog file not found: {FilePath}", filePath);
                return;
            }

            var json = File.ReadAllText(filePath);
        
            var vehicles = JsonSerializer.Deserialize<List<VehicleDto>>(json, serializerOptions);

            if (vehicles is null || vehicles.Count == 0)
            {
                _logger.LogWarning("No vehicles found in file: {FilePath}", filePath);
                return;
            }

            foreach (var dto in vehicles)
            {
                if (string.IsNullOrWhiteSpace(dto.RegistrationNumber))
                {
                    _logger.LogWarning("Skipping vehicle with empty registration number");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(dto.VehicleTypeId))
                {
                    _logger.LogWarning("Skipping vehicle {RegistrationNumber} with empty vehicle type ID",
                        dto.RegistrationNumber);
                    continue;
                }

                try
                {
                    var normalizedTypeId = VehicleTypeIdNormalizer.Normalize(dto.VehicleTypeId);

                    var vehicle = new Vehicle
                    {
                        RegistrationNumber = dto.RegistrationNumber.Trim(),
                        VehicleTypeId = normalizedTypeId,
                        CurrentOdometer = dto.CurrentOdometer,
                        Location = dto.Location ?? string.Empty
                    };

                    if (!_vehicles.TryAdd(vehicle.RegistrationNumber, vehicle))
                    {
                        _logger.LogWarning("Duplicate registration number found: {RegistrationNumber}",
                            vehicle.RegistrationNumber);
                    }
                }
                catch (ArgumentException ex)
                {
                    _logger.LogWarning(ex, "Invalid vehicle type ID for vehicle {RegistrationNumber}: {VehicleTypeId}",
                        dto.RegistrationNumber, dto.VehicleTypeId);
                }
            }

            _logger.LogInformation("Loaded {Count} vehicles from catalog", _vehicles.Count);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse vehicle catalog JSON from {FilePath}", filePath);
            throw new InvalidOperationException($"Invalid JSON in vehicle catalog file: {filePath}", ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to read vehicle catalog from {FilePath}", filePath);
            throw;
        }
    }

    private class VehicleDto
    {
        public string RegistrationNumber { get; set; } = string.Empty;
        public string VehicleTypeId { get; set; } = string.Empty;
        public decimal CurrentOdometer { get; set; }
        public string? Location { get; set; }
    }
}
