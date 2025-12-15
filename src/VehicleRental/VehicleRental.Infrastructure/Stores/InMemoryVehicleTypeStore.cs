using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Helpers;
using VehicleRental.Core.Ports;

namespace VehicleRental.Infrastructure.Stores;

/// <summary>
/// In-memory vehicle type store loaded from JSON.
/// </summary>
public class InMemoryVehicleTypeStore : IVehicleTypeStore
{
    private ConcurrentDictionary<string, VehicleTypeDefinition> _vehicleTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly IPriceFormulaEvaluator _formulaEvaluator;
    private readonly ILogger<InMemoryVehicleTypeStore> _logger;
    private readonly string _jsonFilePath;
    private readonly JsonSerializerOptions serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public InMemoryVehicleTypeStore(
        string jsonFilePath,
        IPriceFormulaEvaluator formulaEvaluator,
        ILogger<InMemoryVehicleTypeStore> logger)
    {
        _formulaEvaluator = formulaEvaluator ?? throw new ArgumentNullException(nameof(formulaEvaluator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(jsonFilePath))
        {
            throw new ArgumentException("JSON file path cannot be null or whitespace.", nameof(jsonFilePath));
        }

        _jsonFilePath = jsonFilePath;
        LoadFromFile();
    }

    public Task<VehicleTypeDefinition?> GetByIdAsync(string vehicleTypeId)
    {
        if (string.IsNullOrWhiteSpace(vehicleTypeId))
        {
            return Task.FromResult<VehicleTypeDefinition?>(null);
        }

        try
        {
            var normalized = VehicleTypeIdNormalizer.Normalize(vehicleTypeId);
            _vehicleTypes.TryGetValue(normalized, out var vehicleType);
            return Task.FromResult(vehicleType);
        }
        catch (ArgumentException)
        {
            return Task.FromResult<VehicleTypeDefinition?>(null);
        }
    }

    public Task<IReadOnlyCollection<VehicleTypeDefinition>> GetAllAsync()
    {
        IReadOnlyCollection<VehicleTypeDefinition> types = [.. _vehicleTypes.Values];
        return Task.FromResult(types);
    }

    /// <summary>
    /// Reloads vehicle types from the JSON file. Used for the reload command.
    /// </summary>
    public void Reload()
    {
        LoadFromFile();
    }

    private void LoadFromFile()
    {
        try
        {
            _logger.LogInformation("Loading vehicle types from {FilePath}", _jsonFilePath);

            if (!File.Exists(_jsonFilePath))
            {
                throw new FileNotFoundException($"Vehicle types file not found: {_jsonFilePath}");
            }

            var json = File.ReadAllText(_jsonFilePath);
          
            var dtos = JsonSerializer.Deserialize<List<VehicleTypeDto>>(json, serializerOptions);

            if (dtos is null || dtos.Count == 0)
            {
                throw new InvalidOperationException($"No vehicle types found in file: {_jsonFilePath}");
            }

            var newVehicleTypes = new ConcurrentDictionary<string, VehicleTypeDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var dto in dtos)
            {
                if (string.IsNullOrWhiteSpace(dto.VehicleTypeId))
                {
                    _logger.LogWarning("Skipping vehicle type with empty ID");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(dto.DisplayName))
                {
                    throw new InvalidOperationException($"Vehicle type {dto.VehicleTypeId} has no display name");
                }

                if (string.IsNullOrWhiteSpace(dto.PricingFormula))
                {
                    throw new InvalidOperationException($"Vehicle type {dto.VehicleTypeId} has no pricing formula");
                }

                try
                {
                    var normalizedTypeId = VehicleTypeIdNormalizer.Normalize(dto.VehicleTypeId);

                    // Validate formula by attempting to evaluate it with dummy variables
                    ValidateFormula(dto.PricingFormula);

                    var vehicleType = new VehicleTypeDefinition
                    {
                        VehicleTypeId = normalizedTypeId,
                        DisplayName = dto.DisplayName.Trim(),
                        PricingFormula = dto.PricingFormula.Trim(),
                        Description = dto.Description?.Trim()
                    };

                    if (!newVehicleTypes.TryAdd(vehicleType.VehicleTypeId, vehicleType))
                    {
                        throw new InvalidOperationException($"Duplicate vehicle type ID found: {vehicleType.VehicleTypeId}");
                    }
                }
                catch (ArgumentException ex)
                {
                    _logger.LogError(ex, "Invalid vehicle type ID: {VehicleTypeId}", dto.VehicleTypeId);
                    throw new InvalidOperationException($"Invalid vehicle type: {dto.VehicleTypeId}", ex);
                }
            }

            // Atomic swap - only replace if all types are valid
            _vehicleTypes = newVehicleTypes;

            _logger.LogInformation("Loaded {Count} vehicle types", _vehicleTypes.Count);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse vehicle types JSON from {FilePath}", _jsonFilePath);
            throw new InvalidOperationException($"Invalid JSON in vehicle types file: {_jsonFilePath}", ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to read vehicle types from {FilePath}", _jsonFilePath);
            throw;
        }
    }

    private void ValidateFormula(string formula)
    {
        // Test formula with dummy variables to ensure it's valid
        var testVariables = new Dictionary<string, decimal>
        {
            ["baseDayRate"] = 100,
            ["baseKmPrice"] = 1,
            ["days"] = 1,
            ["km"] = 1
        };

        try
        {
            _formulaEvaluator.Evaluate(formula, testVariables);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Invalid pricing formula: {formula}", ex);
        }
    }

    private class VehicleTypeDto
    {
        public string VehicleTypeId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PricingFormula { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
