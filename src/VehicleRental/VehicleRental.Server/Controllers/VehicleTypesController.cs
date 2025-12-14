using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VehicleRental.Core.Domain;
using VehicleRental.Server.Services;
using VehicleRental.Shared.Contracts;

namespace VehicleRental.Server.Controllers;

/// <summary>
/// Controller for managing vehicle type definitions.
/// </summary>
[ApiController]
[Route("api/vehicle-types")]
[Authorize] // All endpoints require authentication
public sealed class VehicleTypesController : ControllerBase
{
    private readonly ServerVehicleTypeStore _store;
    private readonly ILogger<VehicleTypesController> _logger;

    public VehicleTypesController(
        ServerVehicleTypeStore store,
        ILogger<VehicleTypesController> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Gets all vehicle type definitions.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<VehicleTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var clientId = User.FindFirst("client_id")?.Value ?? "unknown";
        _logger.LogInformation("Client {ClientId} requested all vehicle types", clientId);

        var types = await _store.GetAllAsDtosAsync();
        return Ok(types);
    }

    /// <summary>
    /// Gets a specific vehicle type by ID.
    /// </summary>
    [HttpGet("{vehicleTypeId}")]
    [ProducesResponseType(typeof(VehicleTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string vehicleTypeId)
    {
        var clientId = User.FindFirst("client_id")?.Value ?? "unknown";
        _logger.LogInformation("Client {ClientId} requested vehicle type: {VehicleTypeId}",
            clientId, vehicleTypeId);

        var type = await _store.GetByIdAsync(vehicleTypeId);

        if (type == null)
        {
            return NotFound(new { error = $"Vehicle type '{vehicleTypeId}' not found" });
        }

        var dto = new VehicleTypeDto
        {
            VehicleTypeId = type.VehicleTypeId,
            DisplayName = type.DisplayName,
            PricingFormula = type.PricingFormula,
            Description = type.Description,
            Version = _store.GetCurrentVersion(),
            LastUpdated = DateTimeOffset.UtcNow
        };

        return Ok(dto);
    }

    /// <summary>
    /// Creates or updates a vehicle type definition.
    /// Requires admin role.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(VehicleTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateOrUpdate([FromBody] VehicleTypeDto dto)
    {
        var clientId = User.FindFirst("client_id")?.Value ?? "unknown";
        _logger.LogInformation("Client {ClientId} creating/updating vehicle type: {VehicleTypeId}",
            clientId, dto.VehicleTypeId);

        var definition = new VehicleTypeDefinition
        {
            VehicleTypeId = dto.VehicleTypeId,
            DisplayName = dto.DisplayName,
            PricingFormula = dto.PricingFormula,
            Description = dto.Description
        };

        var result = await _store.AddOrUpdateAsync(definition);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        var responseDto = new VehicleTypeDto
        {
            VehicleTypeId = result.Value!.VehicleTypeId,
            DisplayName = result.Value.DisplayName,
            PricingFormula = result.Value.PricingFormula,
            Description = result.Value.Description,
            Version = _store.GetCurrentVersion(),
            LastUpdated = DateTimeOffset.UtcNow
        };

        return Ok(responseDto);
    }

    /// <summary>
    /// Deletes a vehicle type definition.
    /// Requires admin role.
    /// </summary>
    [HttpDelete("{vehicleTypeId}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(string vehicleTypeId)
    {
        var clientId = User.FindFirst("client_id")?.Value ?? "unknown";
        _logger.LogInformation("Client {ClientId} deleting vehicle type: {VehicleTypeId}",
            clientId, vehicleTypeId);

        var result = await _store.DeleteAsync(vehicleTypeId);

        if (!result.IsSuccess)
        {
            return NotFound(new { error = result.Error });
        }

        return NoContent();
    }

    /// <summary>
    /// Gets the current version number of vehicle types (for change detection).
    /// </summary>
    [HttpGet("version")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetVersion()
    {
        return Ok(new { version = _store.GetCurrentVersion() });
    }
}
