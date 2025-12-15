using Spectre.Console;
using VehicleRental.CLI.UI;
using VehicleRental.Core.Application;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Ports;

namespace VehicleRental.CLI.Commands;

/// <summary>
/// Command to return a rented vehicle and calculate the rental price.
/// </summary>
public class ReturnCommand(
    ReturnService returnService,
    IRentalRepository rentalRepository,
    IVehicleCatalog vehicleCatalog)
{
    private readonly ReturnService _returnService = returnService;
    private readonly IRentalRepository _rentalRepository = rentalRepository;
    private readonly IVehicleCatalog _vehicleCatalog = vehicleCatalog;

    public async Task ExecuteAsync()
    {
        ConsoleRenderer.DisplaySectionHeader("Vehicle Return", "🏁");

        // Get active rentals
        var allRentals = await _rentalRepository.GetAllAsync();
        var activeRentals = allRentals
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.CheckoutTimestamp)
            .ToList();

        if (activeRentals.Count == 0)
        {
            ConsoleRenderer.DisplayWarning("No active bookings found.");
            return;
        }

        // Select rental
        var rentalResult = ConsolePrompts.SelectRental(activeRentals);
        if (rentalResult.IsCancelled || rentalResult.Value == null)
        {
            throw new OperationCanceledException();
        }

        var selectedRental = rentalResult.Value;

        // Display checkout details
        AnsiConsole.WriteLine();
        ConsoleRenderer.DisplayInfoPanel(
            "📋 Checkout Details",
            ("Booking Number", selectedRental.BookingNumber),
            ("Customer ID", selectedRental.CustomerId),
            ("Vehicle", selectedRental.RegistrationNumber),
            ("Type", selectedRental.VehicleTypeId),
            ("Checked Out", selectedRental.CheckoutTimestamp.ToString("yyyy-MM-dd HH:mm")),
            ("Checkout Odometer", $"{selectedRental.CheckoutOdometer:F0} km"));

        AnsiConsole.WriteLine();

        // Return timestamp
        var timestampResult = ConsolePrompts.PromptTimestamp(
            "Return Timestamp (yyyy-MM-dd HH:mm, e.g., 2024-03-22 14:30)",
            DateTimeOffset.Now);

        if (timestampResult.IsCancelled)
        {
            throw new OperationCanceledException();
        }

        if (timestampResult.Value == default)
        {
            return; // Invalid input was already displayed
        }

        // Return odometer
        var odometerResult = ConsolePrompts.PromptDecimal(
            $"Return Odometer Reading (km) [grey](checkout: {selectedRental.CheckoutOdometer:F0} km)[/]");

        if (odometerResult.IsCancelled)
        {
            throw new OperationCanceledException();
        }

        if (odometerResult.Value < 0)
        {
            return;
        }

        // Base day rate
        var baseDayRateResult = ConsolePrompts.PromptDecimal("Base Day Rate (SEK)");
        if (baseDayRateResult.IsCancelled)
        {
            throw new OperationCanceledException();
        }

        // Base km price
        var baseKmPriceResult = ConsolePrompts.PromptDecimal("Base Kilometer Price (SEK)");
        if (baseKmPriceResult.IsCancelled)
        {
            throw new OperationCanceledException();
        }

        var request = new RegisterReturnRequest
        {
            BookingNumber = selectedRental.BookingNumber,
            ReturnTimestamp = timestampResult.Value,
            ReturnOdometer = odometerResult.Value,
            PricingParameters = new PricingParameters
            {
                BaseDayRate = baseDayRateResult.Value,
                BaseKmPrice = baseKmPriceResult.Value
            }
        };

        var result = await ConsoleRenderer.WithSpinnerAsync(
            "Processing return and calculating price...",
            async () => await _returnService.RegisterReturnAsync(request));

        AnsiConsole.WriteLine();

        if (result.IsSuccess)
        {
            var response = result.Value!;

            // Update vehicle odometer in catalog
            await _vehicleCatalog.UpdateOdometerAsync(selectedRental.RegistrationNumber, odometerResult.Value);

            ConsoleRenderer.DisplayInvoice(
                response.BookingNumber,
                response.Days,
                response.KilometersDriven,
                response.RentalPrice);
        }
        else
        {
            ConsoleRenderer.DisplayFailurePanel("❌ Return Failed", result.Error ?? "Unknown error");
        }
    }
}
