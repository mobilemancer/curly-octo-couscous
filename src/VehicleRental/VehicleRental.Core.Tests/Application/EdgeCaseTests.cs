using Microsoft.Extensions.Logging.Abstractions;
using VehicleRental.Core.Application;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Pricing;
using VehicleRental.Core.Tests.Infrastructure;

namespace VehicleRental.Core.Tests.Application;

/// <summary>
/// Edge case and boundary condition tests for the rental system.
/// Covers scenarios that might break or behave unexpectedly at limits.
/// </summary>
public class EdgeCaseTests
{
    private readonly CheckoutService _checkoutService;
    private readonly ReturnService _returnService;
    private readonly FakeRentalRepository _rentalRepository;
    private readonly FakeVehicleCatalog _vehicleCatalog;
    private readonly FakeVehicleTypeStore _vehicleTypeStore;
    private readonly PricingCalculator _pricingCalculator;

    public EdgeCaseTests()
    {
        _rentalRepository = new FakeRentalRepository();
        _vehicleCatalog = new FakeVehicleCatalog();
        _vehicleTypeStore = new FakeVehicleTypeStore();

        _checkoutService = new CheckoutService(
            _rentalRepository,
            _vehicleCatalog,
            _vehicleTypeStore,
            NullLogger<CheckoutService>.Instance);

        var formulaEvaluator = new SafeFormulaEvaluator();
        _pricingCalculator = new PricingCalculator(
            _vehicleTypeStore,
            formulaEvaluator,
            NullLogger<PricingCalculator>.Instance);

        _returnService = new ReturnService(
            _rentalRepository,
            _pricingCalculator,
            NullLogger<ReturnService>.Instance);

        // Setup basic vehicle type
        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "standard",
            DisplayName = "Standard",
            PricingFormula = "(baseDayRate * days) + (baseKmPrice * km)"
        });
    }

    #region Boundary: Minimum Rental Duration

    [Fact]
    public async Task Return_ExactlyOneMinute_Succeeds()
    {
        // Arrange - minimum valid rental (1 minute)
        await SetupVehicleAndCheckout("MIN001", "BK-MIN-001");
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = await _rentalRepository.GetByBookingNumberAsync("BK-MIN-001");

        // Update the rental to have a known checkout time
        var updatedRental = rental! with { CheckoutTimestamp = checkout };
        await _rentalRepository.UpdateAsync(updatedRental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK-MIN-001",
            ReturnTimestamp = checkout.AddMinutes(1),
            ReturnOdometer = 0,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 1 }
        };

        // Act
        var result = await _returnService.RegisterReturnAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Days); // Should be at least 1 day
    }

    // Note: Return_LessThanOneMinute covered by ReturnServiceTests.RegisterReturnAsync_SameSecondReturn_ReturnsFailure

    #endregion

    // Note: Maximum Duration (60 days) tests covered by ReturnServiceTests

    #region Boundary: Odometer Edge Cases

    // Note: Return_ZeroKilometersDriven covered by ReturnServiceTests.RegisterReturnAsync_ZeroKilometers_CalculatesCorrectly
    // Note: Return_OdometerLessThanCheckout covered by ReturnServiceTests.RegisterReturnAsync_ReturnOdometerLessThanCheckout_ReturnsFailure

    [Fact]
    public async Task Checkout_ZeroOdometer_Succeeds()
    {
        // Arrange - brand new vehicle with zero km
        await _vehicleCatalog.AddVehicleAsync(new Vehicle
        {
            RegistrationNumber = "NEW001",
            VehicleTypeId = "standard",
            CurrentOdometer = 0,
            Location = "test-location"
        });

        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK-NEW-001",
            CustomerId = "CUST-001",
            RegistrationNumber = "NEW001",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 0
        };

        // Act
        var result = await _checkoutService.RegisterCheckoutAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Return_VeryHighOdometer_Succeeds()
    {
        // Arrange - vehicle with very high odometer (old truck)
        await SetupVehicleAndCheckout("HIGH001", "BK-HIGH-001", checkoutOdometer: 999999);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK-HIGH-001",
            ReturnTimestamp = DateTimeOffset.UtcNow.AddDays(1),
            ReturnOdometer = 1000500, // Crossed 1 million
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _returnService.RegisterReturnAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(501, result.Value!.KilometersDriven);
    }

    #endregion

    #region Boundary: Pricing Edge Cases

    // Note: Negative day/km rate tests covered by ReturnServiceTests.RegisterReturnAsync_NegativeBase*Rate_ReturnsFailure

    [Fact]
    public async Task Return_ZeroDayRate_Succeeds()
    {
        // Arrange - free day rate (promotional)
        await SetupVehicleAndCheckout("FREE001", "BK-FREE-001");

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK-FREE-001",
            ReturnTimestamp = DateTimeOffset.UtcNow.AddDays(3),
            ReturnOdometer = 100,
            PricingParameters = new PricingParameters { BaseDayRate = 0, BaseKmPrice = 1 }
        };

        // Act
        var result = await _returnService.RegisterReturnAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value!.RentalPrice); // Only km charge
    }

    [Fact]
    public async Task Return_ZeroKmPrice_Succeeds()
    {
        // Arrange - unlimited km deal
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        await SetupVehicleAndCheckoutWithTime("UNLIM001", "BK-UNLIM-001", checkout);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK-UNLIM-001",
            ReturnTimestamp = checkout.AddDays(2), // Exactly 2 days
            ReturnOdometer = 1000, // 1000 km driven
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0 }
        };

        // Act
        var result = await _returnService.RegisterReturnAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.Value!.RentalPrice); // Only day charge (100 * 2)
    }

    [Fact]
    public async Task Return_VerySmallFractionalPrice_RoundsUpToOne()
    {
        // Arrange - price formula that produces 0.01
        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "micro-price",
            DisplayName = "Micro",
            PricingFormula = "baseDayRate * 0.0001"
        });

        await _vehicleCatalog.AddVehicleAsync(new Vehicle
        {
            RegistrationNumber = "MICRO001",
            VehicleTypeId = "micro-price",
            CurrentOdometer = 0,
            Location = "test-location"
        });

        var checkout = DateTimeOffset.UtcNow;
        await _rentalRepository.AddAsync(new Rental
        {
            BookingNumber = "BK-MICRO-001",
            CustomerId = "CUST-001",
            RegistrationNumber = "MICRO001",
            VehicleTypeId = "micro-price",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 0
        });

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK-MICRO-001",
            ReturnTimestamp = checkout.AddDays(1),
            ReturnOdometer = 0,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0 }
        };

        // Act
        var result = await _returnService.RegisterReturnAsync(request);

        // Assert - 100 * 0.0001 = 0.01, rounded up = 1
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.RentalPrice);
    }

    [Fact]
    public async Task Return_VeryLargePrice_CalculatesCorrectly()
    {
        // Arrange - expensive vehicle, long rental, many km
        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "luxury-exotic",
            DisplayName = "Luxury Exotic",
            PricingFormula = "(baseDayRate * days * 10) + (baseKmPrice * km * 5)"
        });

        await _vehicleCatalog.AddVehicleAsync(new Vehicle
        {
            RegistrationNumber = "EXOTIC001",
            VehicleTypeId = "luxury-exotic",
            CurrentOdometer = 1000,
            Location = "test-location"
        });

        var checkout = DateTimeOffset.UtcNow;
        await _rentalRepository.AddAsync(new Rental
        {
            BookingNumber = "BK-EXOTIC-001",
            CustomerId = "VIP-001",
            RegistrationNumber = "EXOTIC001",
            VehicleTypeId = "luxury-exotic",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 1000
        });

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK-EXOTIC-001",
            ReturnTimestamp = checkout.AddDays(30),
            ReturnOdometer = 6000, // 5000 km
            PricingParameters = new PricingParameters { BaseDayRate = 1000, BaseKmPrice = 10 }
        };

        // Act
        var result = await _returnService.RegisterReturnAsync(request);

        // Assert - (1000 * 30 * 10) + (10 * 5000 * 5) = 300000 + 250000 = 550000
        Assert.True(result.IsSuccess);
        Assert.Equal(550000, result.Value!.RentalPrice);
    }

    #endregion

    // Note: Timestamp edge cases (before checkout, different timezone) covered by ReturnServiceTests
    // Note: Double return (AlreadyCompleted) covered by ReturnServiceTests.RegisterReturnAsync_AlreadyCompleted_ReturnsFailure
    // Note: Return_NonExistentBooking covered by ReturnServiceTests.RegisterReturnAsync_BookingNotFound_ReturnsFailure

    #region Input Validation Edge Cases (Checkout-specific)

    [Fact]
    public async Task Checkout_WhitespaceOnlyBookingNumber_Fails()
    {
        // Arrange
        await _vehicleCatalog.AddVehicleAsync(new Vehicle
        {
            RegistrationNumber = "WS001",
            VehicleTypeId = "standard",
            CurrentOdometer = 0,
            Location = "test-location"
        });

        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "   ", // Only spaces
            CustomerId = "CUST-001",
            RegistrationNumber = "WS001",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 0
        };

        // Act
        var result = await _checkoutService.RegisterCheckoutAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("required", result.Error);
    }

    [Fact]
    public async Task Checkout_WhitespaceOnlyCustomerId_Fails()
    {
        // Arrange
        await _vehicleCatalog.AddVehicleAsync(new Vehicle
        {
            RegistrationNumber = "WS002",
            VehicleTypeId = "standard",
            CurrentOdometer = 0,
            Location = "test-location"
        });

        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK-WS-002",
            CustomerId = "   ", // Only spaces
            RegistrationNumber = "WS002",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 0
        };

        // Act
        var result = await _checkoutService.RegisterCheckoutAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Customer", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Checkout_CaseInsensitiveRegistrationNumber_Succeeds()
    {
        // Arrange - vehicle stored as uppercase
        await _vehicleCatalog.AddVehicleAsync(new Vehicle
        {
            RegistrationNumber = "UPPER123",
            VehicleTypeId = "standard",
            CurrentOdometer = 0,
            Location = "test-location"
        });

        // Request with lowercase
        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK-CASE-001",
            CustomerId = "CUST-001",
            RegistrationNumber = "upper123", // lowercase
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 0
        };

        // Act
        var result = await _checkoutService.RegisterCheckoutAsync(request);

        // Assert - should find the vehicle regardless of case
        Assert.True(result.IsSuccess);
    }

    #endregion

    // Note: Day calculation edge cases (23h59m → 1 day, 24h1m → 2 days) covered by ReturnServiceTests

    #region Concurrent/Sequential Rentals

    [Fact]
    public async Task SameVehicle_SequentialRentals_TrackOdometerCorrectly()
    {
        // Arrange
        await _vehicleCatalog.AddVehicleAsync(new Vehicle
        {
            RegistrationNumber = "SEQ001",
            VehicleTypeId = "standard",
            CurrentOdometer = 10000,
            Location = "test-location"
        });

        // First rental
        var checkout1 = await _checkoutService.RegisterCheckoutAsync(new RegisterCheckoutRequest
        {
            BookingNumber = "BK-SEQ-001",
            CustomerId = "CUST-A",
            RegistrationNumber = "SEQ001",
            CheckoutTimestamp = DateTimeOffset.Parse("2025-12-01T10:00:00Z"),
            CheckoutOdometer = 10000
        });
        Assert.True(checkout1.IsSuccess);

        var return1 = await _returnService.RegisterReturnAsync(new RegisterReturnRequest
        {
            BookingNumber = "BK-SEQ-001",
            ReturnTimestamp = DateTimeOffset.Parse("2025-12-03T10:00:00Z"),
            ReturnOdometer = 10500,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        });
        Assert.True(return1.IsSuccess);
        Assert.Equal(500, return1.Value!.KilometersDriven);

        // Second rental - starts where first ended
        var checkout2 = await _checkoutService.RegisterCheckoutAsync(new RegisterCheckoutRequest
        {
            BookingNumber = "BK-SEQ-002",
            CustomerId = "CUST-B",
            RegistrationNumber = "SEQ001",
            CheckoutTimestamp = DateTimeOffset.Parse("2025-12-04T10:00:00Z"),
            CheckoutOdometer = 10500 // Should match return odometer
        });
        Assert.True(checkout2.IsSuccess);

        var return2 = await _returnService.RegisterReturnAsync(new RegisterReturnRequest
        {
            BookingNumber = "BK-SEQ-002",
            ReturnTimestamp = DateTimeOffset.Parse("2025-12-05T10:00:00Z"),
            ReturnOdometer = 10800,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        });

        // Assert
        Assert.True(return2.IsSuccess);
        Assert.Equal(300, return2.Value!.KilometersDriven);
    }

    #endregion

    #region Helper Methods

    private async Task SetupVehicleAndCheckout(string regNumber, string bookingNumber, decimal checkoutOdometer = 0)
    {
        await SetupVehicleAndCheckoutWithTime(regNumber, bookingNumber, DateTimeOffset.UtcNow, checkoutOdometer);
    }

    private async Task SetupVehicleAndCheckoutWithTime(string regNumber, string bookingNumber, DateTimeOffset checkoutTime, decimal checkoutOdometer = 0)
    {
        await _vehicleCatalog.AddVehicleAsync(new Vehicle
        {
            RegistrationNumber = regNumber,
            VehicleTypeId = "standard",
            CurrentOdometer = checkoutOdometer,
            Location = "test-location"
        });

        await _rentalRepository.AddAsync(new Rental
        {
            BookingNumber = bookingNumber,
            CustomerId = "CUST-001",
            RegistrationNumber = regNumber,
            VehicleTypeId = "standard",
            CheckoutTimestamp = checkoutTime,
            CheckoutOdometer = checkoutOdometer
        });
    }

    #endregion
}
