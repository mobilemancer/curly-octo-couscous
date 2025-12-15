using Spectre.Console;
using VehicleRental.CLI.UI;
using VehicleRental.Core.Application;
using VehicleRental.Core.Ports;

namespace VehicleRental.CLI.Commands;

/// <summary>
/// Command to check out a vehicle to a customer.
/// </summary>
public class CheckoutCommand(
    CheckoutService checkoutService,
    IVehicleTypeStore vehicleTypeStore,
    IVehicleCatalog vehicleCatalog,
    IRentalRepository rentalRepository)
{
    private readonly CheckoutService _checkoutService = checkoutService;
    private readonly IVehicleTypeStore _vehicleTypeStore = vehicleTypeStore;
    private readonly IVehicleCatalog _vehicleCatalog = vehicleCatalog;
    private readonly IRentalRepository _rentalRepository = rentalRepository;

    public async Task ExecuteAsync()
    {
        ConsoleRenderer.DisplaySectionHeader("Vehicle Checkout", "🚗");

        // Step 1: Select vehicle type
        var vehicleTypes = await _vehicleTypeStore.GetAllAsync();
        var typesList = vehicleTypes.ToList();

        if (typesList.Count == 0)
        {
            ConsoleRenderer.DisplayError("No vehicle types available.");
            return;
        }

        var typeResult = ConsolePrompts.SelectVehicleType(typesList);
        if (typeResult.IsCancelled || typeResult.Value == null)
        {
            throw new OperationCanceledException();
        }

        var selectedType = typeResult.Value;
        AnsiConsole.WriteLine();

        // Step 2: Get available vehicles of selected type
        var allVehicles = await _vehicleCatalog.GetAllAsync();
        var allRentals = await _rentalRepository.GetAllAsync();
        var rentedVehicleRegistrations = allRentals
            .Where(r => r.IsActive)
            .Select(r => r.RegistrationNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var availableVehicles = allVehicles
            .Where(v => v.VehicleTypeId.Equals(selectedType.VehicleTypeId, StringComparison.OrdinalIgnoreCase))
            .Where(v => !rentedVehicleRegistrations.Contains(v.RegistrationNumber))
            .ToList();

        if (availableVehicles.Count == 0)
        {
            ConsoleRenderer.DisplayWarning($"No vehicles available for type '{selectedType.DisplayName}'.");
            return;
        }

        var vehicleResult = ConsolePrompts.SelectVehicle(availableVehicles, selectedType.DisplayName);
        if (vehicleResult.IsCancelled || vehicleResult.Value == null)
        {
            throw new OperationCanceledException();
        }

        var selectedVehicle = vehicleResult.Value;

        // Generate booking number
        var bookingNumber = $"BK-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
        AnsiConsole.WriteLine();

        // Step 3: Customer ID
        var customerResult = ConsolePrompts.PromptText("Customer ID");
        if (customerResult.IsCancelled)
        {
            throw new OperationCanceledException();
        }

        if (string.IsNullOrWhiteSpace(customerResult.Value))
        {
            ConsoleRenderer.DisplayError("Customer ID is required.");
            return;
        }

        // Step 4: Checkout timestamp
        var timestampResult = ConsolePrompts.PromptTimestamp(
            "Checkout Timestamp (ISO 8601, e.g., 2024-03-20T10:00:00+01:00)",
            DateTimeOffset.Now);

        if (timestampResult.IsCancelled)
        {
            throw new OperationCanceledException();
        }

        if (timestampResult.Value == default)
        {
            return; // Invalid input was already displayed
        }

        // Step 5: Odometer reading
        var odometerResult = ConsolePrompts.PromptDecimal(
            "Odometer Reading (km)",
            selectedVehicle.CurrentOdometer);

        if (odometerResult.IsCancelled)
        {
            throw new OperationCanceledException();
        }

        if (odometerResult.Value == default && selectedVehicle.CurrentOdometer == 0)
        {
            // Allow 0 as valid input
        }
        else if (odometerResult.Value < 0)
        {
            return; // Invalid input was already displayed
        }

        var request = new RegisterCheckoutRequest
        {
            BookingNumber = bookingNumber,
            CustomerId = customerResult.Value,
            RegistrationNumber = selectedVehicle.RegistrationNumber,
            VehicleTypeId = selectedType.VehicleTypeId,
            CheckoutTimestamp = timestampResult.Value,
            CheckoutOdometer = odometerResult.Value
        };

        var result = await ConsoleRenderer.WithSpinnerAsync(
            "Processing checkout...",
            async () => await _checkoutService.RegisterCheckoutAsync(request));

        AnsiConsole.WriteLine();

        if (result.IsSuccess)
        {
            var response = result.Value!;
            ConsoleRenderer.DisplaySuccessPanel(
                "✅ Checkout Successful",
                ("Booking Number", response.BookingNumber),
                ("Customer ID", response.CustomerId),
                ("Registration", response.RegistrationNumber),
                ("Vehicle Type", response.VehicleTypeId),
                ("Checkout Time", response.CheckoutTimestamp.ToString("yyyy-MM-dd HH:mm:ss zzz")));
        }
        else
        {
            ConsoleRenderer.DisplayFailurePanel("❌ Checkout Failed", result.Error ?? "Unknown error");
        }
    }
}
