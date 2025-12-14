using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using VehicleRental.Core.Application;
using VehicleRental.Core.Ports;
using VehicleRental.Core.Pricing;
using VehicleRental.CLI.Commands;
using VehicleRental.CLI.Configuration;
using VehicleRental.CLI.Services;
using VehicleRental.Infrastructure.Repositories;
using VehicleRental.Infrastructure.Stores;

// =================================================================
// Application Host Setup
// =================================================================

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Configure logging - suppress console output for clean UI
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Load server configuration
var serverConfig = builder.Configuration.GetSection("Server").Get<ServerConfiguration>()
    ?? throw new InvalidOperationException("Server configuration is missing in appsettings.json");

// =================================================================
// Service Registration
// =================================================================

// Configuration
builder.Services.AddSingleton(serverConfig);

// Core services
builder.Services.AddSingleton<IPriceFormulaEvaluator, SafeFormulaEvaluator>();
builder.Services.AddSingleton<PricingCalculator>();
builder.Services.AddSingleton<CheckoutService>();
builder.Services.AddSingleton<ReturnService>();

// Repositories
builder.Services.AddSingleton<IRentalRepository, InMemoryRentalRepository>();

// Vehicle Catalog - local storage (seeded from vehicles.json)
builder.Services.AddSingleton<IVehicleCatalog>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<InMemoryVehicleCatalog>>();
    var vehiclesFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "vehicles.json");
    return new InMemoryVehicleCatalog(vehiclesFilePath, logger);
});

// Vehicle Type Store - Remote or Local
if (!string.IsNullOrWhiteSpace(serverConfig.BaseUrl))
{
    builder.Services.AddSingleton<IVehicleTypeStore>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<RemoteVehicleTypeStore>>();
        var store = new RemoteVehicleTypeStore(
            serverConfig.BaseUrl,
            serverConfig.ClientId,
            serverConfig.ApiKey,
            logger);

        _ = store.InitializeAsync();
        return store;
    });
}
else
{
    builder.Services.AddSingleton<IVehicleTypeStore, InMemoryVehicleTypeStore>();
}

// Commands
builder.Services.AddTransient<ListVehicleTypesCommand>();
builder.Services.AddTransient<CheckoutCommand>();
builder.Services.AddTransient<ReturnCommand>();
builder.Services.AddTransient<ListRentalsCommand>();
builder.Services.AddTransient<InteractiveMenuCommand>();

// Build the host
var host = builder.Build();

// =================================================================
// Wire up vehicle update notifications from server to local catalog
// =================================================================

var vehicleTypeStore = host.Services.GetService<IVehicleTypeStore>();
var vehicleCatalog = host.Services.GetRequiredService<IVehicleCatalog>();

if (vehicleTypeStore is RemoteVehicleTypeStore remoteStore)
{
    remoteStore.OnVehicleUpdated += notification =>
    {
        switch (notification.UpdateType)
        {
            case VehicleRental.Shared.Contracts.VehicleUpdateType.Added:
                if (notification.Vehicle != null)
                {
                    var vehicle = new VehicleRental.Core.Domain.Vehicle
                    {
                        RegistrationNumber = notification.Vehicle.RegistrationNumber,
                        VehicleTypeId = VehicleRental.Core.Helpers.VehicleTypeIdNormalizer.Normalize(notification.Vehicle.VehicleTypeId),
                        CurrentOdometer = notification.Vehicle.CurrentOdometer,
                        Location = notification.Vehicle.Location
                    };
                    _ = vehicleCatalog.AddVehicleAsync(vehicle);
                }
                break;

            case VehicleRental.Shared.Contracts.VehicleUpdateType.Removed:
                if (notification.RegistrationNumber != null)
                {
                    _ = vehicleCatalog.RemoveVehicleAsync(notification.RegistrationNumber);
                }
                break;
        }
    };
}

// =================================================================
// Application Entry Point
// =================================================================

try
{
    var menuCommand = host.Services.GetRequiredService<InteractiveMenuCommand>();
    await menuCommand.RunAsync();
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex);
    return 1;
}
finally
{
    if (vehicleTypeStore is IAsyncDisposable asyncDisposableStore)
    {
        await asyncDisposableStore.DisposeAsync();
    }
}

return 0;

