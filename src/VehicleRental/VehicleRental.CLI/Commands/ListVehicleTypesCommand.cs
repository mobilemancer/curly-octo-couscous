using VehicleRental.CLI.UI;
using VehicleRental.Core.Ports;

namespace VehicleRental.CLI.Commands;

/// <summary>
/// Command to list all available vehicle types.
/// </summary>
public class ListVehicleTypesCommand(IVehicleTypeStore vehicleTypeStore)
{
    private readonly IVehicleTypeStore _vehicleTypeStore = vehicleTypeStore;

    public async Task ExecuteAsync()
    {
        var vehicleTypes = await ConsoleRenderer.WithSpinnerAsync(
            "Loading vehicle types...",
            async () => await _vehicleTypeStore.GetAllAsync());

        var typesList = vehicleTypes.ToList();

        if (typesList.Count == 0)
        {
            ConsoleRenderer.DisplayWarning("No vehicle types available.");
            return;
        }

        ConsoleRenderer.DisplayVehicleTypesTable(typesList);
    }
}
