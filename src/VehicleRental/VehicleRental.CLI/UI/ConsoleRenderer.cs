using Spectre.Console;
using VehicleRental.CLI.Configuration;
using VehicleRental.Core.Domain;

namespace VehicleRental.CLI.UI;

/// <summary>
/// Provides reusable console rendering components for the CLI application.
/// </summary>
public static class ConsoleRenderer
{
    /// <summary>
    /// Displays the application header with banner and connection status.
    /// </summary>
    public static void DisplayHeader(ServerConfiguration serverConfig)
    {
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

    /// <summary>
    /// Displays a section header with a rule.
    /// </summary>
    public static void DisplaySectionHeader(string title, string emoji)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Rule($"[cyan1]{emoji} {title}[/]")
            {
                Justification = Justify.Left,
                Style = new Style(Color.Cyan1)
            });
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays an error message in a styled panel.
    /// </summary>
    public static void DisplayError(string message)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Panel(new Markup($"[red]{message.EscapeMarkup()}[/]"))
            {
                Header = new PanelHeader("[red]Error[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Red)
            });
    }

    /// <summary>
    /// Displays a warning message.
    /// </summary>
    public static void DisplayWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]{message.EscapeMarkup()}[/]");
    }

    /// <summary>
    /// Displays a success panel with key-value pairs.
    /// </summary>
    public static void DisplaySuccessPanel(string header, params (string Label, string Value)[] items)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .HideHeaders()
            .AddColumn("")
            .AddColumn("");

        foreach (var (label, value) in items)
        {
            table.AddRow($"[green]{label}:[/]", $"[white]{value.EscapeMarkup()}[/]");
        }

        AnsiConsole.Write(
            new Panel(table)
            {
                Header = new PanelHeader($" {header} ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Green)
            });
    }

    /// <summary>
    /// Displays a failure panel with an error message.
    /// </summary>
    public static void DisplayFailurePanel(string header, string errorMessage)
    {
        AnsiConsole.Write(
            new Panel(new Markup($"[red]{errorMessage.EscapeMarkup()}[/]"))
            {
                Header = new PanelHeader($" {header} ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Red)
            });
    }

    /// <summary>
    /// Displays an info panel with key-value pairs.
    /// </summary>
    public static void DisplayInfoPanel(string header, params (string Label, string Value)[] items)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .HideHeaders()
            .AddColumn("")
            .AddColumn("");

        foreach (var (label, value) in items)
        {
            table.AddRow($"[cyan]{label}:[/]", $"[white]{value.EscapeMarkup()}[/]");
        }

        AnsiConsole.Write(
            new Panel(table)
            {
                Header = new PanelHeader($" {header} ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Cyan1)
            });
    }

    /// <summary>
    /// Displays a table of vehicle types.
    /// </summary>
    public static void DisplayVehicleTypesTable(IEnumerable<VehicleTypeDefinition> vehicleTypes)
    {
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

    /// <summary>
    /// Displays active and completed rentals tables.
    /// </summary>
    public static void DisplayRentals(IReadOnlyList<Rental> activeRentals, IReadOnlyList<Rental> completedRentals)
    {
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

        var totalCount = activeRentals.Count + completedRentals.Count;
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]Total: {totalCount} rentals ({activeRentals.Count} active, {completedRentals.Count} completed)[/]");
    }

    /// <summary>
    /// Displays an invoice panel for a completed rental.
    /// </summary>
    public static void DisplayInvoice(string bookingNumber, int days, decimal kilometersDriven, decimal totalPrice)
    {
        var invoiceTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .HideHeaders()
            .AddColumn("")
            .AddColumn(new TableColumn("").RightAligned());

        invoiceTable.AddRow("[cyan]Booking Number:[/]", $"[white]{bookingNumber.EscapeMarkup()}[/]");
        invoiceTable.AddEmptyRow();
        invoiceTable.AddRow("[grey]Days:[/]", $"[white]{days}[/]");
        invoiceTable.AddRow("[grey]Kilometers:[/]", $"[white]{kilometersDriven:F2} km[/]");
        invoiceTable.AddEmptyRow();
        invoiceTable.AddRow("[green bold]TOTAL:[/]", $"[green bold]{totalPrice:F2} SEK[/]");

        AnsiConsole.Write(
            new Panel(invoiceTable)
            {
                Header = new PanelHeader(" 🧾 INVOICE ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Green)
            });
    }

    /// <summary>
    /// Waits for user to press a key to continue.
    /// </summary>
    public static void WaitForKeyPress()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        Console.ReadKey(true);
    }

    /// <summary>
    /// Executes an async operation with a loading spinner.
    /// </summary>
    public static async Task<T> WithSpinnerAsync<T>(string message, Func<Task<T>> operation)
    {
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(message, async ctx => await operation());
    }
}
