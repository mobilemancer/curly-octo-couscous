using Microsoft.Extensions.Logging;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Helpers;
using VehicleRental.Core.Ports;

namespace VehicleRental.Core.Application;

/// <summary>
/// Service for registering vehicle checkouts.
/// </summary>
public class CheckoutService
{
    private readonly IRentalRepository _rentalRepository;
    private readonly IVehicleCatalog _vehicleCatalog;
    private readonly IVehicleTypeStore _vehicleTypeStore;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        IRentalRepository rentalRepository,
        IVehicleCatalog vehicleCatalog,
        IVehicleTypeStore vehicleTypeStore,
        ILogger<CheckoutService> logger)
    {
        _rentalRepository = rentalRepository ?? throw new ArgumentNullException(nameof(rentalRepository));
        _vehicleCatalog = vehicleCatalog ?? throw new ArgumentNullException(nameof(vehicleCatalog));
        _vehicleTypeStore = vehicleTypeStore ?? throw new ArgumentNullException(nameof(vehicleTypeStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<RegisterCheckoutResponse>> RegisterCheckoutAsync(RegisterCheckoutRequest request)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.BookingNumber))
        {
            return Result<RegisterCheckoutResponse>.Failure("Booking number is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RegistrationNumber))
        {
            return Result<RegisterCheckoutResponse>.Failure("Registration number is required.");
        }

        if (request.CheckoutOdometer < 0)
        {
            return Result<RegisterCheckoutResponse>.Failure("Checkout odometer cannot be negative.");
        }

        // Validate booking number uniqueness
        var bookingExists = await _rentalRepository.ExistsAsync(request.BookingNumber);
        if (bookingExists)
        {
            _logger.LogWarning("Checkout failed: booking number {BookingNumber} already exists", request.BookingNumber);
            return Result<RegisterCheckoutResponse>.Failure($"Booking number '{request.BookingNumber}' already exists.");
        }

        // Validate vehicle exists
        var vehicle = await _vehicleCatalog.GetByRegistrationNumberAsync(request.RegistrationNumber);
        if (vehicle is null)
        {
            _logger.LogWarning("Checkout failed: vehicle {RegistrationNumber} not found", request.RegistrationNumber);
            return Result<RegisterCheckoutResponse>.Failure($"Vehicle '{request.RegistrationNumber}' not found.");
        }

        // Validate vehicle not already in active rental
        var hasActiveRental = await _rentalRepository.HasActiveRentalAsync(request.RegistrationNumber);
        if (hasActiveRental)
        {
            _logger.LogWarning("Checkout failed: vehicle {RegistrationNumber} already has active rental",
                request.RegistrationNumber);
            return Result<RegisterCheckoutResponse>.Failure($"Vehicle '{request.RegistrationNumber}' is already rented out.");
        }

        // Resolve vehicle type ID (prefer request value, otherwise use catalog value)
        string vehicleTypeId;
        if (!string.IsNullOrWhiteSpace(request.VehicleTypeId))
        {
            try
            {
                vehicleTypeId = VehicleTypeIdNormalizer.Normalize(request.VehicleTypeId);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid vehicle type ID: {VehicleTypeId}", request.VehicleTypeId);
                return Result<RegisterCheckoutResponse>.Failure($"Invalid vehicle type ID: {request.VehicleTypeId}");
            }
        }
        else
        {
            vehicleTypeId = vehicle.VehicleTypeId;
        }

        // Validate vehicle type exists
        var vehicleType = await _vehicleTypeStore.GetByIdAsync(vehicleTypeId);
        if (vehicleType is null)
        {
            _logger.LogWarning("Checkout failed: vehicle type {VehicleTypeId} not found", vehicleTypeId);
            return Result<RegisterCheckoutResponse>.Failure($"Vehicle type '{vehicleTypeId}' not found.");
        }

        // Create and persist rental
        var rental = new Rental
        {
            BookingNumber = request.BookingNumber.Trim(),
            RegistrationNumber = request.RegistrationNumber.Trim(),
            VehicleTypeId = vehicleTypeId,
            CheckoutTimestamp = request.CheckoutTimestamp,
            CheckoutOdometer = request.CheckoutOdometer
        };

        await _rentalRepository.AddAsync(rental);

        _logger.LogInformation("Checkout registered: booking {BookingNumber}, vehicle {RegistrationNumber}, type {VehicleTypeId}",
            rental.BookingNumber, rental.RegistrationNumber, rental.VehicleTypeId);

        return Result<RegisterCheckoutResponse>.Success(new RegisterCheckoutResponse
        {
            BookingNumber = rental.BookingNumber,
            RegistrationNumber = rental.RegistrationNumber,
            VehicleTypeId = rental.VehicleTypeId,
            CheckoutTimestamp = rental.CheckoutTimestamp
        });
    }
}
