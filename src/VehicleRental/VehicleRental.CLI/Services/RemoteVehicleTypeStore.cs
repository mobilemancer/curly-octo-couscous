using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Helpers;
using VehicleRental.Core.Ports;
using VehicleRental.Shared.Contracts;

namespace VehicleRental.CLI.Services;

/// <summary>
/// Client-side vehicle type store that fetches data from the server
/// and receives real-time updates via SignalR.
/// Also handles vehicle update notifications for the local catalog.
/// </summary>
public class RemoteVehicleTypeStore : IVehicleTypeStore, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HubConnection _hubConnection;
    private readonly ILogger<RemoteVehicleTypeStore> _logger;
    private readonly Dictionary<string, VehicleTypeDefinition> _cache = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly string _clientId;
    private readonly string _apiKey;
    private bool _isInitialized;
    private bool _isConnected;
    private long _currentVersion;
    private Task? _backgroundConnectionTask;
    private string? _accessToken; // Store token explicitly

    /// <summary>
    /// Event fired when a vehicle update is received from the server.
    /// The local catalog should subscribe to this to add/remove vehicles.
    /// </summary>
    public event Action<VehicleUpdateNotification>? OnVehicleUpdated;

    public RemoteVehicleTypeStore(
        string serverBaseUrl,
        string clientId,
        string apiKey,
        ILogger<RemoteVehicleTypeStore> logger)
    {
        _logger = logger;
        _clientId = clientId;
        _apiKey = apiKey;

        // Create HttpClient with SSL certificate validation bypass for development
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;
        _httpClient = new HttpClient(handler) { BaseAddress = new Uri(serverBaseUrl) };

        // Configure SignalR connection
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{serverBaseUrl}/hubs/configuration", options =>
            {
                options.HttpMessageHandlerFactory = _ => handler;
                options.AccessTokenProvider = async () =>
                {
                    // Authenticate and get JWT token
                    var token = await AuthenticateAsync(clientId, apiKey);
                    return token;
                };
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        // Subscribe to push notifications - match server's method name
        _hubConnection.On<VehicleTypeUpdateNotification>("VehicleTypesUpdated", HandleVehicleTypeUpdate);

        // Subscribe to vehicle update notifications
        _hubConnection.On<VehicleUpdateNotification>("VehicleUpdated", HandleVehicleUpdate);

        _hubConnection.Reconnecting += error =>
        {
            _isConnected = false;
            _logger.LogWarning("SignalR connection lost. Reconnecting... Error: {Error}", error?.Message);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _isConnected = true;
            _logger.LogInformation("SignalR reconnected. Connection ID: {ConnectionId}", connectionId);
            return ReloadAllAsync(); // Reload all data after reconnection
        };

        _hubConnection.Closed += error =>
        {
            _isConnected = false;
            _logger.LogWarning("SignalR connection closed. Error: {Error}", error?.Message);
            // Start background reconnection task
            _backgroundConnectionTask = Task.Run(async () => await TryConnectInBackgroundAsync());
            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// Initialize the store by connecting to server and loading initial data.
    /// Starts in background if server is unavailable.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        // Use a simple flag check without locking for initialization
        // The lock in ReloadAllAsync will protect the cache during updates
        if (_isInitialized)
            return;

        _logger.LogInformation("Initializing RemoteVehicleTypeStore...");
        _isInitialized = true;

        // Try to connect, but don't fail if server is unavailable
        await TryConnectAsync();
    }

    private async Task TryConnectAsync()
    {
        try
        {
            _logger.LogInformation("Attempting to connect to server...");

            // Authenticate first to get JWT token
            var token = await AuthenticateAsync(_clientId, _apiKey);
            _logger.LogInformation("Authentication successful");

            // Load initial data (now with valid auth token)
            await ReloadAllAsync();

            // Mark as connected after successful data load - API is working
            _isConnected = true;
            _logger.LogInformation("HTTP API connection successful, data loaded");

            // Try to connect to SignalR hub for real-time updates
            try
            {
                await _hubConnection.StartAsync();
                _logger.LogInformation("SignalR connection established");

                // Subscribe to updates
                await _hubConnection.InvokeAsync("SubscribeToUpdates");
                _logger.LogInformation("Subscribed to vehicle type updates");
            }
            catch (Exception signalREx)
            {
                // SignalR failed but HTTP API works - continue without real-time updates
                _logger.LogWarning(signalREx, "SignalR connection failed, continuing with HTTP API only");
            }
        }
        catch (Exception ex)
        {
            _isConnected = false;
            _logger.LogWarning(ex, "Failed to connect to server. Will retry in background.");

            // Start background reconnection task
            _backgroundConnectionTask = Task.Run(async () => await TryConnectInBackgroundAsync());
        }
    }

    private async Task TryConnectInBackgroundAsync()
    {
        while (!_isConnected)
        {
            await Task.Delay(TimeSpan.FromSeconds(10));

            try
            {
                _logger.LogInformation("Background reconnection attempt...");

                // Authenticate first
                var token = await AuthenticateAsync(_clientId, _apiKey);

                // Try to reload data
                await ReloadAllAsync();

                // Try to connect SignalR if not connected
                if (_hubConnection.State == HubConnectionState.Disconnected)
                {
                    await _hubConnection.StartAsync();
                    await _hubConnection.InvokeAsync("SubscribeToUpdates");
                }

                _isConnected = true;
                _logger.LogInformation("Successfully reconnected to server");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Reconnection attempt failed, will retry...");
                // Continue retrying
            }
        }
    }

    private async Task<string> AuthenticateAsync(string clientId, string apiKey)
    {
        var request = new ClientAuthRequest
        {
            ClientId = clientId,
            ApiKey = apiKey
        };

        var response = await _httpClient.PostAsJsonAsync("/api/clients/authenticate", request);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Authentication failed: {StatusCode}", response.StatusCode);
            throw new InvalidOperationException("Authentication failed");
        }

        var authResponse = await response.Content.ReadFromJsonAsync<ClientAuthResponse>();
        if (authResponse?.AccessToken == null || !authResponse.Authenticated)
        {
            _logger.LogError("Authentication response missing token or not authenticated: {ErrorMessage}", authResponse?.ErrorMessage);
            throw new InvalidOperationException($"Authentication failed: {authResponse?.ErrorMessage}");
        }

        _logger.LogInformation("Authenticated successfully as {ClientName}. Token expires in {Seconds}s",
            authResponse.ClientName, authResponse.ExpiresInSeconds);

        // Store token for use in API requests
        _accessToken = authResponse.AccessToken;

        // Also set on DefaultRequestHeaders (for reference, but we'll use explicit headers)
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);

        _logger.LogDebug("Authorization header set on HttpClient. Token starts with: {TokenPrefix}...",
            authResponse.AccessToken.Substring(0, Math.Min(20, authResponse.AccessToken.Length)));

        return authResponse.AccessToken;
    }

    private async Task ReloadAllAsync()
    {
        _logger.LogInformation("Reloading all vehicle types from server...");

        // Use HttpRequestMessage with explicit Authorization header
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/vehicle-types");
        if (_accessToken != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }
        else
        {
            _logger.LogWarning("No access token available - request will fail");
        }

        var response = await _httpClient.SendAsync(request);
        _logger.LogInformation("Received response with status {StatusCode}", response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("Request to /api/vehicle-types failed with status {StatusCode}. Response: {Response}",
                response.StatusCode, errorBody);
            throw new HttpRequestException($"Request failed with status {response.StatusCode}: {errorBody}");
        }

        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Parsing JSON response...");
        var dtos = await response.Content.ReadFromJsonAsync<List<VehicleTypeDto>>();
        _logger.LogInformation("Parsed {Count} vehicle types from response", dtos?.Count ?? 0);

        if (dtos == null)
        {
            _logger.LogWarning("Server returned null vehicle types list");
            return;
        }

        _logger.LogInformation("Acquiring sync lock to update cache...");
        await _syncLock.WaitAsync();
        try
        {
            _logger.LogInformation("Updating cache with {Count} vehicle types", dtos.Count);
            _cache.Clear();
            foreach (var dto in dtos)
            {
                var vehicleTypeDef = MapFromDto(dto);
                _cache[vehicleTypeDef.VehicleTypeId] = vehicleTypeDef;
            }

            // Update version from response headers if available
            if (response.Headers.TryGetValues("X-Version", out var versions))
            {
                if (long.TryParse(versions.First(), out var version))
                {
                    _currentVersion = version;
                }
            }

            _logger.LogInformation("Loaded {Count} vehicle types (version {Version})",
                _cache.Count, _currentVersion);
        }
        finally
        {
            _logger.LogInformation("Releasing sync lock");
            _syncLock.Release();
        }

        _logger.LogInformation("ReloadAllAsync completed successfully");
    }

    private async void HandleVehicleTypeUpdate(VehicleTypeUpdateNotification notification)
    {
        _logger.LogInformation("Received update notification: {Type} - {Count} types affected (new version: {Version})",
            notification.UpdateType, notification.AffectedTypeIds.Count, notification.NewVersion);

        try
        {
            // Reload all data - ReloadAllAsync has its own locking mechanism
            _logger.LogInformation("Reloading data due to update notification");
            await ReloadAllAsync();

            _currentVersion = notification.NewVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling vehicle type update");
        }
    }

    private void HandleVehicleUpdate(VehicleUpdateNotification notification)
    {
        _logger.LogInformation("Received vehicle update: {UpdateType} for {Registration} at {Location}",
            notification.UpdateType,
            notification.Vehicle?.RegistrationNumber ?? notification.RegistrationNumber,
            notification.Location);

        try
        {
            // Raise event for local catalog to handle
            OnVehicleUpdated?.Invoke(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling vehicle update notification");
        }
    }

    private static VehicleTypeDefinition MapFromDto(VehicleTypeDto dto)
    {
        return new VehicleTypeDefinition
        {
            VehicleTypeId = dto.VehicleTypeId,
            DisplayName = dto.DisplayName,
            PricingFormula = dto.PricingFormula,
            Description = dto.Description
        };
    }

    // IVehicleTypeStore implementation

    public async Task<VehicleTypeDefinition?> GetByIdAsync(string vehicleTypeId)
    {
        if (!_isInitialized)
            await InitializeAsync();

        if (!_isConnected)
        {
            _logger.LogWarning("Not connected to server, returning null for {VehicleTypeId}", vehicleTypeId);
            return null;
        }

        await _syncLock.WaitAsync();
        try
        {
            return _cache.TryGetValue(vehicleTypeId, out var vehicleType) ? vehicleType : null;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<IReadOnlyCollection<VehicleTypeDefinition>> GetAllAsync()
    {
        if (!_isInitialized)
            await InitializeAsync();

        if (!_isConnected)
        {
            _logger.LogWarning("Not connected to server, returning empty list");
            return Array.Empty<VehicleTypeDefinition>();
        }

        await _syncLock.WaitAsync();
        try
        {
            return _cache.Values.ToList();
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _hubConnection.DisposeAsync();
        _httpClient.Dispose();
        _syncLock.Dispose();
    }
}
