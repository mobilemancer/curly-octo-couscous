using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
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
                    await CheckoutVehicleAsync(checkoutService, vehicleTypeStore);
                    break;

                case "🏁 Return Vehicle":
                    await ReturnVehicleAsync(returnService, rentalRepository);
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

static async Task CheckoutVehicleAsync(CheckoutService service, IVehicleTypeStore store)
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(
        new Rule("[cyan1]🚗 Vehicle Checkout[/]")
        {
            Justification = Justify.Left,
            Style = new Style(Color.Cyan1)
        });
    AnsiConsole.WriteLine();

    // Show available types
    var vehicleTypes = await store.GetAllAsync();
    var typesList = vehicleTypes.ToList();

    if (typesList.Any())
    {
        var typesTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn("[grey]Type ID[/]")
            .AddColumn("[grey]Name[/]");

        foreach (var vt in typesList)
        {
            typesTable.AddRow(vt.VehicleTypeId.EscapeMarkup(), vt.DisplayName.EscapeMarkup());
        }

        AnsiConsole.Write(typesTable);
        AnsiConsole.WriteLine();
    }

    var registrationNumber = AnsiConsole.Prompt(
        new TextPrompt<string>("[cyan]Vehicle Registration Number[/] (e.g., license plate, callsign):")
            .PromptStyle("white")
            .ValidationErrorMessage("[red]Registration number is required[/]")
            .Validate(input => !string.IsNullOrWhiteSpace(input)));

    // Generate a unique, user-friendly booking number
    var bookingNumber = $"BK-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";

    var vehicleTypeId = AnsiConsole.Prompt(
        new TextPrompt<string>("[cyan]Vehicle Type ID[/] (optional, press Enter to skip):")
            .PromptStyle("white")
            .AllowEmpty());

    var checkoutTimestamp = AnsiConsole.Prompt(
        new TextPrompt<DateTimeOffset>("[cyan]Checkout Timestamp[/] (ISO 8601, e.g., 2024-03-20T10:00:00+01:00):")
            .PromptStyle("white")
            .ValidationErrorMessage("[red]Invalid timestamp format[/]"));

    var odometer = AnsiConsole.Prompt(
        new TextPrompt<decimal>("[cyan]Odometer Reading[/] (km):")
            .PromptStyle("white")
            .ValidationErrorMessage("[red]Invalid odometer reading[/]")
            .Validate(value => value >= 0 ? ValidationResult.Success() : ValidationResult.Error("[red]Odometer cannot be negative[/]")));

    var request = new RegisterCheckoutRequest
    {
        BookingNumber = bookingNumber,
        RegistrationNumber = registrationNumber,
        VehicleTypeId = string.IsNullOrWhiteSpace(vehicleTypeId) ? null : vehicleTypeId,
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
        new TextPrompt<string>("[cyan]Booking Number:[/]")
            .PromptStyle("white")
            .ValidationErrorMessage("[red]Booking number is required[/]")
            .Validate(input => !string.IsNullOrWhiteSpace(input)));

    var returnTimestamp = AnsiConsole.Prompt(
        new TextPrompt<DateTimeOffset>("[cyan]Return Timestamp[/] (ISO 8601, e.g., 2024-03-22T14:30:00+01:00):")
            .PromptStyle("white")
            .ValidationErrorMessage("[red]Invalid timestamp format[/]"));

    var odometer = AnsiConsole.Prompt(
        new TextPrompt<decimal>("[cyan]Odometer Reading[/] (km):")
            .PromptStyle("white")
            .ValidationErrorMessage("[red]Invalid odometer reading[/]")
            .Validate(value => value >= 0 ? ValidationResult.Success() : ValidationResult.Error("[red]Odometer cannot be negative[/]")));

    var baseDayRate = AnsiConsole.Prompt(
        new TextPrompt<decimal>("[cyan]Base Day Rate[/] (SEK):")
            .PromptStyle("white")
            .ValidationErrorMessage("[red]Invalid base day rate[/]"));

    var baseKmPrice = AnsiConsole.Prompt(
        new TextPrompt<decimal>("[cyan]Base Kilometer Price[/] (SEK):")
            .PromptStyle("white")
            .ValidationErrorMessage("[red]Invalid base kilometer price[/]"));

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

