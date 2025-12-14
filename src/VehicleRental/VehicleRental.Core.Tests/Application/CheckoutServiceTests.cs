using Microsoft.Extensions.Logging.Abstractions;
using VehicleRental.Core.Application;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Helpers;
using VehicleRental.Core.Infrastructure.Testing;

namespace VehicleRental.Core.Tests.Application;

public class CheckoutServiceTests
{
    private readonly CheckoutService _service;
    private readonly FakeRentalRepository _rentalRepository;
    private readonly FakeVehicleCatalog _vehicleCatalog;
    private readonly FakeVehicleTypeStore _vehicleTypeStore;

    public CheckoutServiceTests()
    {
        _rentalRepository = new FakeRentalRepository();
        _vehicleCatalog = new FakeVehicleCatalog();
        _vehicleTypeStore = new FakeVehicleTypeStore();
        _service = new CheckoutService(
            _rentalRepository,
            _vehicleCatalog,
            _vehicleTypeStore,
            NullLogger<CheckoutService>.Instance);

        // Setup test data
        _vehicleCatalog.AddVehicle(new Vehicle
        {
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            Location = "test-location"
        });

        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "small-car",
            DisplayName = "Small Car",
            PricingFormula = "baseDayRate * days"
        });
    }

    [Fact]
    public async Task RegisterCheckoutAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 10000
        };

        // Act
        var result = await _service.RegisterCheckoutAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("BK001", result.Value.BookingNumber);
        Assert.Equal("ABC123", result.Value.RegistrationNumber);
        Assert.Equal("small-car", result.Value.VehicleTypeId);
    }

    [Fact]
    public async Task RegisterCheckoutAsync_EmptyBookingNumber_ReturnsFailure()
    {
        // Arrange
        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 10000
        };

        // Act
        var result = await _service.RegisterCheckoutAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Booking number is required", result.Error);
    }

    [Fact]
    public async Task RegisterCheckoutAsync_EmptyRegistrationNumber_ReturnsFailure()
    {
        // Arrange
        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 10000
        };

        // Act
        var result = await _service.RegisterCheckoutAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Registration number is required", result.Error);
    }

    [Fact]
    public async Task RegisterCheckoutAsync_NegativeOdometer_ReturnsFailure()
    {
        // Arrange
        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = -100
        };

        // Act
        var result = await _service.RegisterCheckoutAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("odometer cannot be negative", result.Error);
    }

    [Fact]
    public async Task RegisterCheckoutAsync_DuplicateBookingNumber_ReturnsFailure()
    {
        // Arrange
        var existingRental = new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = DateTimeOffset.UtcNow.AddDays(-1),
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(existingRental);

        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 15000
        };

        // Act
        var result = await _service.RegisterCheckoutAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public async Task RegisterCheckoutAsync_VehicleNotFound_ReturnsFailure()
    {
        // Arrange
        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "UNKNOWN",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 10000
        };

        // Act
        var result = await _service.RegisterCheckoutAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task RegisterCheckoutAsync_VehicleAlreadyRented_ReturnsFailure()
    {
        // Arrange
        var existingRental = new Rental
        {
            BookingNumber = "BK000",
            CustomerId = "CUST002",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = DateTimeOffset.UtcNow.AddDays(-1),
            CheckoutOdometer = 10000
        };
        await _rentalRepository.AddAsync(existingRental);

        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 15000
        };

        // Act
        var result = await _service.RegisterCheckoutAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("already rented", result.Error);
    }

    [Fact]
    public async Task RegisterCheckoutAsync_ExplicitVehicleTypeId_UsesProvidedType()
    {
        // Arrange
        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "truck",
            DisplayName = "Truck",
            PricingFormula = "baseDayRate * days * 1.5"
        });

        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "truck",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 10000
        };

        // Act
        var result = await _service.RegisterCheckoutAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("truck", result.Value!.VehicleTypeId);
    }

    [Fact]
    public async Task RegisterCheckoutAsync_InvalidVehicleTypeId_ReturnsFailure()
    {
        // Arrange
        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "unknown-type",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 10000
        };

        // Act
        var result = await _service.RegisterCheckoutAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }
}
