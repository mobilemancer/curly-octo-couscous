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
            var vehicles = JsonSerializer.Deserialize<List<VehicleDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

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
                        CurrentOdometer = dto.CurrentOdometer
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
    }
}
