using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace VehicleRental.Server.Hubs;

/// <summary>
/// SignalR hub for pushing configuration updates to connected clients.
/// Implements best practices: authentication, logging, error handling.
/// </summary>
[Authorize]
public sealed class ConfigurationHub : Hub
{
    private readonly ILogger<ConfigurationHub> _logger;

    public ConfigurationHub(ILogger<ConfigurationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var clientId = Context.User?.FindFirst("client_id")?.Value ?? "unknown";
        var connectionId = Context.ConnectionId;

        _logger.LogInformation("Client connected to ConfigurationHub: {ClientId} (ConnectionId: {ConnectionId})",
            clientId, connectionId);

        // Add to global group for broadcasting
        await Groups.AddToGroupAsync(connectionId, "AllClients");

        // Add to location-specific group for vehicle updates
        await Groups.AddToGroupAsync(connectionId, $"Location:{clientId}");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var clientId = Context.User?.FindFirst("client_id")?.Value ?? "unknown";
        var connectionId = Context.ConnectionId;

        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ClientId} (ConnectionId: {ConnectionId})",
                clientId, connectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ClientId} (ConnectionId: {ConnectionId})",
                clientId, connectionId);
        }

        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client explicitly subscribes to updates (optional - automatic via group membership).
    /// </summary>
    public Task SubscribeToUpdates()
    {
        var clientId = Context.User?.FindFirst("client_id")?.Value ?? "unknown";
        _logger.LogInformation("Client subscribed to updates: {ClientId}", clientId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Health check endpoint for clients to verify connection.
    /// </summary>
    public Task<string> Ping()
    {
        return Task.FromResult("pong");
    }
}
