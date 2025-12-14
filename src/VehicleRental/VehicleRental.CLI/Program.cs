using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using VehicleRental.Core.Application;
using VehicleRental.Core.Domain;
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

// Configure logging - suppress console output for clean UI
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Warning); // Only show warnings and errors

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
builder.Services.AddSingleton<PricingCalculator>();
builder.Services.AddSingleton<CheckoutService>();
builder.Services.AddSingleton<ReturnService>();

// Repositories
builder.Services.AddSingleton<IRentalRepository, InMemoryRentalRepository>();

// Vehicle Catalog - local storage (seeded from vehicles.json)
// Each client/location stores their own vehicle data locally
builder.Services.AddSingleton<IVehicleCatalog>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<InMemoryVehicleCatalog>>();
    var vehiclesFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "vehicles.json");
    return new InMemoryVehicleCatalog(vehiclesFilePath, logger);
});

// Vehicle Type Store - Remote or Local
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

        // Initialize connection (non-blocking, continues in background if server unavailable)
        _ = store.InitializeAsync();

        return store;
    });
}
else
{
    // Fallback to local in-memory store
    builder.Services.AddSingleton<IVehicleTypeStore, InMemoryVehicleTypeStore>();
}

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
        // Handle vehicle updates from server and add to local catalog
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
    // Display menu and process commands
    await RunInteractiveCliAsync(host.Services, serverConfig);
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex);
    return 1;
}
finally
{
    // Cleanup - dispose remote services if used
    if (vehicleTypeStore is IAsyncDisposable asyncDisposableStore)
    {
        await asyncDisposableStore.DisposeAsync();
    }
}

return 0;

// =================================================================
// Interactive CLI Methods
// =================================================================

static void DisplayHeader(ServerConfiguration serverConfig)
{
    // Display banner
    AnsiConsole.Write(
        new FigletText("Vehicle Rental")
            .Centered()
            .Color(Color.Cyan1));

    var panel = new Panel("[cyan1]Client-Server Architecture[/]\n[grey]Version 2.0[/]")
    {
        Border = BoxBorder.Rounded,
        BorderStyle = new Style(Color.Cyan1),
        Padding = new Padding(1, 0)
    };
    AnsiConsole.Write(Align.Center(panel));

    // Display connection status
    var statusTable = new Table()
        .Border(TableBorder.None)
        .HideHeaders()
        .AddColumn(new TableColumn("").Centered());

    if (!string.IsNullOrWhiteSpace(serverConfig.BaseUrl))
    {
        statusTable.AddRow($"[green]●[/] Server: [cyan]{serverConfig.BaseUrl}[/]");
        statusTable.AddRow($"[green]●[/] Client: [cyan]{serverConfig.ClientId}[/]");
        statusTable.AddRow($"[green]●[/] Real-time updates: [green]Enabled[/]");
    }
    else
    {
        statusTable.AddRow($"[yellow]●[/] Mode: [yellow]Local (No server connection)[/]");
    }

    AnsiConsole.Write(statusTable.Centered());
    AnsiConsole.WriteLine();
}

static async Task RunInteractiveCliAsync(IServiceProvider services, ServerConfiguration serverConfig)
{
    var checkoutService = services.GetRequiredService<CheckoutService>();
    var returnService = services.GetRequiredService<ReturnService>();
    var vehicleTypeStore = services.GetRequiredService<IVehicleTypeStore>();
    var vehicleCatalog = services.GetRequiredService<IVehicleCatalog>();
    var rentalRepository = services.GetRequiredService<IRentalRepository>();

    AnsiConsole.Clear();
    DisplayHeader(serverConfig);

    while (true)
    {
        AnsiConsole.MarkupLine("[cyan1]What would you like to do?[/]");
        var command = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .PageSize(10)
                .AddChoices(new[] {
                    "📋 List Vehicle Types",
                    "🚗 Check Out Vehicle",
                    "🏁 Return Vehicle",
                    "📊 List All Rentals",
                    "🚪 Exit"
                }));

        try
        {
            switch (command)
            {
                case "📋 List Vehicle Types":
                    await ListVehicleTypesAsync(vehicleTypeStore);
                    break;

                case "🚗 Check Out Vehicle":
                    try
                    {
                        await CheckoutVehicleAsync(checkoutService, vehicleTypeStore, vehicleCatalog, rentalRepository);
                    }
                    catch (OperationCanceledException)
                    {
                        AnsiConsole.MarkupLine("\n[yellow]Checkout cancelled.[/]");
                    }
                    break;

                case "🏁 Return Vehicle":
                    try
                    {
                        await ReturnVehicleAsync(returnService, rentalRepository, vehicleCatalog);
                    }
                    catch (OperationCanceledException)
                    {
                        AnsiConsole.MarkupLine("\n[yellow]Return cancelled.[/]");
                    }
                    break;

                case "📊 List All Rentals":
                    await ListRentalsAsync(rentalRepository);
                    break;

                case "🚪 Exit":
                    AnsiConsole.MarkupLine("\n[cyan]👋 Thank you for using Vehicle Rental Management System![/]");
                    return;
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(
                new Panel(new Markup($"[red]{ex.Message.EscapeMarkup()}[/]"))
                {
                    Header = new PanelHeader("[red]Error[/]"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Red)
                });
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        Console.ReadKey(true);
        AnsiConsole.Clear();
        DisplayHeader(serverConfig);
    }
}

static async Task ListVehicleTypesAsync(IVehicleTypeStore store)
{
    var vehicleTypes = await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Loading vehicle types...", async ctx => await store.GetAllAsync());

    if (!vehicleTypes.Any())
    {
        AnsiConsole.MarkupLine("\n[yellow]No vehicle types available.[/]");
        return;
    }

    var table = new Table()
        .Border(TableBorder.Rounded)
        .BorderColor(Color.Cyan1)
        .AddColumn(new TableColumn("[cyan1]ID[/]").Centered())
        .AddColumn(new TableColumn("[cyan1]Name[/]"))
        .AddColumn(new TableColumn("[cyan1]Pricing Formula[/]"))
        .AddColumn(new TableColumn("[cyan1]Description[/]"));

    foreach (var vt in vehicleTypes)
    {
        table.AddRow(
            $"[cyan]{vt.VehicleTypeId.EscapeMarkup()}[/]",
            $"[white]{vt.DisplayName.EscapeMarkup()}[/]",
            $"[grey]{vt.PricingFormula.EscapeMarkup()}[/]",
            $"[grey]{(string.IsNullOrEmpty(vt.Description) ? "-" : vt.Description.EscapeMarkup())}[/]"
        );
    }

    AnsiConsole.WriteLine();
    AnsiConsole.Write(
        new Panel(table)
        {
            Header = new PanelHeader(" 📋 Vehicle Types ", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1)
        });
}

static async Task CheckoutVehicleAsync(CheckoutService service, IVehicleTypeStore store, IVehicleCatalog catalog, IRentalRepository repository)
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(
        new Rule("[cyan1]🚗 Vehicle Checkout[/]")
        {
            Justification = Justify.Left,
            Style = new Style(Color.Cyan1)
        });
    AnsiConsole.WriteLine();

    // Step 1: Select vehicle type
    var vehicleTypes = await store.GetAllAsync();
    var typesList = vehicleTypes.ToList();

    if (!typesList.Any())
    {
        AnsiConsole.MarkupLine("[red]No vehicle types available.[/]");
        return;
    }

    // Add a special marker for cancel option
    var cancelType = new VehicleTypeDefinition
    {
        VehicleTypeId = "__CANCEL__",
        DisplayName = "Cancel",
        PricingFormula = "",
        Description = null
    };

    var typesWithCancel = typesList.Concat(new[] { cancelType }).ToList();

    var selectedType = AnsiConsole.Prompt(
        new SelectionPrompt<VehicleTypeDefinition>()
            .Title("[cyan]Select Vehicle Type:[/] [grey](ESC to cancel)[/]")
            .PageSize(10)
            .UseConverter(vt => vt.VehicleTypeId == "__CANCEL__" ? "[red]❌ Cancel[/]" : $"{vt.DisplayName} ({vt.VehicleTypeId})")
            .AddChoices(typesWithCancel));

    if (selectedType.VehicleTypeId == "__CANCEL__")
    {
        throw new OperationCanceledException();
    }

    AnsiConsole.WriteLine();

    // Step 2: Get vehicles of selected type from catalog, excluding rented ones
    var allVehicles = await catalog.GetAllAsync();
    var allRentals = await repository.GetAllAsync();
    var rentedVehicleRegistrations = allRentals
        .Where(r => r.IsActive)
        .Select(r => r.RegistrationNumber)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var availableVehicles = allVehicles
        .Where(v => v.VehicleTypeId.Equals(selectedType.VehicleTypeId, StringComparison.OrdinalIgnoreCase))
        .Where(v => !rentedVehicleRegistrations.Contains(v.RegistrationNumber))
        .ToList();

    if (!availableVehicles.Any())
    {
        AnsiConsole.MarkupLine($"[yellow]No vehicles available for type '{selectedType.DisplayName}'.[/]");
        return;
    }

    // Add a special marker for cancel option
    var cancelVehicle = new Vehicle
    {
        RegistrationNumber = "__CANCEL__",
        VehicleTypeId = selectedType.VehicleTypeId,
        CurrentOdometer = 0,
        Location = "cancel"
    };

    var vehiclesWithCancel = availableVehicles.Concat(new[] { cancelVehicle }).ToList();

    var selectedVehicle = AnsiConsole.Prompt(
        new SelectionPrompt<Vehicle>()
            .Title($"[cyan]Select {selectedType.DisplayName}:[/] [grey](ESC to cancel)[/]")
            .PageSize(10)
            .UseConverter(v => v.RegistrationNumber == "__CANCEL__" ? "[red]❌ Cancel[/]" : $"{v.RegistrationNumber} (Odometer: {v.CurrentOdometer:F0} km)")
            .AddChoices(vehiclesWithCancel));

    if (selectedVehicle.RegistrationNumber == "__CANCEL__")
    {
        throw new OperationCanceledException();
    }

    var registrationNumber = selectedVehicle.RegistrationNumber;
    var vehicleTypeId = selectedType.VehicleTypeId;

    // Generate a unique, user-friendly booking number
    var bookingNumber = $"BK-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";

    AnsiConsole.WriteLine();

    // Step 3: Prompt for customer ID
    var customerId = AnsiConsole.Prompt(
        new TextPrompt<string>("[cyan]Customer ID[/] [grey]or type 'esc' to cancel[/]:")
            .PromptStyle("white"));

    if (customerId.Equals("esc", StringComparison.OrdinalIgnoreCase))
    {
        throw new OperationCanceledException();
    }

    if (string.IsNullOrWhiteSpace(customerId))
    {
        AnsiConsole.MarkupLine("[red]Customer ID is required.[/]");
        return;
    }

    // Step 4: Prompt for checkout timestamp
    var checkoutTimestampInput = AnsiConsole.Prompt(
        new TextPrompt<string>("[cyan]Checkout Timestamp[/] (ISO 8601, e.g., 2024-03-20T10:00:00+01:00) [grey]or type 'esc' to cancel[/]:")
            .PromptStyle("white")
            .DefaultValue(DateTimeOffset.Now.ToString("o"))
            .AllowEmpty());

    if (checkoutTimestampInput.Equals("esc", StringComparison.OrdinalIgnoreCase))
    {
        throw new OperationCanceledException();
    }

    if (!DateTimeOffset.TryParse(checkoutTimestampInput, out var checkoutTimestamp))
    {
        AnsiConsole.MarkupLine("[red]Invalid timestamp format.[/]");
        return;
    }

    // Use vehicle's current odometer as default
    var odometerInput = AnsiConsole.Prompt(
        new TextPrompt<string>("[cyan]Odometer Reading[/] (km) [grey]or type 'esc' to cancel[/]:")
            .PromptStyle("white")
            .DefaultValue(selectedVehicle.CurrentOdometer.ToString())
            .AllowEmpty());

    if (odometerInput.Equals("esc", StringComparison.OrdinalIgnoreCase))
    {
        throw new OperationCanceledException();
    }

    if (!decimal.TryParse(odometerInput, out var odometer) || odometer < 0)
    {
        AnsiConsole.MarkupLine("[red]Invalid odometer reading.[/]");
        return;
    }

    var request = new RegisterCheckoutRequest
    {
        BookingNumber = bookingNumber,
        CustomerId = customerId,
        RegistrationNumber = registrationNumber,
        VehicleTypeId = vehicleTypeId,
        CheckoutTimestamp = checkoutTimestamp,
        CheckoutOdometer = odometer
    };

    var result = await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Processing checkout...", async ctx => await service.RegisterCheckoutAsync(request));

    AnsiConsole.WriteLine();

    if (result.IsSuccess)
    {
        var successTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .HideHeaders()
            .AddColumn("")
            .AddColumn("");

        successTable.AddRow("[green]Booking Number:[/]", $"[white bold]{result.Value!.BookingNumber.EscapeMarkup()}[/]");
        successTable.AddRow("[green]Customer ID:[/]", $"[white]{result.Value.CustomerId.EscapeMarkup()}[/]");
        successTable.AddRow("[green]Registration:[/]", $"[white]{result.Value.RegistrationNumber.EscapeMarkup()}[/]");
        successTable.AddRow("[green]Vehicle Type:[/]", $"[white]{result.Value.VehicleTypeId.EscapeMarkup()}[/]");
        successTable.AddRow("[green]Checkout Time:[/]", $"[white]{result.Value.CheckoutTimestamp:yyyy-MM-dd HH:mm:ss zzz}[/]");

        AnsiConsole.Write(
            new Panel(successTable)
            {
                Header = new PanelHeader(" ✅ Checkout Successful ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Green)
            });
    }
    else
    {
        AnsiConsole.Write(
            new Panel(new Markup($"[red]{result.Error.EscapeMarkup()}[/]"))
            {
                Header = new PanelHeader(" ❌ Checkout Failed ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Red)
            });
    }
}

static async Task ReturnVehicleAsync(ReturnService service, IRentalRepository repository, IVehicleCatalog catalog)
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(
        new Rule("[cyan1]🏁 Vehicle Return[/]")
        {
            Justification = Justify.Left,
            Style = new Style(Color.Cyan1)
        });
    AnsiConsole.WriteLine();

    // Get all active rentals
    var allRentals = await repository.GetAllAsync();
    var activeRentals = allRentals.Where(r => r.IsActive).OrderByDescending(r => r.CheckoutTimestamp).ToList();

    if (!activeRentals.Any())
    {
        AnsiConsole.MarkupLine("[yellow]No active bookings found.[/]");
        return;
    }

    // Create a dummy rental for the cancel option
    var cancelRental = new Rental
    {
        BookingNumber = "__CANCEL__",
        CustomerId = "",
        RegistrationNumber = "",
        VehicleTypeId = "",
        CheckoutTimestamp = DateTimeOffset.MinValue,
        CheckoutOdometer = 0
    };

    var rentalsWithCancel = activeRentals.Concat(new[] { cancelRental }).ToList();

    // Display instruction and header
    AnsiConsole.MarkupLine("[grey]Use arrow keys to select, Enter to confirm:[/]");
    AnsiConsole.MarkupLine("[cyan1]┌────────────────────────┬──────────────┬─────────────────┬──────────────────┬──────────────┐[/]");
    AnsiConsole.MarkupLine("[cyan1]│[/] [bold cyan1]Booking Number[/]         [cyan1]│[/] [bold cyan1]Vehicle[/]      [cyan1]│[/] [bold cyan1]Type[/]            [cyan1]│[/] [bold cyan1]Checked Out[/]      [cyan1]│[/] [bold cyan1]Odometer[/]     [cyan1]│[/]");
    AnsiConsole.MarkupLine("[cyan1]└────────────────────────┴──────────────┴─────────────────┴──────────────────┴──────────────┘[/]");

    var selectedRental = AnsiConsole.Prompt(
        new SelectionPrompt<Rental>()
            .PageSize(10)
            .HighlightStyle(new Style(Color.Cyan1, Color.Grey15))
            .UseConverter(r =>
            {
                if (r.BookingNumber == "__CANCEL__")
                    return "[red]  ❌ Cancel and return to main menu[/]";

                return $"{r.BookingNumber.EscapeMarkup(),23} {r.RegistrationNumber.EscapeMarkup(),14} {r.VehicleTypeId.EscapeMarkup(),17} {r.CheckoutTimestamp.ToLocalTime(),18:yyyy-MM-dd HH:mm} {r.CheckoutOdometer,11:F0} km";
            })

            .AddChoices(rentalsWithCancel));

    if (selectedRental.BookingNumber == "__CANCEL__")
    {
        throw new OperationCanceledException();
    }

    var bookingNumber = selectedRental.BookingNumber;

    // Display checkout details
    AnsiConsole.WriteLine();
    var checkoutInfoTable = new Table()
        .Border(TableBorder.Rounded)
        .BorderColor(Color.Cyan1)
        .HideHeaders()
        .AddColumn("")
        .AddColumn("");

    checkoutInfoTable.AddRow("[cyan]Booking Number:[/]", $"[white]{selectedRental.BookingNumber.EscapeMarkup()}[/]");
    checkoutInfoTable.AddRow("[cyan]Customer ID:[/]", $"[white]{selectedRental.CustomerId.EscapeMarkup()}[/]");
    checkoutInfoTable.AddRow("[cyan]Vehicle:[/]", $"[white]{selectedRental.RegistrationNumber.EscapeMarkup()}[/]");
    checkoutInfoTable.AddRow("[cyan]Type:[/]", $"[white]{selectedRental.VehicleTypeId.EscapeMarkup()}[/]");
    checkoutInfoTable.AddRow("[cyan]Checked Out:[/]", $"[white]{selectedRental.CheckoutTimestamp:yyyy-MM-dd HH:mm}[/]");
    checkoutInfoTable.AddRow("[cyan]Checkout Odometer:[/]", $"[white]{selectedRental.CheckoutOdometer:F0} km[/]");

    AnsiConsole.Write(
        new Panel(checkoutInfoTable)
        {
            Header = new PanelHeader(" 📋 Checkout Details ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1)
        });
    AnsiConsole.WriteLine();

    var returnTimestampInput = AnsiConsole.Prompt(
        new TextPrompt<string>("[cyan]Return Timestamp[/] (yyyy-MM-dd HH:mm, e.g., 2024-03-22 14:30) [grey]or type 'esc' to cancel[/]:")
            .PromptStyle("white")
            .DefaultValue(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm"))
            .AllowEmpty());

    if (returnTimestampInput.Equals("esc", StringComparison.OrdinalIgnoreCase))
    {
        throw new OperationCanceledException();
    }

    if (!DateTimeOffset.TryParse(returnTimestampInput, out var returnTimestamp))
    {
        AnsiConsole.MarkupLine("[red]Invalid timestamp format.[/]");
        return;
    }

    var odometerInput = AnsiConsole.Prompt(
        new TextPrompt<string>($"[cyan]Return Odometer Reading[/] (km) [grey](checkout: {selectedRental.CheckoutOdometer:F0} km) or type 'esc' to cancel[/]:")
            .PromptStyle("white"));

    if (odometerInput.Equals("esc", StringComparison.OrdinalIgnoreCase))
    {
        throw new OperationCanceledException();
    }

    if (!decimal.TryParse(odometerInput, out var odometer) || odometer < 0)
    {
        AnsiConsole.MarkupLine("[red]Invalid odometer reading.[/]");
        return;
    }

    var baseDayRateInput = AnsiConsole.Prompt(
        new TextPrompt<string>("[cyan]Base Day Rate[/] (SEK) [grey]or type 'esc' to cancel[/]:")
            .PromptStyle("white"));

    if (baseDayRateInput.Equals("esc", StringComparison.OrdinalIgnoreCase))
    {
        throw new OperationCanceledException();
    }

    if (!decimal.TryParse(baseDayRateInput, out var baseDayRate))
    {
        AnsiConsole.MarkupLine("[red]Invalid base day rate.[/]");
        return;
    }

    var baseKmPriceInput = AnsiConsole.Prompt(
        new TextPrompt<string>("[cyan]Base Kilometer Price[/] (SEK) [grey]or type 'esc' to cancel[/]:")
            .PromptStyle("white"));

    if (baseKmPriceInput.Equals("esc", StringComparison.OrdinalIgnoreCase))
    {
        throw new OperationCanceledException();
    }

    if (!decimal.TryParse(baseKmPriceInput, out var baseKmPrice))
    {
        AnsiConsole.MarkupLine("[red]Invalid base kilometer price.[/]");
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

    var result = await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Processing return and calculating price...", async ctx => await service.RegisterReturnAsync(request));

    AnsiConsole.WriteLine();

    if (result.IsSuccess)
    {
        var response = result.Value!;

        // Update the vehicle's odometer in the catalog
        await catalog.UpdateOdometerAsync(selectedRental.RegistrationNumber, odometer);

        var invoiceTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .HideHeaders()
            .AddColumn("")
            .AddColumn(new TableColumn("").RightAligned());

        invoiceTable.AddRow("[cyan]Booking Number:[/]", $"[white]{response.BookingNumber.EscapeMarkup()}[/]");
        invoiceTable.AddEmptyRow();
        invoiceTable.AddRow("[grey]Days:[/]", $"[white]{response.Days}[/]");
        invoiceTable.AddRow("[grey]Kilometers:[/]", $"[white]{response.KilometersDriven:F2} km[/]");
        invoiceTable.AddEmptyRow();
        invoiceTable.AddRow("[green bold]TOTAL:[/]", $"[green bold]{response.RentalPrice:F2} SEK[/]");

        AnsiConsole.Write(
            new Panel(invoiceTable)
            {
                Header = new PanelHeader(" 🧾 INVOICE ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Green)
            });
    }
    else
    {
        AnsiConsole.Write(
            new Panel(new Markup($"[red]{result.Error.EscapeMarkup()}[/]"))
            {
                Header = new PanelHeader(" ❌ Return Failed ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Red)
            });
    }
}

static async Task ListRentalsAsync(IRentalRepository repository)
{
    var rentals = await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Loading rentals...", async ctx => await repository.GetAllAsync());

    var rentalsList = rentals.ToList();

    if (!rentalsList.Any())
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]No rentals found.[/]");
        return;
    }

    var activeRentals = rentalsList.Where(r => r.IsActive).ToList();
    var completedRentals = rentalsList.Where(r => !r.IsActive).ToList();

    AnsiConsole.WriteLine();

    if (activeRentals.Any())
    {
        var activeTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow)
            .AddColumn(new TableColumn("[yellow]Booking #[/]"))
            .AddColumn(new TableColumn("[yellow]Customer[/]"))
            .AddColumn(new TableColumn("[yellow]Vehicle[/]"))
            .AddColumn(new TableColumn("[yellow]Type[/]"))
            .AddColumn(new TableColumn("[yellow]Checked Out[/]"))
            .AddColumn(new TableColumn("[yellow]Odometer[/]").RightAligned());

        foreach (var rental in activeRentals)
        {
            activeTable.AddRow(
                $"[white]{rental.BookingNumber.EscapeMarkup()}[/]",
                $"[white]{rental.CustomerId.EscapeMarkup()}[/]",
                $"[white]{rental.RegistrationNumber.EscapeMarkup()}[/]",
                $"[grey]{rental.VehicleTypeId.EscapeMarkup()}[/]",
                $"[white]{rental.CheckoutTimestamp:yyyy-MM-dd HH:mm}[/]",
                $"[white]{rental.CheckoutOdometer:F1} km[/]"
            );
        }

        AnsiConsole.Write(
            new Panel(activeTable)
            {
                Header = new PanelHeader($" 🚗 Active Rentals ({activeRentals.Count}) ", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow)
            });

        AnsiConsole.WriteLine();
    }

    if (completedRentals.Any())
    {
        var completedTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .AddColumn(new TableColumn("[green]Booking #[/]"))
            .AddColumn(new TableColumn("[green]Customer[/]"))
            .AddColumn(new TableColumn("[green]Vehicle[/]"))
            .AddColumn(new TableColumn("[green]Type[/]"))
            .AddColumn(new TableColumn("[green]Returned[/]"))
            .AddColumn(new TableColumn("[green]Distance[/]").RightAligned())
            .AddColumn(new TableColumn("[green]Price[/]").RightAligned());

        foreach (var rental in completedRentals)
        {
            var distance = rental.ReturnOdometer!.Value - rental.CheckoutOdometer;
            completedTable.AddRow(
                $"[white]{rental.BookingNumber.EscapeMarkup()}[/]",
                $"[white]{rental.CustomerId.EscapeMarkup()}[/]",
                $"[white]{rental.RegistrationNumber.EscapeMarkup()}[/]",
                $"[grey]{rental.VehicleTypeId.EscapeMarkup()}[/]",
                $"[white]{rental.ReturnTimestamp:yyyy-MM-dd HH:mm}[/]",
                $"[white]{distance:F1} km[/]",
                $"[green]{rental.RentalPrice:F2} SEK[/]"
            );
        }

        AnsiConsole.Write(
            new Panel(completedTable)
            {
                Header = new PanelHeader($" 🏁 Completed Rentals ({completedRentals.Count}) ", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Green)
            });
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[grey]Total: {rentalsList.Count} rentals ({activeRentals.Count} active, {completedRentals.Count} completed)[/]");
}

