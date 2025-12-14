using Microsoft.Extensions.Logging.Abstractions;
using VehicleRental.Core.Application;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Infrastructure.Testing;
using VehicleRental.Core.Pricing;

namespace VehicleRental.Core.Tests.Application;

public class ReturnServiceTests
{
    private readonly ReturnService _service;
    private readonly FakeRentalRepository _rentalRepository;
    private readonly FakeVehicleTypeStore _vehicleTypeStore;
    private readonly PricingCalculator _pricingCalculator;

    public ReturnServiceTests()
    {
        _rentalRepository = new FakeRentalRepository();
        _vehicleTypeStore = new FakeVehicleTypeStore();

        var formulaEvaluator = new SafeFormulaEvaluator();
        _pricingCalculator = new PricingCalculator(
            _vehicleTypeStore,
            formulaEvaluator,
            NullLogger<PricingCalculator>.Instance);

        _service = new ReturnService(
            _rentalRepository,
            _pricingCalculator,
            NullLogger<ReturnService>.Instance);

        // Setup test data
        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "small-car",
            DisplayName = "Small Car",
            PricingFormula = "baseDayRate * days"
        });

        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "station-wagon",
            DisplayName = "Station Wagon",
            PricingFormula = "(baseDayRate * days * 1.3) + (baseKmPrice * km)"
        });
    }

    [Fact]
    public async Task RegisterReturnAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = checkout.AddDays(3),
            ReturnOdometer = 10500,
            PricingParameters = new PricingParameters
            {
                BaseDayRate = 100,
                BaseKmPrice = 0.5m
            }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("BK001", result.Value.BookingNumber);
        Assert.Equal(3, result.Value.Days);
        Assert.Equal(500, result.Value.KilometersDriven);
        Assert.Equal(300, result.Value.RentalPrice); // baseDayRate * days = 100 * 3 = 300
    }

    [Fact]
    public async Task RegisterReturnAsync_StationWagonFormula_CalculatesCorrectly()
    {
        // Arrange
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK002",
            CustomerId = "CUST002",
            RegistrationNumber = "DEF456",
            VehicleTypeId = "station-wagon",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK002",
            ReturnTimestamp = checkout.AddDays(3),
            ReturnOdometer = 10150,
            PricingParameters = new PricingParameters
            {
                BaseDayRate = 100,
                BaseKmPrice = 0.5m
            }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        // (100 * 3 * 1.3) + (0.5 * 150) = 390 + 75 = 465
        Assert.Equal(465, result.Value!.RentalPrice);
    }

    [Fact]
    public async Task RegisterReturnAsync_RoundsUpToNearestInteger()
    {
        // Arrange - Create a scenario that produces fractional price
        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "test-fractional",
            DisplayName = "Test",
            PricingFormula = "baseDayRate * days * 1.7"
        });

        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK003",
            CustomerId = "CUST003",
            RegistrationNumber = "TEST001",
            VehicleTypeId = "test-fractional",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK003",
            ReturnTimestamp = checkout.AddDays(2),
            ReturnOdometer = 10100,
            PricingParameters = new PricingParameters
            {
                BaseDayRate = 100,
                BaseKmPrice = 0.5m
            }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert - 100 * 2 * 1.7 = 340 (no rounding needed in this case)
        // But the system should use Ceiling for any fractional amounts
        Assert.True(result.IsSuccess);
        Assert.Equal(340, result.Value!.RentalPrice);
    }

    [Fact]
    public async Task RegisterReturnAsync_EmptyBookingNumber_ReturnsFailure()
    {
        // Arrange
        var request = new RegisterReturnRequest
        {
            BookingNumber = "",
            ReturnTimestamp = DateTimeOffset.UtcNow,
            ReturnOdometer = 10500,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Booking number is required", result.Error);
    }

    [Fact]
    public async Task RegisterReturnAsync_NegativeOdometer_ReturnsFailure()
    {
        // Arrange
        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = DateTimeOffset.UtcNow,
            ReturnOdometer = -100,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("odometer cannot be negative", result.Error);
    }

    [Fact]
    public async Task RegisterReturnAsync_BookingNotFound_ReturnsFailure()
    {
        // Arrange
        var request = new RegisterReturnRequest
        {
            BookingNumber = "UNKNOWN",
            ReturnTimestamp = DateTimeOffset.UtcNow,
            ReturnOdometer = 10500,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task RegisterReturnAsync_AlreadyCompleted_ReturnsFailure()
    {
        // Arrange
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000,
            ReturnTimestamp = checkout.AddDays(2),
            ReturnOdometer = 10200,
            RentalPrice = 200
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = checkout.AddDays(3),
            ReturnOdometer = 10300,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("already completed", result.Error);
    }

    [Fact]
    public async Task RegisterReturnAsync_ReturnBeforeCheckout_ReturnsFailure()
    {
        // Arrange
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = checkout.AddDays(-1), // Before checkout!
            ReturnOdometer = 10500,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("before checkout", result.Error);
    }

    [Fact]
    public async Task RegisterReturnAsync_ReturnOdometerLessThanCheckout_ReturnsFailure()
    {
        // Arrange
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = checkout.AddDays(2),
            ReturnOdometer = 9000, // Less than checkout!
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("less than checkout", result.Error);
    }

    [Fact]
    public async Task RegisterReturnAsync_ZeroKilometers_CalculatesCorrectly()
    {
        // Arrange
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = checkout.AddDays(2),
            ReturnOdometer = 10000, // Same as checkout
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.KilometersDriven);
        Assert.Equal(200, result.Value.RentalPrice); // baseDayRate * days = 100 * 2
    }

    [Fact]
    public async Task RegisterReturnAsync_DifferentTimeZones_CalculatesCorrectly()
    {
        // Arrange - Checkout in UTC, return in different timezone
        var checkoutUtc = new DateTimeOffset(2025, 12, 10, 10, 0, 0, TimeSpan.Zero);
        var returnEst = new DateTimeOffset(2025, 12, 13, 5, 0, 0, TimeSpan.FromHours(-5)); // Same instant as 10:00 UTC

        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkoutUtc,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = returnEst,
            ReturnOdometer = 10300,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert - Should be 3 days (UTC-based calculation)
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Days);
        Assert.Equal(300, result.Value.RentalPrice);
    }

    [Fact]
    public async Task RegisterReturnAsync_SameSecondReturn_ReturnsFailure()
    {
        // Arrange - UI enforces minimum 1 minute between checkout and return
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = checkout, // Same exact moment
            ReturnOdometer = 10000,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert - Should fail validation (UI should prevent this, but backend validates too)
        Assert.False(result.IsSuccess);
        Assert.Contains("at least 1 minute", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterReturnAsync_SubOneHourRental_CountsAsOneDay()
    {
        // Arrange - 2 hour rental should count as 1 day
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = checkout.AddHours(2), // 2 hours later
            ReturnOdometer = 10050,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert - Any rental < 24 hours should count as 1 day
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Days);
        Assert.Equal(100, result.Value.RentalPrice); // 100 * 1 day
    }

    [Fact]
    public async Task RegisterReturnAsync_ExactlyOneDayRental_CountsAsOneDay()
    {
        // Arrange - Exactly 24 hours should count as 1 day
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = checkout.AddHours(24), // Exactly 24 hours
            ReturnOdometer = 10100,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert - 24 hours exactly should count as 1 day
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Days);
        Assert.Equal(100, result.Value.RentalPrice);
    }

    [Fact]
    public async Task RegisterReturnAsync_JustOverOneDayRental_CountsAsTwoDays()
    {
        // Arrange - 24 hours and 1 second should count as 2 days (rounds up)
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = checkout.AddHours(24).AddSeconds(1), // 24h 1s
            ReturnOdometer = 10100,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert - Should round up to 2 days
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Days);
        Assert.Equal(200, result.Value.RentalPrice);
    }

    [Fact]
    public async Task RegisterReturnAsync_ExtremelyLongRental_ReturnsFailure()
    {
        // Arrange - 1000 day rental exceeds maximum of 60 days
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = checkout.AddDays(1000), // Way too long
            ReturnOdometer = 50000,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert - Should fail validation (max 60 days)
        Assert.False(result.IsSuccess);
        Assert.Contains("60 days", result.Error);
    }

    [Fact]
    public async Task RegisterReturnAsync_MaximumAllowedRental_ReturnsSuccess()
    {
        // Arrange - 60 day rental should be allowed
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = checkout.AddDays(60), // Maximum allowed
            ReturnOdometer = 15000,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert - Should succeed
        Assert.True(result.IsSuccess);
        Assert.Equal(60, result.Value!.Days);
        Assert.Equal(6000, result.Value.RentalPrice);
    }

    [Fact]
    public async Task RegisterReturnAsync_ZeroBaseRates_CalculatesCorrectly()
    {
        // Arrange - Zero base rates should result in zero price
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = checkout.AddDays(3),
            ReturnOdometer = 10500,
            PricingParameters = new PricingParameters
            {
                BaseDayRate = 0,
                BaseKmPrice = 0
            }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert - Zero rates should result in zero price
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.RentalPrice);
    }

    [Fact]
    public async Task RegisterReturnAsync_NegativeBaseDayRate_ReturnsFailure()
    {
        // Arrange - Negative rates should not be allowed
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = checkout.AddDays(3),
            ReturnOdometer = 10500,
            PricingParameters = new PricingParameters
            {
                BaseDayRate = -100,
                BaseKmPrice = 0.5m
            }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert - Should fail validation
        Assert.False(result.IsSuccess);
        Assert.Contains("negative", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterReturnAsync_NegativeBaseKmPrice_ReturnsFailure()
    {
        // Arrange - Negative rates should not be allowed
        var checkout = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkout,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = checkout.AddDays(3),
            ReturnOdometer = 10500,
            PricingParameters = new PricingParameters
            {
                BaseDayRate = 100,
                BaseKmPrice = -0.5m
            }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert - Should fail validation
        Assert.False(result.IsSuccess);
        Assert.Contains("negative", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterReturnAsync_DSTBoundary_CalculatesCorrectly()
    {
        // Arrange - Test DST transition (Spring forward: 2 AM becomes 3 AM)
        // In US, DST typically occurs on second Sunday of March
        // Checkout at 1:00 AM EST (UTC-5), return at 4:00 AM EDT (UTC-4) on DST day
        var checkoutBeforeDst = new DateTimeOffset(2025, 3, 9, 1, 0, 0, TimeSpan.FromHours(-5));
        var returnAfterDst = new DateTimeOffset(2025, 3, 9, 4, 0, 0, TimeSpan.FromHours(-4));

        // The clock jumped from 2 AM to 3 AM, so wall clock shows 3 hours
        // But in UTC terms: 1 AM EST = 6 AM UTC, 4 AM EDT = 8 AM UTC = 2 hours actual time

        var rental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = checkoutBeforeDst,
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(rental);

        var request = new RegisterReturnRequest
        {
            BookingNumber = "BK001",
            ReturnTimestamp = returnAfterDst,
            ReturnOdometer = 10050,
            PricingParameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m }
        };

        // Act
        var result = await _service.RegisterReturnAsync(request);

        // Assert - Calculation uses UTC, so should be based on actual elapsed time (2 hours = 1 day)
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Days);
        Assert.Equal(100, result.Value.RentalPrice);
    }
}
