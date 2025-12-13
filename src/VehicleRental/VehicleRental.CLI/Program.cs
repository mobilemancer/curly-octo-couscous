using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VehicleRental.Core.Application;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Ports;
using VehicleRental.Core.Pricing;
using VehicleRental.Infrastructure.Repositories;
using VehicleRental.Infrastructure.Stores;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

// Setup DI container
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddConfiguration(configuration.GetSection("Logging"));
});

// Register Infrastructure
var vehiclesJsonPath = configuration["FilePaths:VehiclesJson"] ?? "Data/vehicles.json";
var vehicleTypesJsonPath = configuration["FilePaths:VehicleTypesJson"] ?? "Data/vehicle-types.json";

services.AddSingleton<IRentalRepository, InMemoryRentalRepository>();
services.AddSingleton<IVehicleCatalog>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<InMemoryVehicleCatalog>>();
    return new InMemoryVehicleCatalog(vehiclesJsonPath, logger);
});
services.AddSingleton<IPriceFormulaEvaluator, SafeFormulaEvaluator>();
services.AddSingleton<IVehicleTypeStore>(sp =>
{
    var evaluator = sp.GetRequiredService<IPriceFormulaEvaluator>();
    var logger = sp.GetRequiredService<ILogger<InMemoryVehicleTypeStore>>();
    return new InMemoryVehicleTypeStore(vehicleTypesJsonPath, evaluator, logger);
});

// Register Core services
services.AddSingleton<PricingCalculator>();
services.AddSingleton<CheckoutService>();
services.AddSingleton<ReturnService>();

var serviceProvider = services.BuildServiceProvider();

// Get pricing parameters from config
var baseDayRate = configuration.GetValue<decimal>("PricingParameters:BaseDayRate");
var baseKmPrice = configuration.GetValue<decimal>("PricingParameters:BaseKmPrice");
var pricingParameters = new PricingParameters
{
    BaseDayRate = baseDayRate,
    BaseKmPrice = baseKmPrice
};

Console.WriteLine("=== Vehicle Rental Management System ===");
Console.WriteLine($"Base Day Rate: {baseDayRate:C}, Base Km Price: {baseKmPrice:C}");
Console.WriteLine();

if (args.Length == 0)
{
    ShowUsage();
    return;
}

var command = args[0].ToLowerInvariant();

try
{
    switch (command)
    {
        case "checkout":
            await HandleCheckout(args, serviceProvider);
            break;
        case "return":
            await HandleReturn(args, serviceProvider, pricingParameters);
            break;
        default:
            Console.WriteLine($"Unknown command: {command}");
            ShowUsage();
            break;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}

static void ShowUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  checkout <booking-number> <registration-number> <checkout-timestamp> <odometer>");
    Console.WriteLine("  return <booking-number> <return-timestamp> <odometer>");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  checkout BK001 ABC123 2025-12-13T10:00:00Z 10000");
    Console.WriteLine("  return BK001 2025-12-16T10:00:00Z 10500");
}

static async Task HandleCheckout(string[] args, ServiceProvider sp)
{
    if (args.Length < 5)
    {
        Console.WriteLine("Error: checkout requires <booking-number> <registration-number> <timestamp> <odometer>");
        return;
    }

    var checkoutService = sp.GetRequiredService<CheckoutService>();

    var request = new RegisterCheckoutRequest
    {
        BookingNumber = args[1],
        RegistrationNumber = args[2],
        CheckoutTimestamp = ParseTimestamp(args[3]),
        CheckoutOdometer = decimal.Parse(args[4])
    };

    var result = await checkoutService.RegisterCheckoutAsync(request);

    if (result.IsSuccess)
    {
        Console.WriteLine($"✓ Checkout registered successfully");
        Console.WriteLine($"  Booking Number: {result.Value!.BookingNumber}");
        Console.WriteLine($"  Vehicle: {result.Value.RegistrationNumber}");
        Console.WriteLine($"  Type: {result.Value.VehicleTypeId}");
        Console.WriteLine($"  Timestamp: {result.Value.CheckoutTimestamp:yyyy-MM-dd HH:mm:ss zzz}");
    }
    else
    {
        Console.WriteLine($"✗ Checkout failed: {result.Error}");
        Environment.Exit(1);
    }
}

static async Task HandleReturn(string[] args, ServiceProvider sp, PricingParameters pricingParams)
{
    if (args.Length < 4)
    {
        Console.WriteLine("Error: return requires <booking-number> <timestamp> <odometer>");
        return;
    }

    var returnService = sp.GetRequiredService<ReturnService>();

    var request = new RegisterReturnRequest
    {
        BookingNumber = args[1],
        ReturnTimestamp = ParseTimestamp(args[2]),
        ReturnOdometer = decimal.Parse(args[3]),
        PricingParameters = pricingParams
    };

    var result = await returnService.RegisterReturnAsync(request);

    if (result.IsSuccess)
    {
        Console.WriteLine($"✓ Return registered successfully");
        Console.WriteLine($"  Booking Number: {result.Value!.BookingNumber}");
        Console.WriteLine($"  Days: {result.Value.Days}");
        Console.WriteLine($"  Kilometers: {result.Value.KilometersDriven:N0} km");
        Console.WriteLine($"  Rental Price: {result.Value.RentalPrice:C}");
    }
    else
    {
        Console.WriteLine($"✗ Return failed: {result.Error}");
        Environment.Exit(1);
    }
}

static DateTimeOffset ParseTimestamp(string timestamp)
{
    // Try parsing with explicit offset first (ISO 8601)
    if (DateTimeOffset.TryParse(timestamp, out var result))
    {
        return result;
    }

    // If no offset, try parsing as DateTime and apply local timezone
    if (DateTime.TryParse(timestamp, out var dt))
    {
        return new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt));
    }

    throw new FormatException($"Invalid timestamp format: {timestamp}. Use ISO 8601 format (e.g., 2025-12-13T10:00:00Z or 2025-12-13T10:00:00+01:00)");
}

