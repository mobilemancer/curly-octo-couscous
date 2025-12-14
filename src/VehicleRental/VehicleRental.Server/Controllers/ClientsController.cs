using Microsoft.AspNetCore.Mvc;
using VehicleRental.Server.Services;
using VehicleRental.Shared.Contracts;

namespace VehicleRental.Server.Controllers;

/// <summary>
/// Controller for client authentication.
/// </summary>
[ApiController]
[Route("api/clients")]
public sealed class ClientsController : ControllerBase
{
    private readonly ClientAuthenticationService _authService;
    private readonly ILogger<ClientsController> _logger;

    public ClientsController(
        ClientAuthenticationService authService,
        ILogger<ClientsController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a client and issues a JWT token.
    /// </summary>
    /// <param name="request">Authentication request containing clientId and apiKey.</param>
    /// <returns>Authentication response with JWT token if successful.</returns>
    [HttpPost("authenticate")]
    [ProducesResponseType(typeof(ClientAuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ClientAuthResponse), StatusCodes.Status401Unauthorized)]
    public IActionResult Authenticate([FromBody] ClientAuthRequest request)
    {
        _logger.LogInformation("Authentication attempt for client: {ClientId}", request.ClientId);

        var response = _authService.Authenticate(request);

        if (!response.Authenticated)
        {
            return Unauthorized(response);
        }

        return Ok(response);
    }
}
