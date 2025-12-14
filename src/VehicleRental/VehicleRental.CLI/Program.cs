using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VehicleRental.Core.Application;
using VehicleRental.Core.Ports;
using VehicleRental.Core.Pricing;
using VehicleRental.CLI.Configuration;
using VehicleRental.CLI.Services;
using VehicleRental.Infrastructure.Repositories;
using VehicleRental.Infrastructure.Stores;

// =================================================================
// Application Host Setup
// =================================================================

var builder = Host.CreateApplicationBuilder(args);

// Add configuration from appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Load server configuration
var serverConfig = builder.Configuration.GetSection("Server").Get<ServerConfiguration>();
if (serverConfig == null)
{
    throw new InvalidOperationException("Server configuration is missing in appsettings.json");
}

// =================================================================
// Service Registration
// =================================================================

// Core services
builder.Services.AddSingleton<IPriceFormulaEvaluator, SafeFormulaEvaluator>();
builder.Services.AddSingleton<CheckoutService>();
builder.Services.AddSingleton<ReturnService>();

// Repositories
builder.Services.AddSingleton<IRentalRepository, InMemoryRentalRepository>();
builder.Services.AddSingleton<IVehicleCatalog, InMemoryVehicleCatalog>();

// Vehicle Type Store - Remote or Local
// Check if server is configured and available
if (!string.IsNullOrWhiteSpace(serverConfig.BaseUrl))
{
    // Use remote store that connects to server
    builder.Services.AddSingleton<IVehicleTypeStore>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<RemoteVehicleTypeStore>>();
        var store = new RemoteVehicleTypeStore(
            serverConfig.BaseUrl,
            serverConfig.ClientId,
            serverConfig.ApiKey,
            logger);
        
        // Initialize connection
        store.InitializeAsync().GetAwaiter().GetResult();
        
        return store;
    });
    
    Console.WriteLine($"✓ Connected to Vehicle Rental Server at {serverConfig.BaseUrl}");
    Console.WriteLine($"✓ Authenticated as: {serverConfig.ClientId}");
    Console.WriteLine($"✓ Real-time configuration updates enabled via SignalR");
}
else
{
    // Fallback to local in-memory store
    builder.Services.AddSingleton<IVehicleTypeStore, InMemoryVehicleTypeStore>();
    Console.WriteLine("⚠ Using local in-memory vehicle type store (no server connection)");
}

// Build the host
var host = builder.Build();

// =================================================================
// Application Entry Point
// =================================================================

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Vehicle Rental CLI starting...");

try
{
    // Display menu and process commands
    await RunInteractiveCliAsync(host.Services, logger);
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error in CLI");
    return 1;
}
finally
{
    // Cleanup - dispose RemoteVehicleTypeStore if used
    var vehicleTypeStore = host.Services.GetService<IVehicleTypeStore>();
    if (vehicleTypeStore is IAsyncDisposable asyncDisposable)
    {
        await asyncDisposable.DisposeAsync();
    }
}

return 0;

// =================================================================
// Interactive CLI Methods
// =================================================================

static async Task RunInteractiveCliAsync(IServiceProvider services, ILogger logger)
{
    var checkoutService = services.GetRequiredService<CheckoutService>();
    var returnService = services.GetRequiredService<ReturnService>();
    var vehicleTypeStore = services.GetRequiredService<IVehicleTypeStore>();
    var rentalRepository = services.GetRequiredService<IRentalRepository>();

    Console.WriteLine();
    Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║       Vehicle Rental Management System - CLI v2.0         ║");
    Console.WriteLine("║               Client-Server Architecture                   ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
    Console.WriteLine();

    while (true)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine(" Available Commands:");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  1. list-types     - Show all vehicle types");
        Console.WriteLine("  2. checkout       - Check out a vehicle");
        Console.WriteLine("  3. return         - Return a vehicle");
        Console.WriteLine("  4. list-rentals   - Show all active rentals");
        Console.WriteLine("  5. help           - Show this help");
        Console.WriteLine("  6. exit           - Exit application");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.Write("\nEnter command: ");

        var command = Console.ReadLine()?.Trim().ToLowerInvariant();

        try
        {
            switch (command)
            {
                case "1":
                case "list-types":
                    await ListVehicleTypesAsync(vehicleTypeStore, logger);
                    break;

                case "2":
                case "checkout":
                    await CheckoutVehicleAsync(checkoutService, vehicleTypeStore, logger);
                    break;

                case "3":
                case "return":
                    await ReturnVehicleAsync(returnService, rentalRepository, logger);
                    break;

                case "4":
                case "list-rentals":
                    await ListRentalsAsync(rentalRepository, logger);
                    break;

                case "5":
                case "help":
                    // Help is displayed by default
                    break;

                case "6":
                case "exit":
                    Console.WriteLine("\n👋 Thank you for using Vehicle Rental Management System!");
                    return;

                default:
                    Console.WriteLine("\n❌ Invalid command. Type 'help' to see available commands.");
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing command: {Command}", command);
            Console.WriteLine($"\n❌ Error: {ex.Message}");
        }
    }
}

static async Task ListVehicleTypesAsync(IVehicleTypeStore store, ILogger logger)
{
    Console.WriteLine("\n📋 Available Vehicle Types:");
    Console.WriteLine("─────────────────────────────────────────────────────────");

    var vehicleTypes = await store.GetAllAsync();

    if (!vehicleTypes.Any())
    {
        Console.WriteLine("  No vehicle types available.");
        return;
    }

    foreach (var vt in vehicleTypes)
    {
        Console.WriteLine($"\n  ID: {vt.VehicleTypeId}");
        Console.WriteLine($"  Name: {vt.DisplayName}");
        Console.WriteLine($"  Formula: {vt.PricingFormula}");
        if (!string.IsNullOrEmpty(vt.Description))
        {
            Console.WriteLine($"  Description: {vt.Description}");
        }
    }
}

static async Task CheckoutVehicleAsync(CheckoutService service, IVehicleTypeStore store, ILogger logger)
{
    Console.WriteLine("\n🚗 Vehicle Checkout");
    Console.WriteLine("─────────────────────────────────────────────────────────");

    // Show available types
    var vehicleTypes = await store.GetAllAsync();
    Console.WriteLine("\nAvailable vehicle types:");
    foreach (var vt in vehicleTypes)
    {
        Console.WriteLine($"  - {vt.VehicleTypeId} ({vt.DisplayName})");
    }

    Console.Write("\nEnter Booking Number: ");
    var bookingNumber = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(bookingNumber))
    {
        Console.WriteLine("❌ Booking number is required.");
        return;
    }

    Console.Write("Enter Vehicle Registration Number: ");
    var registrationNumber = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(registrationNumber))
    {
        Console.WriteLine("❌ Registration number is required.");
        return;
    }

    Console.Write("Enter Vehicle Type ID (optional, press Enter to skip): ");
    var vehicleTypeId = Console.ReadLine()?.Trim();

    Console.Write("Enter Checkout Timestamp (ISO 8601, e.g., 2024-03-20T10:00:00+01:00): ");
    var checkoutTimestampStr = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(checkoutTimestampStr) || !DateTimeOffset.TryParse(checkoutTimestampStr, out var checkoutTimestamp))
    {
        Console.WriteLine("❌ Invalid checkout timestamp.");
        return;
    }

    Console.Write("Enter Odometer Reading (km): ");
    var odometerStr = Console.ReadLine()?.Trim();
    if (!decimal.TryParse(odometerStr, out var odometer))
    {
        Console.WriteLine("❌ Invalid odometer reading.");
        return;
    }

    var request = new RegisterCheckoutRequest
    {
        BookingNumber = bookingNumber,
        RegistrationNumber = registrationNumber,
        VehicleTypeId = string.IsNullOrWhiteSpace(vehicleTypeId) ? null : vehicleTypeId,
        CheckoutTimestamp = checkoutTimestamp,
        CheckoutOdometer = odometer
    };

    var result = await service.RegisterCheckoutAsync(request);

    if (result.IsSuccess)
    {
        Console.WriteLine($"\n✅ Vehicle checked out successfully!");
        Console.WriteLine($"   Booking Number: {result.Value!.BookingNumber}");
        Console.WriteLine($"   Registration: {result.Value.RegistrationNumber}");
        Console.WriteLine($"   Vehicle Type: {result.Value.VehicleTypeId}");
    }
    else
    {
        Console.WriteLine($"\n❌ Checkout failed: {result.Error}");
    }
}

static async Task ReturnVehicleAsync(ReturnService service, IRentalRepository repository, ILogger logger)
{
    Console.WriteLine("\n🏁 Vehicle Return");
    Console.WriteLine("─────────────────────────────────────────────────────────");

    Console.Write("\nEnter Booking Number: ");
    var bookingNumber = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(bookingNumber))
    {
        Console.WriteLine("❌ Booking number is required.");
        return;
    }

    Console.Write("Enter Return Timestamp (ISO 8601, e.g., 2024-03-22T14:30:00+01:00): ");
    var returnTimestampStr = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(returnTimestampStr) || !DateTimeOffset.TryParse(returnTimestampStr, out var returnTimestamp))
    {
        Console.WriteLine("❌ Invalid return timestamp.");
        return;
    }

    Console.Write("Enter Odometer Reading (km): ");
    var odometerStr = Console.ReadLine()?.Trim();
    if (!decimal.TryParse(odometerStr, out var odometer))
    {
        Console.WriteLine("❌ Invalid odometer reading.");
        return;
    }

    Console.Write("Enter Base Day Rate (SEK): ");
    var baseDayRateStr = Console.ReadLine()?.Trim();
    if (!decimal.TryParse(baseDayRateStr, out var baseDayRate))
    {
        Console.WriteLine("❌ Invalid base day rate.");
        return;
    }

    Console.Write("Enter Base Kilometer Price (SEK): ");
    var baseKmPriceStr = Console.ReadLine()?.Trim();
    if (!decimal.TryParse(baseKmPriceStr, out var baseKmPrice))
    {
        Console.WriteLine("❌ Invalid base kilometer price.");
        return;
    }

    var request = new RegisterReturnRequest
    {
        BookingNumber = bookingNumber,
        ReturnTimestamp = returnTimestamp,
        ReturnOdometer = odometer,
        PricingParameters = new VehicleRental.Core.Domain.PricingParameters
        {
            BaseDayRate = baseDayRate,
            BaseKmPrice = baseKmPrice
        }
    };

    var result = await service.RegisterReturnAsync(request);

    if (result.IsSuccess)
    {
        var response = result.Value!;
        Console.WriteLine($"\n✅ Vehicle returned successfully!");
        Console.WriteLine($"\n╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║                        INVOICE                             ║");
        Console.WriteLine($"╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine($"  Booking Number: {response.BookingNumber}");
        Console.WriteLine($"  Days: {response.Days}");
        Console.WriteLine($"  Kilometers: {response.KilometersDriven:F2} km");
        Console.WriteLine($"  ──────────────────────────────────────────────────────────");
        Console.WriteLine($"  TOTAL: {response.RentalPrice:F2} SEK");
        Console.WriteLine($"  ══════════════════════════════════════════════════════════");
    }
    else
    {
        Console.WriteLine($"\n❌ Return failed: {result.Error}");
    }
}

static async Task ListRentalsAsync(IRentalRepository repository, ILogger logger)
{
    Console.WriteLine("\n📊 Rentals:");
    Console.WriteLine("─────────────────────────────────────────────────────────");
    Console.WriteLine("  Note: This feature requires additional repository methods.");
    Console.WriteLine("  For now, rentals are tracked internally in the repository.");
}

