using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Ports;
using VehicleRental.Shared.Contracts;

namespace VehicleRental.DevTools.CLI.Services;

/// <summary>
/// Client-side vehicle type store that fetches data from the server
/// and receives real-time updates via SignalR.
/// </summary>
public class RemoteVehicleTypeStore : IVehicleTypeStore, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HubConnection _hubConnection;
    private readonly ILogger<RemoteVehicleTypeStore> _logger;
    private readonly Dictionary<string, VehicleTypeDefinition> _cache = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _isInitialized;
    private long _currentVersion;

    public RemoteVehicleTypeStore(
        string serverBaseUrl,
        string clientId,
        string apiKey,
        ILogger<RemoteVehicleTypeStore> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient { BaseAddress = new Uri(serverBaseUrl) };

        // Configure SignalR connection
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{serverBaseUrl}/hubs/configuration", options =>
            {
                options.AccessTokenProvider = async () =>
                {
                    // Authenticate and get JWT token
                    var token = await AuthenticateAsync(clientId, apiKey);
                    return token;
                };
            })
            .WithAutomaticReconnect()
            .Build();

        // Subscribe to push notifications
        _hubConnection.On<VehicleTypeUpdateNotification>("VehicleTypeUpdated", HandleVehicleTypeUpdate);

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning("SignalR connection lost. Reconnecting... Error: {Error}", error?.Message);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR reconnected. Connection ID: {ConnectionId}", connectionId);
            return ReloadAllAsync(); // Reload all data after reconnection
        };

        _hubConnection.Closed += error =>
        {
            _logger.LogError("SignalR connection closed. Error: {Error}", error?.Message);
            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// Initialize the store by connecting to server and loading initial data.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        await _syncLock.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            _logger.LogInformation("Initializing RemoteVehicleTypeStore...");

            // Load initial data
            await ReloadAllAsync();

            // Connect to SignalR hub
            await _hubConnection.StartAsync();
            _logger.LogInformation("SignalR connection established");

            // Subscribe to updates
            await _hubConnection.InvokeAsync("SubscribeToUpdates");
            _logger.LogInformation("Subscribed to vehicle type updates");

            _isInitialized = true;
        }
        finally
        {
            _syncLock.Release();
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

        // Set authorization header for HTTP requests
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);

        return authResponse.AccessToken;
    }

    private async Task ReloadAllAsync()
    {
        _logger.LogInformation("Reloading all vehicle types from server...");

        var response = await _httpClient.GetAsync("/api/vehicle-types");
        response.EnsureSuccessStatusCode();

        var dtos = await response.Content.ReadFromJsonAsync<List<VehicleTypeDto>>();
        if (dtos == null)
        {
            _logger.LogWarning("Server returned null vehicle types list");
            return;
        }

        await _syncLock.WaitAsync();
        try
        {
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
            _syncLock.Release();
        }
    }

    private async void HandleVehicleTypeUpdate(VehicleTypeUpdateNotification notification)
    {
        _logger.LogInformation("Received update notification: {Type} - {Count} types affected (new version: {Version})",
            notification.UpdateType, notification.AffectedTypeIds.Count, notification.NewVersion);

        await _syncLock.WaitAsync();
        try
        {
            // For any change, reload the affected types
            // In a real implementation, we might optimize this by only fetching specific types
            // For simplicity, just reload all data
            _logger.LogInformation("Reloading data due to update notification");
            await ReloadAllAsync();

            _currentVersion = notification.NewVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling vehicle type update");
        }
        finally
        {
            _syncLock.Release();
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
