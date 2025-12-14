using Microsoft.Extensions.Logging.Abstractions;
using VehicleRental.Core.Application;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Infrastructure.Testing;

namespace VehicleRental.Core.Tests.Application;

/// <summary>
/// Tests specifically for client-server architecture features.
/// </summary>
public class ClientServerArchitectureTests
{
    private readonly CheckoutService _checkoutService;
    private readonly FakeRentalRepository _rentalRepository;
    private readonly FakeVehicleCatalog _vehicleCatalog;
    private readonly FakeVehicleTypeStore _vehicleTypeStore;

    public ClientServerArchitectureTests()
    {
        _rentalRepository = new FakeRentalRepository();
        _vehicleCatalog = new FakeVehicleCatalog();
        _vehicleTypeStore = new FakeVehicleTypeStore();
        _checkoutService = new CheckoutService(
            _rentalRepository,
            _vehicleCatalog,
            _vehicleTypeStore,
            NullLogger<CheckoutService>.Instance);

        // Setup test data
        _vehicleCatalog.AddVehicle(new Vehicle
        {
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CurrentOdometer = 10000
        });

        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "small-car",
            DisplayName = "Small Car",
            PricingFormula = "baseDayRate * days"
        });
    }

    [Fact]
    public async Task RegisterCheckoutAsync_EmptyCustomerId_ReturnsFailure()
    {
        // Arrange
        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK001",
            CustomerId = "",
            RegistrationNumber = "ABC123",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 10000
        };

        // Act
        var result = await _checkoutService.RegisterCheckoutAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Customer ID is required", result.Error);
    }

    [Fact]
    public async Task RegisterCheckoutAsync_WhitespaceCustomerId_ReturnsFailure()
    {
        // Arrange
        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK001",
            CustomerId = "   ",
            RegistrationNumber = "ABC123",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 10000
        };

        // Act
        var result = await _checkoutService.RegisterCheckoutAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Customer ID is required", result.Error);
    }

    [Fact]
    public async Task RegisterCheckoutAsync_CustomerIdWithWhitespace_TrimsCorrectly()
    {
        // Arrange
        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK001",
            CustomerId = "  CUST001  ",
            RegistrationNumber = "ABC123",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 10000
        };

        // Act
        var result = await _checkoutService.RegisterCheckoutAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("CUST001", result.Value!.CustomerId); // Should be trimmed
    }

    [Fact]
    public async Task RegisterCheckoutAsync_MultipleCustomers_CanRentSameVehicleType()
    {
        // Arrange - Customer 1 rents a vehicle
        var request1 = new RegisterCheckoutRequest
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 10000
        };
        await _checkoutService.RegisterCheckoutAsync(request1);

        // Add another vehicle of the same type
        _vehicleCatalog.AddVehicle(new Vehicle
        {
            RegistrationNumber = "DEF456",
            VehicleTypeId = "small-car",
            CurrentOdometer = 20000
        });

        // Customer 2 rents another vehicle of the same type
        var request2 = new RegisterCheckoutRequest
        {
            BookingNumber = "BK002",
            CustomerId = "CUST002",
            RegistrationNumber = "DEF456",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 20000
        };

        // Act
        var result = await _checkoutService.RegisterCheckoutAsync(request2);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("CUST002", result.Value!.CustomerId);
    }

    [Fact]
    public async Task RegisterCheckoutAsync_CustomerIdIsIncludedInResponse()
    {
        // Arrange
        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK001",
            CustomerId = "CUST123",
            RegistrationNumber = "ABC123",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 10000
        };

        // Act
        var result = await _checkoutService.RegisterCheckoutAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("CUST123", result.Value.CustomerId);
    }

    [Fact]
    public async Task RegisterCheckoutAsync_CustomerIdIsPersistedInRental()
    {
        // Arrange
        var request = new RegisterCheckoutRequest
        {
            BookingNumber = "BK001",
            CustomerId = "CUST999",
            RegistrationNumber = "ABC123",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 10000
        };

        // Act
        await _checkoutService.RegisterCheckoutAsync(request);

        // Assert - Verify the rental was persisted with the correct customer ID
        var rental = await _rentalRepository.GetByBookingNumberAsync("BK001");
        Assert.NotNull(rental);
        Assert.Equal("CUST999", rental.CustomerId);
    }

    [Fact]
    public async Task RentalRepository_GetAllAsync_ReturnsAllRentals()
    {
        // Arrange - Create multiple rentals for different customers
        await _rentalRepository.AddAsync(new Rental
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "ABC123",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 10000
        });

        await _rentalRepository.AddAsync(new Rental
        {
            BookingNumber = "BK002",
            CustomerId = "CUST002",
            RegistrationNumber = "DEF456",
            VehicleTypeId = "small-car",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 20000
        });

        // Act
        var allRentals = await _rentalRepository.GetAllAsync();

        // Assert
        Assert.NotNull(allRentals);
        Assert.Equal(2, allRentals.Count());
        Assert.Contains(allRentals, r => r.CustomerId == "CUST001");
        Assert.Contains(allRentals, r => r.CustomerId == "CUST002");
    }

    [Fact]
    public async Task VehicleCatalog_GetAllAsync_ReturnsAllVehicles()
    {
        // Arrange
        _vehicleCatalog.AddVehicle(new Vehicle
        {
            RegistrationNumber = "DEF456",
            VehicleTypeId = "truck",
            CurrentOdometer = 50000
        });

        // Act
        var allVehicles = await _vehicleCatalog.GetAllAsync();

        // Assert
        Assert.NotNull(allVehicles);
        Assert.Equal(2, allVehicles.Count); // ABC123 + DEF456
        Assert.Contains(allVehicles, v => v.RegistrationNumber == "ABC123");
        Assert.Contains(allVehicles, v => v.RegistrationNumber == "DEF456");
    }

    [Fact]
    public async Task VehicleCatalog_UpdateOdometerAsync_UpdatesVehicleOdometer()
    {
        // Arrange
        var originalVehicle = await _vehicleCatalog.GetByRegistrationNumberAsync("ABC123");
        Assert.NotNull(originalVehicle);
        Assert.Equal(10000, originalVehicle.CurrentOdometer);

        // Act
        var updated = await _vehicleCatalog.UpdateOdometerAsync("ABC123", 15500);

        // Assert
        Assert.True(updated);
        var vehicleAfterUpdate = await _vehicleCatalog.GetByRegistrationNumberAsync("ABC123");
        Assert.NotNull(vehicleAfterUpdate);
        Assert.Equal(15500, vehicleAfterUpdate.CurrentOdometer);
    }

    [Fact]
    public async Task VehicleCatalog_UpdateOdometerAsync_NonExistentVehicle_ReturnsFalse()
    {
        // Act
        var updated = await _vehicleCatalog.UpdateOdometerAsync("NONEXISTENT", 15500);

        // Assert
        Assert.False(updated);
    }

    [Fact]
    public async Task VehicleCatalog_UpdateOdometerAsync_CaseInsensitive()
    {
        // Act
        var updated = await _vehicleCatalog.UpdateOdometerAsync("abc123", 12000);

        // Assert
        Assert.True(updated);
        var vehicle = await _vehicleCatalog.GetByRegistrationNumberAsync("ABC123");
        Assert.NotNull(vehicle);
        Assert.Equal(12000, vehicle.CurrentOdometer);
    }

    [Fact]
    public async Task VehicleTypeStore_GetAllAsync_ReturnsAllVehicleTypes()
    {
        // Arrange
        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "truck",
            DisplayName = "Truck",
            PricingFormula = "baseDayRate * days * 1.5"
        });

        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "premium-suv",
            DisplayName = "Premium SUV",
            PricingFormula = "baseDayRate * days * 2"
        });

        // Act
        var allTypes = await _vehicleTypeStore.GetAllAsync();

        // Assert
        Assert.NotNull(allTypes);
        Assert.Equal(3, allTypes.Count); // small-car + truck + premium-suv
        Assert.Contains(allTypes, vt => vt.VehicleTypeId == "small-car");
        Assert.Contains(allTypes, vt => vt.VehicleTypeId == "truck");
        Assert.Contains(allTypes, vt => vt.VehicleTypeId == "premium-suv");
    }

    [Fact]
    public async Task MultipleCustomers_CanHaveActiveRentalsSimultaneously()
    {
        // Arrange - Setup multiple vehicles
        _vehicleCatalog.AddVehicle(new Vehicle
        {
            RegistrationNumber = "VEH001",
            VehicleTypeId = "small-car",
            CurrentOdometer = 10000
        });

        _vehicleCatalog.AddVehicle(new Vehicle
        {
            RegistrationNumber = "VEH002",
            VehicleTypeId = "small-car",
            CurrentOdometer = 20000
        });

        _vehicleCatalog.AddVehicle(new Vehicle
        {
            RegistrationNumber = "VEH003",
            VehicleTypeId = "small-car",
            CurrentOdometer = 30000
        });

        // Act - Multiple customers checking out simultaneously
        var request1 = new RegisterCheckoutRequest
        {
            BookingNumber = "BK001",
            CustomerId = "CUST001",
            RegistrationNumber = "VEH001",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 10000
        };

        var request2 = new RegisterCheckoutRequest
        {
            BookingNumber = "BK002",
            CustomerId = "CUST002",
            RegistrationNumber = "VEH002",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 20000
        };

        var request3 = new RegisterCheckoutRequest
        {
            BookingNumber = "BK003",
            CustomerId = "CUST003",
            RegistrationNumber = "VEH003",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 30000
        };

        var result1 = await _checkoutService.RegisterCheckoutAsync(request1);
        var result2 = await _checkoutService.RegisterCheckoutAsync(request2);
        var result3 = await _checkoutService.RegisterCheckoutAsync(request3);

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.True(result3.IsSuccess);

        var allRentals = await _rentalRepository.GetAllAsync();
        var activeRentals = allRentals.Where(r => r.IsActive).ToList();
        Assert.Equal(3, activeRentals.Count);
        Assert.All(activeRentals, r => Assert.True(r.IsActive));
    }
}
