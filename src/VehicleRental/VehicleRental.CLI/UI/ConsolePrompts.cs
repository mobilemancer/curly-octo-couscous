using Spectre.Console;
using VehicleRental.Core.Domain;

namespace VehicleRental.CLI.UI;

/// <summary>
/// Provides reusable console prompt components with consistent cancel handling.
/// </summary>
public static class ConsolePrompts
{
    private const string CancelMarker = "__CANCEL__";

    /// <summary>
    /// Result of a prompt that can be cancelled.
    /// </summary>
    public readonly record struct PromptResult<T>(bool IsCancelled, T? Value);

    /// <summary>
    /// Prompts the user to select from a list of vehicle types.
    /// </summary>
    public static PromptResult<VehicleTypeDefinition> SelectVehicleType(
        IEnumerable<VehicleTypeDefinition> vehicleTypes,
        string title = "Select Vehicle Type:")
    {
        var typesList = vehicleTypes.ToList();

        if (typesList.Count == 0)
        {
            return new PromptResult<VehicleTypeDefinition>(false, null);
        }

        var cancelType = new VehicleTypeDefinition
        {
            VehicleTypeId = CancelMarker,
            DisplayName = "Cancel",
            PricingFormula = "",
            Description = null
        };

        var typesWithCancel = typesList.Concat([cancelType]).ToList();

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<VehicleTypeDefinition>()
                .Title($"[cyan]{title}[/] [grey](ESC to cancel)[/]")
                .PageSize(10)
                .UseConverter(vt => vt.VehicleTypeId == CancelMarker
                    ? "[red]❌ Cancel[/]"
                    : $"{vt.DisplayName} ({vt.VehicleTypeId})")
                .AddChoices(typesWithCancel));

        return selected.VehicleTypeId == CancelMarker
            ? new PromptResult<VehicleTypeDefinition>(true, null)
            : new PromptResult<VehicleTypeDefinition>(false, selected);
    }

    /// <summary>
    /// Prompts the user to select from a list of vehicles.
    /// </summary>
    public static PromptResult<Vehicle> SelectVehicle(
        IEnumerable<Vehicle> vehicles,
        string vehicleTypeName,
        string title = "Select Vehicle:")
    {
        var vehiclesList = vehicles.ToList();

        if (vehiclesList.Count == 0)
        {
            return new PromptResult<Vehicle>(false, null);
        }

        var cancelVehicle = new Vehicle
        {
            RegistrationNumber = CancelMarker,
            VehicleTypeId = "",
            CurrentOdometer = 0,
            Location = "cancel"
        };

        var vehiclesWithCancel = vehiclesList.Concat([cancelVehicle]).ToList();

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<Vehicle>()
                .Title($"[cyan]Select {vehicleTypeName}:[/] [grey](ESC to cancel)[/]")
                .PageSize(10)
                .UseConverter(v => v.RegistrationNumber == CancelMarker
                    ? "[red]❌ Cancel[/]"
                    : $"{v.RegistrationNumber} (Odometer: {v.CurrentOdometer:F0} km)")
                .AddChoices(vehiclesWithCancel));

        return selected.RegistrationNumber == CancelMarker
            ? new PromptResult<Vehicle>(true, null)
            : new PromptResult<Vehicle>(false, selected);
    }

    /// <summary>
    /// Prompts the user to select from a list of active rentals.
    /// </summary>
    public static PromptResult<Rental> SelectRental(IEnumerable<Rental> rentals)
    {
        var rentalsList = rentals.ToList();

        if (rentalsList.Count == 0)
        {
            return new PromptResult<Rental>(false, null);
        }

        var cancelRental = new Rental
        {
            BookingNumber = CancelMarker,
            CustomerId = "",
            RegistrationNumber = "",
            VehicleTypeId = "",
            CheckoutTimestamp = DateTimeOffset.MinValue,
            CheckoutOdometer = 0
        };

        var rentalsWithCancel = rentalsList.Concat([cancelRental]).ToList();

        // Display header for the selection
        AnsiConsole.MarkupLine("[grey]Use arrow keys to select, Enter to confirm:[/]");
        AnsiConsole.MarkupLine("[cyan1]┌────────────────────────┬──────────────┬─────────────────┬──────────────────┬──────────────┐[/]");
        AnsiConsole.MarkupLine("[cyan1]│[/] [bold cyan1]Booking Number[/]         [cyan1]│[/] [bold cyan1]Vehicle[/]      [cyan1]│[/] [bold cyan1]Type[/]            [cyan1]│[/] [bold cyan1]Checked Out[/]      [cyan1]│[/] [bold cyan1]Odometer[/]     [cyan1]│[/]");
        AnsiConsole.MarkupLine("[cyan1]└────────────────────────┴──────────────┴─────────────────┴──────────────────┴──────────────┘[/]");

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<Rental>()
                .PageSize(10)
                .HighlightStyle(new Style(Color.Cyan1, Color.Grey15))
                .UseConverter(r =>
                {
                    if (r.BookingNumber == CancelMarker)
                        return "[red]  ❌ Cancel and return to main menu[/]";

                    return $"{r.BookingNumber.EscapeMarkup(),23} {r.RegistrationNumber.EscapeMarkup(),14} {r.VehicleTypeId.EscapeMarkup(),17} {r.CheckoutTimestamp.ToLocalTime(),18:yyyy-MM-dd HH:mm} {r.CheckoutOdometer,11:F0} km";
                })
                .AddChoices(rentalsWithCancel));

        return selected.BookingNumber == CancelMarker
            ? new PromptResult<Rental>(true, null)
            : new PromptResult<Rental>(false, selected);
    }

    /// <summary>
    /// Prompts for text input with cancel support.
    /// </summary>
    public static PromptResult<string> PromptText(
        string prompt,
        string? defaultValue = null,
        bool allowEmpty = false)
    {
        var textPrompt = new TextPrompt<string>($"[cyan]{prompt}[/] [grey]or type 'esc' to cancel[/]:")
            .PromptStyle("white");

        if (defaultValue != null)
        {
            textPrompt.DefaultValue(defaultValue);
        }

        if (allowEmpty || defaultValue != null)
        {
            textPrompt.AllowEmpty();
        }

        var input = AnsiConsole.Prompt(textPrompt);

        if (input.Equals("esc", StringComparison.OrdinalIgnoreCase))
        {
            return new PromptResult<string>(true, null);
        }

        return new PromptResult<string>(false, input);
    }

    /// <summary>
    /// Prompts for a timestamp with cancel support.
    /// </summary>
    public static PromptResult<DateTimeOffset> PromptTimestamp(
        string prompt,
        DateTimeOffset? defaultValue = null)
    {
        var defaultStr = (defaultValue ?? DateTimeOffset.Now).ToString("yyyy-MM-dd HH:mm");

        var result = PromptText(prompt, defaultStr, allowEmpty: true);

        if (result.IsCancelled)
        {
            return new PromptResult<DateTimeOffset>(true, default);
        }

        if (!DateTimeOffset.TryParse(result.Value, out var timestamp))
        {
            AnsiConsole.MarkupLine("[red]Invalid timestamp format.[/]");
            return new PromptResult<DateTimeOffset>(false, default);
        }

        return new PromptResult<DateTimeOffset>(false, timestamp);
    }

    /// <summary>
    /// Prompts for a decimal number with cancel support.
    /// </summary>
    public static PromptResult<decimal> PromptDecimal(
        string prompt,
        decimal? defaultValue = null,
        bool requirePositive = true)
    {
        var result = PromptText(prompt, defaultValue?.ToString(), allowEmpty: defaultValue.HasValue);

        if (result.IsCancelled)
        {
            return new PromptResult<decimal>(true, default);
        }

        if (!decimal.TryParse(result.Value, out var value))
        {
            AnsiConsole.MarkupLine("[red]Invalid number format.[/]");
            return new PromptResult<decimal>(false, default);
        }

        if (requirePositive && value < 0)
        {
            AnsiConsole.MarkupLine("[red]Value must be positive.[/]");
            return new PromptResult<decimal>(false, default);
        }

        return new PromptResult<decimal>(false, value);
    }

    /// <summary>
    /// Displays the main menu and returns the selected option.
    /// </summary>
    public static string ShowMainMenu()
    {
        AnsiConsole.MarkupLine("[cyan1]What would you like to do?[/]");
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .PageSize(10)
                .AddChoices(
                [
                    "📋 List Vehicle Types",
                    "🚗 Check Out Vehicle",
                    "🏁 Return Vehicle",
                    "📊 List All Rentals",
                    "🚪 Exit"
                ]));
    }
}
