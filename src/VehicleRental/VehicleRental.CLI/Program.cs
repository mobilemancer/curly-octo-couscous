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
builder.Services.AddSingleton<IVehicleCatalog>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<InMemoryVehicleCatalog>>();
    // mock vehicle repository data
    var vehiclesFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "vehicles.json");
    return new InMemoryVehicleCatalog(vehiclesFilePath, logger);
});

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
                        await CheckoutVehicleAsync(checkoutService, vehicleTypeStore, vehicleCatalog);
                    }
                    catch (OperationCanceledException)
                    {
                        AnsiConsole.MarkupLine("\n[yellow]Checkout cancelled.[/]");
                    }
                    break;

                case "🏁 Return Vehicle":
                    try
                    {
                        await ReturnVehicleAsync(returnService, rentalRepository);
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

static async Task CheckoutVehicleAsync(CheckoutService service, IVehicleTypeStore store, IVehicleCatalog catalog)
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

    // Step 2: Get vehicles of selected type from catalog
    var allVehicles = await catalog.GetAllAsync();
    var availableVehicles = allVehicles
        .Where(v => v.VehicleTypeId.Equals(selectedType.VehicleTypeId, StringComparison.OrdinalIgnoreCase))
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
        CurrentOdometer = 0
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

    // Step 3: Prompt for checkout timestamp
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

static async Task ReturnVehicleAsync(ReturnService service, IRentalRepository repository)
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(
        new Rule("[cyan1]🏁 Vehicle Return[/]")
        {
            Justification = Justify.Left,
            Style = new Style(Color.Cyan1)
        });
    AnsiConsole.WriteLine();

    var bookingNumber = AnsiConsole.Prompt(
        new TextPrompt<string>("[cyan]Booking Number[/] [grey]or type 'esc' to cancel[/]:")
            .PromptStyle("white"));

    if (bookingNumber.Equals("esc", StringComparison.OrdinalIgnoreCase))
    {
        throw new OperationCanceledException();
    }

    var returnTimestampInput = AnsiConsole.Prompt(
        new TextPrompt<string>("[cyan]Return Timestamp[/] (ISO 8601, e.g., 2024-03-22T14:30:00+01:00) [grey]or type 'esc' to cancel[/]:")
            .PromptStyle("white")
            .DefaultValue(DateTimeOffset.Now.ToString("o"))
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
        new TextPrompt<string>("[cyan]Odometer Reading[/] (km) [grey]or type 'esc' to cancel[/]:")
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
            .AddColumn(new TableColumn("[yellow]Vehicle[/]"))
            .AddColumn(new TableColumn("[yellow]Type[/]"))
            .AddColumn(new TableColumn("[yellow]Checked Out[/]"))
            .AddColumn(new TableColumn("[yellow]Odometer[/]").RightAligned());

        foreach (var rental in activeRentals)
        {
            activeTable.AddRow(
                $"[white]{rental.BookingNumber.EscapeMarkup()}[/]",
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

