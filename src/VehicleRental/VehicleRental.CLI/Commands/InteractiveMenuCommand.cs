using Spectre.Console;
using VehicleRental.CLI.Configuration;
using VehicleRental.CLI.UI;

namespace VehicleRental.CLI.Commands;

/// <summary>
/// Orchestrates the interactive menu loop, delegating to individual commands.
/// </summary>
public class InteractiveMenuCommand
{
    private readonly ServerConfiguration _serverConfig;
    private readonly ListVehicleTypesCommand _listVehicleTypesCommand;
    private readonly CheckoutCommand _checkoutCommand;
    private readonly ReturnCommand _returnCommand;
    private readonly ListRentalsCommand _listRentalsCommand;

    public InteractiveMenuCommand(
        ServerConfiguration serverConfig,
        ListVehicleTypesCommand listVehicleTypesCommand,
        CheckoutCommand checkoutCommand,
        ReturnCommand returnCommand,
        ListRentalsCommand listRentalsCommand)
    {
        _serverConfig = serverConfig;
        _listVehicleTypesCommand = listVehicleTypesCommand;
        _checkoutCommand = checkoutCommand;
        _returnCommand = returnCommand;
        _listRentalsCommand = listRentalsCommand;
    }

    public async Task RunAsync()
    {
        AnsiConsole.Clear();
        ConsoleRenderer.DisplayHeader(_serverConfig);

        while (true)
        {
            var command = ConsolePrompts.ShowMainMenu();

            try
            {
                switch (command)
                {
                    case "📋 List Vehicle Types":
                        await _listVehicleTypesCommand.ExecuteAsync();
                        break;

                    case "🚗 Check Out Vehicle":
                        try
                        {
                            await _checkoutCommand.ExecuteAsync();
                        }
                        catch (OperationCanceledException)
                        {
                            AnsiConsole.MarkupLine("\n[yellow]Checkout cancelled.[/]");
                        }
                        break;

                    case "🏁 Return Vehicle":
                        try
                        {
                            await _returnCommand.ExecuteAsync();
                        }
                        catch (OperationCanceledException)
                        {
                            AnsiConsole.MarkupLine("\n[yellow]Return cancelled.[/]");
                        }
                        break;

                    case "📊 List All Rentals":
                        await _listRentalsCommand.ExecuteAsync();
                        break;

                    case "🚪 Exit":
                        AnsiConsole.MarkupLine("\n[cyan]👋 Thank you for using Vehicle Rental Management System![/]");
                        return;
                }
            }
            catch (Exception ex)
            {
                ConsoleRenderer.DisplayError(ex.Message);
            }

            ConsoleRenderer.WaitForKeyPress();
            AnsiConsole.Clear();
            ConsoleRenderer.DisplayHeader(_serverConfig);
        }
    }
}
