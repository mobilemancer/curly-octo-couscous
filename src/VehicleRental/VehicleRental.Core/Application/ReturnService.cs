using Microsoft.Extensions.Logging;
using VehicleRental.Core.Helpers;
using VehicleRental.Core.Ports;
using VehicleRental.Core.Pricing;

namespace VehicleRental.Core.Application;

/// <summary>
/// Service for registering vehicle returns and calculating rental prices.
/// </summary>
public class ReturnService(
    IRentalRepository rentalRepository,
    PricingCalculator pricingCalculator,
    ILogger<ReturnService> logger)
{
    private readonly IRentalRepository _rentalRepository = rentalRepository ?? throw new ArgumentNullException(nameof(rentalRepository));
    private readonly PricingCalculator _pricingCalculator = pricingCalculator ?? throw new ArgumentNullException(nameof(pricingCalculator));
    private readonly ILogger<ReturnService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result<RegisterReturnResponse>> RegisterReturnAsync(RegisterReturnRequest request)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.BookingNumber))
        {
            return Result<RegisterReturnResponse>.Failure("Booking number is required.");
        }

        if (request.ReturnOdometer < 0)
        {
            return Result<RegisterReturnResponse>.Failure("Return odometer cannot be negative.");
        }

        // Validate pricing parameters
        if (request.PricingParameters.BaseDayRate < 0)
        {
            return Result<RegisterReturnResponse>.Failure("Base day rate cannot be negative.");
        }

        if (request.PricingParameters.BaseKmPrice < 0)
        {
            return Result<RegisterReturnResponse>.Failure("Base kilometer price cannot be negative.");
        }

        // Load rental
        var rental = await _rentalRepository.GetByBookingNumberAsync(request.BookingNumber);
        if (rental is null)
        {
            _logger.LogWarning("Return failed: booking {BookingNumber} not found", request.BookingNumber);
            return Result<RegisterReturnResponse>.Failure($"Booking '{request.BookingNumber}' not found.");
        }

        // Validate rental is active
        if (!rental.IsActive)
        {
            _logger.LogWarning("Return failed: booking {BookingNumber} already completed", request.BookingNumber);
            return Result<RegisterReturnResponse>.Failure($"Booking '{request.BookingNumber}' is already completed.");
        }

        // Validate return timestamp is not before checkout
        if (request.ReturnTimestamp.UtcDateTime < rental.CheckoutTimestamp.UtcDateTime)
        {
            _logger.LogWarning("Return failed: return timestamp before checkout for booking {BookingNumber}",
                request.BookingNumber);
            return Result<RegisterReturnResponse>.Failure("Return timestamp cannot be before checkout timestamp.");
        }

        // Validate minimum rental duration (at least 1 minute)
        var duration = request.ReturnTimestamp.UtcDateTime - rental.CheckoutTimestamp.UtcDateTime;
        if (duration.TotalMinutes < 1)
        {
            _logger.LogWarning("Return failed: rental duration less than 1 minute for booking {BookingNumber}",
                request.BookingNumber);
            return Result<RegisterReturnResponse>.Failure("Rental must be at least 1 minute.");
        }

        // Validate maximum rental duration (60 days)
        if (duration.TotalDays > 60)
        {
            _logger.LogWarning("Return failed: rental duration exceeds 60 days for booking {BookingNumber}",
                request.BookingNumber);
            return Result<RegisterReturnResponse>.Failure("Rental duration cannot exceed 60 days.");
        }

        // Validate return odometer is not less than checkout
        if (request.ReturnOdometer < rental.CheckoutOdometer)
        {
            _logger.LogWarning("Return failed: return odometer less than checkout for booking {BookingNumber}",
                request.BookingNumber);
            return Result<RegisterReturnResponse>.Failure("Return odometer cannot be less than checkout odometer.");
        }

        // Calculate days and kilometers
        var days = RentalCalculations.CalculateDays(rental.CheckoutTimestamp, request.ReturnTimestamp);
        var km = RentalCalculations.CalculateDistance(rental.CheckoutOdometer, request.ReturnOdometer);

        _logger.LogInformation("Calculating rental price for booking {BookingNumber}: {Days} days, {Km} km",
            request.BookingNumber, days, km);

        // Calculate raw price
        decimal rawPrice;
        try
        {
            rawPrice = await _pricingCalculator.CalculateAsync(
                rental.VehicleTypeId,
                request.PricingParameters,
                days,
                km);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Return failed: pricing calculation error for booking {BookingNumber}",
                request.BookingNumber);
            return Result<RegisterReturnResponse>.Failure($"Failed to calculate rental price: {ex.Message}");
        }

        // Apply final rounding
        var finalPrice = RentalCalculations.RoundFinalPrice(rawPrice);

        _logger.LogInformation("Calculated price for booking {BookingNumber}: raw={RawPrice}, final={FinalPrice}",
            request.BookingNumber, rawPrice, finalPrice);

        // Update rental
        var updatedRental = rental with
        {
            ReturnTimestamp = request.ReturnTimestamp,
            ReturnOdometer = request.ReturnOdometer,
            RentalPrice = finalPrice
        };

        await _rentalRepository.UpdateAsync(updatedRental);

        _logger.LogInformation("Return registered: booking {BookingNumber}, price {Price}",
            request.BookingNumber, finalPrice);

        return Result<RegisterReturnResponse>.Success(new RegisterReturnResponse
        {
            BookingNumber = request.BookingNumber,
            Days = days,
            KilometersDriven = km,
            RentalPrice = finalPrice
        });
    }
}
