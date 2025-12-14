using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VehicleRental.DevTools.CLI.Configuration;
using VehicleRental.Shared.Contracts;

// =================================================================
// Dev Tools CLI - For Administering Vehicle Rental Server
// =================================================================

var builder = Host.CreateApplicationBuilder(args);

// Add configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning); // Less verbose for admin tool

// Load server configuration
var serverConfig = builder.Configuration.GetSection("Server").Get<ServerConfiguration>();
if (serverConfig == null)
{
    throw new InvalidOperationException("Server configuration missing in appsettings.json");
}

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║     Vehicle Rental Server - Administration Tool              ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine($"\n🌐 Server: {serverConfig.BaseUrl}");
Console.WriteLine($"👤 Admin: {serverConfig.ClientId}");
Console.WriteLine();

// Create HttpClient with SSL certificate validation bypass for development
var handler = new HttpClientHandler();
handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;
using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(serverConfig.BaseUrl) };

// Authenticate with retry logic
ClientAuthResponse? authResponse = null;
while (authResponse == null)
{
    try
    {
        Console.WriteLine("🔄 Connecting to server...");
        authResponse = await AuthenticateAsync(httpClient, serverConfig.ClientId, serverConfig.ApiKey, logger);
        if (authResponse == null)
        {
            Console.WriteLine("⚠️  Authentication failed. Retrying in 10 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(10));
            continue;
        }

        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.AccessToken);

        Console.WriteLine($"✅ Authenticated as: {authResponse.ClientName}");
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Connection attempt failed");
        Console.WriteLine($"⚠️  Server unavailable. Retrying in 10 seconds...");
        await Task.Delay(TimeSpan.FromSeconds(10));
    }
}

// Main menu loop
while (true)
{
    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine(" Admin Commands:");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  Vehicle Types:");
    Console.WriteLine("  1. list-types    - List all vehicle types");
    Console.WriteLine("  2. add-type      - Add new vehicle type");
    Console.WriteLine("  3. update-type   - Update existing vehicle type");
    Console.WriteLine("  4. delete-type   - Delete vehicle type");
    Console.WriteLine();
    Console.WriteLine("  Vehicles:");
    Console.WriteLine("  5. list-vehicles - List all vehicles");
    Console.WriteLine("  6. add-vehicle   - Add new vehicle to fleet");
    Console.WriteLine("  7. delete-vehicle - Remove vehicle from fleet");
    Console.WriteLine();
    Console.WriteLine("  8. exit          - Exit tool");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.Write("\nEnter command: ");

    var command = Console.ReadLine()?.Trim().ToLowerInvariant();

    try
    {
        switch (command)
        {
            case "1":
            case "list":
            case "list-types":
                await ListVehicleTypesAsync(httpClient, logger);
                break;

            case "2":
            case "add":
            case "add-type":
                await AddVehicleTypeAsync(httpClient, logger);
                break;

            case "3":
            case "update":
            case "update-type":
                await UpdateVehicleTypeAsync(httpClient, logger);
                break;

            case "4":
            case "delete":
            case "delete-type":
                await DeleteVehicleTypeAsync(httpClient, logger);
                break;

            case "5":
            case "list-vehicles":
                await ListVehiclesAsync(httpClient, logger);
                break;

            case "6":
            case "add-vehicle":
                await AddVehicleAsync(httpClient, logger);
                break;

            case "7":
            case "delete-vehicle":
                await DeleteVehicleAsync(httpClient, logger);
                break;

            case "8":
            case "exit":
                Console.WriteLine("\n👋 Goodbye!");
                return 0;

            default:
                Console.WriteLine("\n❌ Invalid command.");
                break;
        }
    }
    catch (HttpRequestException ex)
    {
        logger.LogWarning(ex, "HTTP request failed");
        Console.WriteLine($"\n⚠️  Connection issue: {ex.Message}");
        Console.WriteLine("The server may be temporarily unavailable. Command will retry on next attempt.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Command failed");
        Console.WriteLine($"\n❌ Error: {ex.Message}");
    }
}

// =================================================================
// Helper Methods
// =================================================================

static async Task<ClientAuthResponse?> AuthenticateAsync(
    HttpClient client,
    string clientId,
    string apiKey,
    ILogger logger)
{
    var request = new ClientAuthRequest
    {
        ClientId = clientId,
        ApiKey = apiKey
    };

    var response = await client.PostAsJsonAsync("/api/clients/authenticate", request);

    if (!response.IsSuccessStatusCode)
    {
        logger.LogError("Authentication failed: {StatusCode}", response.StatusCode);
        return null;
    }

    return await response.Content.ReadFromJsonAsync<ClientAuthResponse>();
}

static async Task ListVehicleTypesAsync(HttpClient client, ILogger logger)
{
    Console.WriteLine("\n📋 Vehicle Types:");
    Console.WriteLine("─────────────────────────────────────────────────────────────");

    var response = await client.GetAsync("/api/vehicle-types");
    response.EnsureSuccessStatusCode();

    var vehicleTypes = await response.Content.ReadFromJsonAsync<List<VehicleTypeDto>>();

    if (vehicleTypes == null || vehicleTypes.Count == 0)
    {
        Console.WriteLine("  No vehicle types found.");
        return;
    }

    foreach (var vt in vehicleTypes)
    {
        Console.WriteLine($"\n  ID: {vt.VehicleTypeId}");
        Console.WriteLine($"  Name: {vt.DisplayName}");
        Console.WriteLine($"  Formula: {vt.PricingFormula}");
        Console.WriteLine($"  Version: {vt.Version} (Updated: {vt.LastUpdated:yyyy-MM-dd HH:mm:ss})");
        if (!string.IsNullOrEmpty(vt.Description))
        {
            Console.WriteLine($"  Description: {vt.Description}");
        }
    }
}

static async Task AddVehicleTypeAsync(HttpClient client, ILogger logger)
{
    Console.WriteLine("\n➕ Add New Vehicle Type");
    Console.WriteLine("─────────────────────────────────────────────────────────────");

    Console.Write("\nVehicle Type ID (e.g., 'luxury-car'): ");
    var id = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(id))
    {
        Console.WriteLine("❌ ID is required.");
        return;
    }

    Console.Write("Display Name (e.g., 'Luxury Car'): ");
    var displayName = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(displayName))
    {
        Console.WriteLine("❌ Display name is required.");
        return;
    }

    Console.Write("Pricing Formula (e.g., '(baseDayRate * days * 2) + (baseKmPrice * km * 1.5)'): ");
    var formula = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(formula))
    {
        Console.WriteLine("❌ Formula is required.");
        return;
    }

    Console.Write("Description (optional): ");
    var description = Console.ReadLine()?.Trim();

    var newVehicleType = new VehicleTypeDto
    {
        VehicleTypeId = id,
        DisplayName = displayName,
        PricingFormula = formula,
        Description = string.IsNullOrWhiteSpace(description) ? null : description,
        Version = 0,
        LastUpdated = DateTimeOffset.UtcNow
    };

    var response = await client.PostAsJsonAsync("/api/vehicle-types", newVehicleType);

    if (response.IsSuccessStatusCode)
    {
        Console.WriteLine($"\n✓ Vehicle type '{displayName}' added successfully!");
    }
    else
    {
        var error = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"\n❌ Failed to add vehicle type: {response.StatusCode}");
        Console.WriteLine($"   {error}");
    }
}

static async Task UpdateVehicleTypeAsync(HttpClient client, ILogger logger)
{
    Console.WriteLine("\n✏️  Update Vehicle Type");
    Console.WriteLine("─────────────────────────────────────────────────────────────");

    Console.Write("\nVehicle Type ID to update: ");
    var id = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(id))
    {
        Console.WriteLine("❌ ID is required.");
        return;
    }

    // Fetch existing
    var getResponse = await client.GetAsync($"/api/vehicle-types/{id}");
    if (!getResponse.IsSuccessStatusCode)
    {
        Console.WriteLine($"❌ Vehicle type '{id}' not found.");
        return;
    }

    var existing = await getResponse.Content.ReadFromJsonAsync<VehicleTypeDto>();
    if (existing == null)
    {
        Console.WriteLine($"❌ Failed to load vehicle type.");
        return;
    }

    Console.WriteLine($"\nCurrent values:");
    Console.WriteLine($"  Display Name: {existing.DisplayName}");
    Console.WriteLine($"  Formula: {existing.PricingFormula}");
    Console.WriteLine($"  Description: {existing.Description ?? "(none)"}");

    Console.Write($"\nNew Display Name (or Enter to keep '{existing.DisplayName}'): ");
    var displayName = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(displayName)) displayName = existing.DisplayName;

    Console.Write($"\nNew Pricing Formula (or Enter to keep current): ");
    var formula = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(formula)) formula = existing.PricingFormula;

    Console.Write($"\nNew Description (or Enter to keep current): ");
    var description = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(description)) description = existing.Description;

    var updated = new VehicleTypeDto
    {
        VehicleTypeId = id,
        DisplayName = displayName,
        PricingFormula = formula,
        Description = description,
        Version = existing.Version + 1,
        LastUpdated = DateTimeOffset.UtcNow
    };

    var response = await client.PutAsJsonAsync($"/api/vehicle-types/{id}", updated);

    if (response.IsSuccessStatusCode)
    {
        Console.WriteLine($"\n✓ Vehicle type '{displayName}' updated successfully!");
    }
    else
    {
        var error = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"\n❌ Failed to update: {response.StatusCode}");
        Console.WriteLine($"   {error}");
    }
}

static async Task DeleteVehicleTypeAsync(HttpClient client, ILogger logger)
{
    Console.WriteLine("\n🗑️  Delete Vehicle Type");
    Console.WriteLine("─────────────────────────────────────────────────────────────");

    Console.Write("\nVehicle Type ID to delete: ");
    var id = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(id))
    {
        Console.WriteLine("❌ ID is required.");
        return;
    }

    Console.Write($"⚠️  Are you sure you want to delete '{id}'? (yes/no): ");
    var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (confirm != "yes")
    {
        Console.WriteLine("Cancelled.");
        return;
    }

    var response = await client.DeleteAsync($"/api/vehicle-types/{id}");

    if (response.IsSuccessStatusCode)
    {
        Console.WriteLine($"\n✓ Vehicle type '{id}' deleted successfully!");
    }
    else
    {
        var error = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"\n❌ Failed to delete: {response.StatusCode}");
        Console.WriteLine($"   {error}");
    }
}

static async Task ListVehiclesAsync(HttpClient client, ILogger logger)
{
    Console.WriteLine("\n🚗 All Vehicles in Fleet");
    Console.WriteLine("─────────────────────────────────────────────────────────────");

    var response = await client.GetAsync("/api/vehicles");

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"❌ Failed to fetch vehicles: {response.StatusCode}");
        return;
    }

    var vehicles = await response.Content.ReadFromJsonAsync<List<VehicleDto>>();

    if (vehicles == null || vehicles.Count == 0)
    {
        Console.WriteLine("\nNo vehicles found in fleet.");
        return;
    }

    // Group by location
    var groupedByLocation = vehicles.GroupBy(v => v.Location).OrderBy(g => g.Key);

    foreach (var locationGroup in groupedByLocation)
    {
        Console.WriteLine($"\n📍 Location: {locationGroup.Key}");
        Console.WriteLine("   ─────────────────────────────────────────────────────");

        foreach (var vehicle in locationGroup.OrderBy(v => v.VehicleTypeId).ThenBy(v => v.RegistrationNumber))
        {
            Console.WriteLine($"   • {vehicle.RegistrationNumber,-10} | Type: {vehicle.VehicleTypeId,-15} | Odometer: {vehicle.CurrentOdometer,8:N0} km");
        }
    }

    Console.WriteLine($"\n   Total vehicles: {vehicles.Count}");
}

static async Task AddVehicleAsync(HttpClient client, ILogger logger)
{
    Console.WriteLine("\n➕ Add New Vehicle to Fleet");
    Console.WriteLine("─────────────────────────────────────────────────────────────");

    // Fetch available vehicle types first
    var typesResponse = await client.GetAsync("/api/vehicle-types");
    if (!typesResponse.IsSuccessStatusCode)
    {
        Console.WriteLine("❌ Failed to fetch vehicle types. Cannot proceed.");
        return;
    }

    var vehicleTypes = await typesResponse.Content.ReadFromJsonAsync<List<VehicleTypeDto>>();
    if (vehicleTypes == null || vehicleTypes.Count == 0)
    {
        Console.WriteLine("❌ No vehicle types available. Please add vehicle types first.");
        return;
    }

    Console.WriteLine("\nAvailable vehicle types:");
    foreach (var type in vehicleTypes.OrderBy(t => t.VehicleTypeId))
    {
        Console.WriteLine($"  • {type.VehicleTypeId} - {type.DisplayName}");
    }

    // Get vehicle details
    Console.Write("\nRegistration Number (e.g., 'ABC123'): ");
    var regNumber = Console.ReadLine()?.Trim().ToUpperInvariant();
    if (string.IsNullOrWhiteSpace(regNumber))
    {
        Console.WriteLine("❌ Registration number is required.");
        return;
    }

    Console.Write("Vehicle Type ID: ");
    var typeId = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(typeId))
    {
        Console.WriteLine("❌ Vehicle type ID is required.");
        return;
    }

    // Validate vehicle type exists
    if (!vehicleTypes.Any(t => string.Equals(t.VehicleTypeId, typeId, StringComparison.OrdinalIgnoreCase)))
    {
        Console.WriteLine($"❌ Vehicle type '{typeId}' does not exist.");
        return;
    }

    Console.Write("Current Odometer (km): ");
    if (!decimal.TryParse(Console.ReadLine()?.Trim(), out var odometer) || odometer < 0)
    {
        Console.WriteLine("❌ Invalid odometer reading. Must be a non-negative number.");
        return;
    }

    Console.Write("Location (client ID, e.g., 'location-stockholm-001'): ");
    var location = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(location))
    {
        Console.WriteLine("❌ Location is required.");
        return;
    }

    var newVehicle = new VehicleDto
    {
        RegistrationNumber = regNumber,
        VehicleTypeId = typeId,
        CurrentOdometer = odometer,
        Location = location
    };

    var response = await client.PostAsJsonAsync("/api/vehicles", newVehicle);

    if (response.IsSuccessStatusCode)
    {
        Console.WriteLine($"\n✓ Vehicle '{regNumber}' added successfully to {location}!");
    }
    else
    {
        var error = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"\n❌ Failed to add vehicle: {response.StatusCode}");
        Console.WriteLine($"   {error}");
    }
}

static async Task DeleteVehicleAsync(HttpClient client, ILogger logger)
{
    Console.WriteLine("\n🗑️  Delete Vehicle from Fleet");
    Console.WriteLine("─────────────────────────────────────────────────────────────");

    Console.Write("\nRegistration Number to delete: ");
    var regNumber = Console.ReadLine()?.Trim().ToUpperInvariant();
    if (string.IsNullOrWhiteSpace(regNumber))
    {
        Console.WriteLine("❌ Registration number is required.");
        return;
    }

    Console.Write($"⚠️  Are you sure you want to delete vehicle '{regNumber}'? (yes/no): ");
    var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (confirm != "yes")
    {
        Console.WriteLine("Cancelled.");
        return;
    }

    var response = await client.DeleteAsync($"/api/vehicles/{regNumber}");

    if (response.IsSuccessStatusCode)
    {
        Console.WriteLine($"\n✓ Vehicle '{regNumber}' deleted successfully!");
    }
    else
    {
        var error = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"\n❌ Failed to delete: {response.StatusCode}");
        Console.WriteLine($"   {error}");
    }
}
