using VehicleRental.CLI.UI;
using VehicleRental.Core.Ports;

namespace VehicleRental.CLI.Commands;

/// <summary>
/// Command to list all rentals (active and completed).
/// </summary>
public class ListRentalsCommand
{
    private readonly IRentalRepository _rentalRepository;

    public ListRentalsCommand(IRentalRepository rentalRepository)
    {
        _rentalRepository = rentalRepository;
    }

    public async Task ExecuteAsync()
    {
        var rentals = await ConsoleRenderer.WithSpinnerAsync(
            "Loading rentals...",
            async () => await _rentalRepository.GetAllAsync());

        var rentalsList = rentals.ToList();

        if (!rentalsList.Any())
        {
            ConsoleRenderer.DisplayWarning("No rentals found.");
            return;
        }

        var activeRentals = rentalsList.Where(r => r.IsActive).ToList();
        var completedRentals = rentalsList.Where(r => !r.IsActive).ToList();

        ConsoleRenderer.DisplayRentals(activeRentals, completedRentals);
    }
}
