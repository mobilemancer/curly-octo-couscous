using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using VehicleRental.Core.Helpers;
using VehicleRental.Core.Ports;
using VehicleRental.Server.Hubs;
using VehicleRental.Shared.Contracts;

namespace VehicleRental.Server.Controllers;

/// <summary>
/// Controller for relaying vehicle operations to clients.
/// The server does NOT store vehicles - it only validates and relays to the appropriate client location.
/// Each client/location stores their own vehicle data locally.
/// </summary>
[ApiController]
[Route("api/vehicles")]
[Authorize] // All endpoints require authentication
public sealed class VehiclesController : ControllerBase
{
    private readonly IVehicleTypeStore _vehicleTypeStore;
    private readonly IHubContext<ConfigurationHub> _hubContext;
    private readonly ILogger<VehiclesController> _logger;

    public VehiclesController(
        IVehicleTypeStore vehicleTypeStore,
        IHubContext<ConfigurationHub> hubContext,
        ILogger<VehiclesController> logger)
    {
        _vehicleTypeStore = vehicleTypeStore;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Relays a new vehicle to a specific location.
    /// The server validates the vehicle type exists, then forwards via SignalR to the target location.
    /// Requires admin role.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RelayAddVehicle([FromBody] VehicleDto dto)
    {
        var clientId = User.FindFirst("client_id")?.Value ?? "unknown";
        _logger.LogInformation("Client {ClientId} relaying new vehicle: {RegistrationNumber} to location {Location}",
            clientId, dto.RegistrationNumber, dto.Location);

        // Validate input
        if (string.IsNullOrWhiteSpace(dto.RegistrationNumber))
        {
            return BadRequest(new { error = "Registration number is required" });
        }

        if (string.IsNullOrWhiteSpace(dto.VehicleTypeId))
        {
            return BadRequest(new { error = "Vehicle type ID is required" });
        }

        if (string.IsNullOrWhiteSpace(dto.Location))
        {
            return BadRequest(new { error = "Location is required" });
        }

        if (dto.CurrentOdometer < 0)
        {
            return BadRequest(new { error = "Odometer reading cannot be negative" });
        }

        // Validate that vehicle type exists (server knows about vehicle types)
        var vehicleType = await _vehicleTypeStore.GetByIdAsync(dto.VehicleTypeId);
        if (vehicleType == null)
        {
            return BadRequest(new { error = $"Vehicle type '{dto.VehicleTypeId}' does not exist" });
        }

        // Normalize the vehicle type ID and registration number
        var normalizedDto = new VehicleDto
        {
            RegistrationNumber = dto.RegistrationNumber.Trim().ToUpperInvariant(),
            VehicleTypeId = VehicleTypeIdNormalizer.Normalize(dto.VehicleTypeId),
            CurrentOdometer = dto.CurrentOdometer,
            Location = dto.Location.Trim()
        };

        // Relay to the target location via SignalR
        await NotifyVehicleUpdateAsync(VehicleUpdateType.Added, normalizedDto);

        _logger.LogInformation("Vehicle {RegistrationNumber} relayed to location {Location}",
            normalizedDto.RegistrationNumber, normalizedDto.Location);

        return Accepted(normalizedDto);
    }

    /// <summary>
    /// Relays a vehicle removal to a specific location.
    /// Requires admin role.
    /// </summary>
    [HttpDelete("{registrationNumber}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RelayRemoveVehicle(string registrationNumber, [FromQuery] string location)
    {
        var clientId = User.FindFirst("client_id")?.Value ?? "unknown";
        _logger.LogInformation("Client {ClientId} relaying vehicle removal: {RegistrationNumber} at location {Location}",
            clientId, registrationNumber, location);

        if (string.IsNullOrWhiteSpace(registrationNumber))
        {
            return BadRequest(new { error = "Registration number is required" });
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            return BadRequest(new { error = "Location is required (use ?location=xxx query parameter)" });
        }

        // Relay removal to the target location via SignalR
        await NotifyVehicleRemovalAsync(registrationNumber.Trim().ToUpperInvariant(), location.Trim());

        _logger.LogInformation("Vehicle removal {RegistrationNumber} relayed to location {Location}",
            registrationNumber, location);

        return Accepted();
    }

    /// <summary>
    /// Notifies clients about vehicle additions via SignalR.
    /// Only notifies clients at the specified location.
    /// </summary>
    private async Task NotifyVehicleUpdateAsync(VehicleUpdateType updateType, VehicleDto vehicle)
    {
        try
        {
            var notification = new VehicleUpdateNotification
            {
                UpdateType = updateType,
                Vehicle = vehicle,
                Location = vehicle.Location
            };

            // Send to location-specific group
            await _hubContext.Clients
                .Group($"Location:{vehicle.Location}")
                .SendAsync("VehicleUpdated", notification);

            _logger.LogInformation("Relayed vehicle {UpdateType} to clients at {Location}: {RegistrationNumber}",
                updateType, vehicle.Location, vehicle.RegistrationNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to relay vehicle update to clients");
        }
    }

    /// <summary>
    /// Notifies clients about vehicle removal via SignalR.
    /// Only notifies clients at the specified location.
    /// </summary>
    private async Task NotifyVehicleRemovalAsync(string registrationNumber, string location)
    {
        try
        {
            var notification = new VehicleUpdateNotification
            {
                UpdateType = VehicleUpdateType.Removed,
                RegistrationNumber = registrationNumber,
                Location = location
            };

            // Send to location-specific group
            await _hubContext.Clients
                .Group($"Location:{location}")
                .SendAsync("VehicleUpdated", notification);

            _logger.LogInformation("Relayed vehicle removal to clients at {Location}: {RegistrationNumber}",
                location, registrationNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to relay vehicle removal to clients");
        }
    }
}
