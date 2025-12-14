using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Helpers;
using VehicleRental.Core.Ports;
using VehicleRental.Server.Hubs;
using VehicleRental.Shared.Contracts;

namespace VehicleRental.Server.Services;

/// <summary>
/// Server-side implementation of vehicle type store with push notification support.
/// Thread-safe, authoritative source for vehicle type definitions.
/// </summary>
public sealed class ServerVehicleTypeStore : IVehicleTypeStore
{
    private readonly ConcurrentDictionary<string, VehicleTypeDefinition> _types;
    private readonly IPriceFormulaEvaluator _formulaEvaluator;
    private readonly IHubContext<ConfigurationHub> _hubContext;
    private readonly ILogger<ServerVehicleTypeStore> _logger;
    private int _currentVersion = 1;

    public ServerVehicleTypeStore(
        IPriceFormulaEvaluator formulaEvaluator,
        IHubContext<ConfigurationHub> hubContext,
        ILogger<ServerVehicleTypeStore> logger)
    {
        _types = new ConcurrentDictionary<string, VehicleTypeDefinition>(StringComparer.OrdinalIgnoreCase);
        _formulaEvaluator = formulaEvaluator;
        _hubContext = hubContext;
        _logger = logger;

        // Seed with initial vehicle types
        SeedInitialTypes();
    }

    private void SeedInitialTypes()
    {
        var initialTypes = new[]
        {
            new VehicleTypeDefinition
            {
                VehicleTypeId = "small-car",
                DisplayName = "Small Car",
                PricingFormula = "baseDayRate * days",
                Description = "Compact vehicles for city driving"
            },
            new VehicleTypeDefinition
            {
                VehicleTypeId = "station-wagon",
                DisplayName = "Station Wagon",
                PricingFormula = "(baseDayRate * days * 1.3) + (baseKmPrice * km)",
                Description = "Family vehicles with extra cargo space"
            },
            new VehicleTypeDefinition
            {
                VehicleTypeId = "truck",
                DisplayName = "Truck",
                PricingFormula = "(baseDayRate * days * 1.5) + (baseKmPrice * km * 1.5)",
                Description = "Heavy-duty vehicles for transport"
            }
        };

        foreach (var type in initialTypes)
        {
            _types[VehicleTypeIdNormalizer.Normalize(type.VehicleTypeId)] = type;
        }

        _logger.LogInformation("Seeded {Count} initial vehicle types", initialTypes.Length);
    }

    public Task<VehicleTypeDefinition?> GetByIdAsync(string vehicleTypeId)
    {
        var normalizedId = VehicleTypeIdNormalizer.Normalize(vehicleTypeId);
        _types.TryGetValue(normalizedId, out var type);
        return Task.FromResult(type);
    }

    public Task<IReadOnlyCollection<VehicleTypeDefinition>> GetAllAsync()
    {
        var types = _types.Values.ToList();
        return Task.FromResult<IReadOnlyCollection<VehicleTypeDefinition>>(types);
    }

    /// <summary>
    /// Adds or updates a vehicle type definition.
    /// Validates formula and notifies connected clients via SignalR.
    /// </summary>
    public async Task<Result<VehicleTypeDefinition>> AddOrUpdateAsync(VehicleTypeDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.VehicleTypeId))
        {
            return Result<VehicleTypeDefinition>.Failure("VehicleTypeId is required");
        }

        if (string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            return Result<VehicleTypeDefinition>.Failure("DisplayName is required");
        }

        if (string.IsNullOrWhiteSpace(definition.PricingFormula))
        {
            return Result<VehicleTypeDefinition>.Failure("PricingFormula is required");
        }

        var normalizedId = VehicleTypeIdNormalizer.Normalize(definition.VehicleTypeId);

        // Validate formula before accepting
        try
        {
            var variables = new Dictionary<string, decimal>
            {
                ["baseDayRate"] = 100m,
                ["baseKmPrice"] = 0.5m,
                ["days"] = 1m,
                ["km"] = 0m
            };
            _formulaEvaluator.Evaluate(definition.PricingFormula, variables);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid pricing formula for vehicle type {VehicleTypeId}: {Formula}",
                normalizedId, definition.PricingFormula);
            return Result<VehicleTypeDefinition>.Failure($"Invalid pricing formula: {ex.Message}");
        }

        var isUpdate = _types.ContainsKey(normalizedId);
        var normalizedDefinition = definition with { VehicleTypeId = normalizedId };

        _types[normalizedId] = normalizedDefinition;
        _currentVersion++;

        _logger.LogInformation("{Action} vehicle type: {VehicleTypeId} (version {Version})",
            isUpdate ? "Updated" : "Added", normalizedId, _currentVersion);

        // Notify all connected clients
        await NotifyClientsAsync(
            isUpdate ? UpdateType.Modified : UpdateType.Added,
            new[] { normalizedId }
        );

        return Result<VehicleTypeDefinition>.Success(normalizedDefinition);
    }

    /// <summary>
    /// Deletes a vehicle type.
    /// Notifies connected clients via SignalR.
    /// </summary>
    public async Task<Result<bool>> DeleteAsync(string vehicleTypeId)
    {
        var normalizedId = VehicleTypeIdNormalizer.Normalize(vehicleTypeId);

        if (!_types.TryRemove(normalizedId, out _))
        {
            return Result<bool>.Failure($"Vehicle type '{normalizedId}' not found");
        }

        _currentVersion++;

        _logger.LogInformation("Deleted vehicle type: {VehicleTypeId} (version {Version})",
            normalizedId, _currentVersion);

        // Notify all connected clients
        await NotifyClientsAsync(UpdateType.Deleted, new[] { normalizedId });

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Gets the current version number (for change tracking).
    /// </summary>
    public int GetCurrentVersion() => _currentVersion;

    /// <summary>
    /// Converts internal domain models to DTOs for client communication.
    /// </summary>
    public Task<List<VehicleTypeDto>> GetAllAsDtosAsync()
    {
        var dtos = _types.Values.Select(ToDto).ToList();
        return Task.FromResult(dtos);
    }

    /// <summary>
    /// Notifies all connected clients about vehicle type changes via SignalR.
    /// </summary>
    private async Task NotifyClientsAsync(UpdateType updateType, string[] affectedTypeIds)
    {
        var notification = new VehicleTypeUpdateNotification
        {
            UpdateType = updateType,
            Timestamp = DateTimeOffset.UtcNow,
            AffectedTypeIds = affectedTypeIds.ToList(),
            NewVersion = _currentVersion,
            Message = $"{updateType} {affectedTypeIds.Length} vehicle type(s)"
        };

        try
        {
            await _hubContext.Clients.All.SendAsync("VehicleTypesUpdated", notification);
            _logger.LogInformation("Notified clients about {UpdateType} for types: {Types}",
                updateType, string.Join(", ", affectedTypeIds));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify clients about vehicle type update");
        }
    }

    private VehicleTypeDto ToDto(VehicleTypeDefinition definition)
    {
        return new VehicleTypeDto
        {
            VehicleTypeId = definition.VehicleTypeId,
            DisplayName = definition.DisplayName,
            PricingFormula = definition.PricingFormula,
            Description = definition.Description,
            Version = _currentVersion,
            LastUpdated = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// Result type for operations that can fail with an error message.
/// </summary>
public sealed record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }

    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(string error) => new() { IsSuccess = false, Error = error };
}
