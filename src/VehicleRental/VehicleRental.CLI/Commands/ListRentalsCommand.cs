using VehicleRental.CLI.UI;
using VehicleRental.Core.Ports;

namespace VehicleRental.CLI.Commands;

/// <summary>
/// Command to list all rentals (active and completed).
/// </summary>
public class ListRentalsCommand(IRentalRepository rentalRepository)
{
    private readonly IRentalRepository _rentalRepository = rentalRepository;

    public async Task ExecuteAsync()
    {
        var rentals = await ConsoleRenderer.WithSpinnerAsync(
            "Loading rentals...",
            async () => await _rentalRepository.GetAllAsync());

        var rentalsList = rentals.ToList();

        if (rentalsList.Count == 0)
        {
            ConsoleRenderer.DisplayWarning("No rentals found.");
            return;
        }

        var activeRentals = rentalsList.Where(r => r.IsActive).ToList();
        var completedRentals = rentalsList.Where(r => !r.IsActive).ToList();

        ConsoleRenderer.DisplayRentals(activeRentals, completedRentals);
    }
}
