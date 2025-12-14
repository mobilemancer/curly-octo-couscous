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
Console.WriteLine($"\nServer: {serverConfig.BaseUrl}");
Console.WriteLine($"Admin Client: {serverConfig.ClientId}");
Console.WriteLine();

using var httpClient = new HttpClient { BaseAddress = new Uri(serverConfig.BaseUrl) };

// Authenticate
try
{
    var authResponse = await AuthenticateAsync(httpClient, serverConfig.ClientId, serverConfig.ApiKey, logger);
    if (authResponse == null)
    {
        Console.WriteLine("❌ Authentication failed. Exiting.");
        return 1;
    }
    
    httpClient.DefaultRequestHeaders.Authorization = 
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.AccessToken);
    
    Console.WriteLine($"✓ Authenticated as: {authResponse.ClientName}");
    Console.WriteLine();
}
catch (Exception ex)
{
    logger.LogError(ex, "Authentication error");
    Console.WriteLine($"❌ Failed to connect to server: {ex.Message}");
    return 1;
}

// Main menu loop
while (true)
{
    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine(" Admin Commands:");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  1. list       - List all vehicle types");
    Console.WriteLine("  2. add        - Add new vehicle type");
    Console.WriteLine("  3. update     - Update existing vehicle type");
    Console.WriteLine("  4. delete     - Delete vehicle type");
    Console.WriteLine("  5. exit       - Exit tool");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.Write("\nEnter command: ");
    
    var command = Console.ReadLine()?.Trim().ToLowerInvariant();
    
    try
    {
        switch (command)
        {
            case "1":
            case "list":
                await ListVehicleTypesAsync(httpClient, logger);
                break;
                
            case "2":
            case "add":
                await AddVehicleTypeAsync(httpClient, logger);
                break;
                
            case "3":
            case "update":
                await UpdateVehicleTypeAsync(httpClient, logger);
                break;
                
            case "4":
            case "delete":
                await DeleteVehicleTypeAsync(httpClient, logger);
                break;
                
            case "5":
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
        logger.LogError(ex, "HTTP request failed");
        Console.WriteLine($"\n❌ Request failed: {ex.Message}");
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

