using Spectre.Console;
using VehicleRental.CLI.Configuration;
using VehicleRental.CLI.UI;

namespace VehicleRental.CLI.Commands;

/// <summary>
/// Orchestrates the interactive menu loop, delegating to individual commands.
/// </summary>
public class InteractiveMenuCommand(
    ServerConfiguration serverConfig,
    ListVehicleTypesCommand listVehicleTypesCommand,
    CheckoutCommand checkoutCommand,
    ReturnCommand returnCommand,
    ListRentalsCommand listRentalsCommand)
{
    private readonly ServerConfiguration _serverConfig = serverConfig;
    private readonly ListVehicleTypesCommand _listVehicleTypesCommand = listVehicleTypesCommand;
    private readonly CheckoutCommand _checkoutCommand = checkoutCommand;
    private readonly ReturnCommand _returnCommand = returnCommand;
    private readonly ListRentalsCommand _listRentalsCommand = listRentalsCommand;

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
