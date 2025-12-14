using Microsoft.Extensions.Logging.Abstractions;
using VehicleRental.Core.Application;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Infrastructure.Testing;
using VehicleRental.Core.Pricing;

namespace VehicleRental.Core.Tests.Application;

/// <summary>
/// End-to-end scenario tests that cover the full rental workflow:
/// 1. Add new vehicle type
/// 2. Add new vehicle of that type
/// 3. Rent and return the vehicle
/// </summary>
public class EndToEndRentalScenarioTests
{
    private readonly CheckoutService _checkoutService;
    private readonly ReturnService _returnService;
    private readonly FakeRentalRepository _rentalRepository;
    private readonly FakeVehicleCatalog _vehicleCatalog;
    private readonly FakeVehicleTypeStore _vehicleTypeStore;
    private readonly PricingCalculator _pricingCalculator;

    public EndToEndRentalScenarioTests()
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
    }

    [Fact]
    public async Task FullRentalWorkflow_NewVehicleType_NewVehicle_CheckoutAndReturn_Succeeds()
    {
        // =================================================================
        // Step 1: Add new vehicle type (e.g., "electric-suv")
        // =================================================================
        var newVehicleType = new VehicleTypeDefinition
        {
            VehicleTypeId = "electric-suv",
            DisplayName = "Electric SUV",
            PricingFormula = "(baseDayRate * days * 1.5) + (baseKmPrice * km * 0.8)"
        };
        _vehicleTypeStore.AddVehicleType(newVehicleType);

        // Verify vehicle type was added
        var retrievedType = await _vehicleTypeStore.GetByIdAsync("electric-suv");
        Assert.NotNull(retrievedType);
        Assert.Equal("Electric SUV", retrievedType.DisplayName);

        // =================================================================
        // Step 2: Add new vehicle of that type
        // =================================================================
        var newVehicle = new Vehicle
        {
            RegistrationNumber = "EV001",
            VehicleTypeId = "electric-suv",
            CurrentOdometer = 0,
            Location = "location-stockholm-001"
        };
        var addResult = await _vehicleCatalog.AddVehicleAsync(newVehicle);
        Assert.True(addResult);

        // Verify vehicle was added
        var retrievedVehicle = await _vehicleCatalog.GetByRegistrationNumberAsync("EV001");
        Assert.NotNull(retrievedVehicle);
        Assert.Equal("electric-suv", retrievedVehicle.VehicleTypeId);
        Assert.Equal("location-stockholm-001", retrievedVehicle.Location);

        // =================================================================
        // Step 3: Checkout the vehicle
        // =================================================================
        var checkoutTime = DateTimeOffset.Parse("2025-12-10T09:00:00Z");
        var checkoutRequest = new RegisterCheckoutRequest
        {
            BookingNumber = "BK-EV-001",
            CustomerId = "CUST-PREMIUM-001",
            RegistrationNumber = "EV001",
            CheckoutTimestamp = checkoutTime,
            CheckoutOdometer = 0
        };

        var checkoutResult = await _checkoutService.RegisterCheckoutAsync(checkoutRequest);

        Assert.True(checkoutResult.IsSuccess);
        Assert.NotNull(checkoutResult.Value);
        Assert.Equal("BK-EV-001", checkoutResult.Value.BookingNumber);
        Assert.Equal("EV001", checkoutResult.Value.RegistrationNumber);
        Assert.Equal("electric-suv", checkoutResult.Value.VehicleTypeId);

        // =================================================================
        // Step 4: Return the vehicle after 5 days and 250 km
        // =================================================================
        var returnRequest = new RegisterReturnRequest
        {
            BookingNumber = "BK-EV-001",
            ReturnTimestamp = checkoutTime.AddDays(5),
            ReturnOdometer = 250,
            PricingParameters = new PricingParameters
            {
                BaseDayRate = 150m,
                BaseKmPrice = 0.75m
            }
        };

        var returnResult = await _returnService.RegisterReturnAsync(returnRequest);

        Assert.True(returnResult.IsSuccess);
        Assert.NotNull(returnResult.Value);
        Assert.Equal("BK-EV-001", returnResult.Value.BookingNumber);

        // Verify pricing calculation: (150 * 5 * 1.5) + (0.75 * 250 * 0.8) = 1125 + 150 = 1275
        Assert.Equal(1275m, returnResult.Value.RentalPrice);
    }

    [Fact]
    public async Task FullRentalWorkflow_MultipleVehiclesOfNewType_IndependentRentals()
    {
        // =================================================================
        // Step 1: Add new vehicle type (e.g., "luxury-sedan")
        // =================================================================
        var luxuryType = new VehicleTypeDefinition
        {
            VehicleTypeId = "luxury-sedan",
            DisplayName = "Luxury Sedan",
            PricingFormula = "(baseDayRate * days * 2) + (baseKmPrice * km)"
        };
        _vehicleTypeStore.AddVehicleType(luxuryType);

        // =================================================================
        // Step 2: Add multiple vehicles of the new type
        // =================================================================
        var vehicle1 = new Vehicle
        {
            RegistrationNumber = "LUX001",
            VehicleTypeId = "luxury-sedan",
            CurrentOdometer = 5000,
            Location = "location-stockholm-001"
        };
        var vehicle2 = new Vehicle
        {
            RegistrationNumber = "LUX002",
            VehicleTypeId = "luxury-sedan",
            CurrentOdometer = 3000,
            Location = "location-stockholm-001"
        };

        await _vehicleCatalog.AddVehicleAsync(vehicle1);
        await _vehicleCatalog.AddVehicleAsync(vehicle2);

        // =================================================================
        // Step 3: Rent first vehicle
        // =================================================================
        var checkout1Time = DateTimeOffset.Parse("2025-12-10T10:00:00Z");
        var checkout1Result = await _checkoutService.RegisterCheckoutAsync(new RegisterCheckoutRequest
        {
            BookingNumber = "BK-LUX-001",
            CustomerId = "VIP-001",
            RegistrationNumber = "LUX001",
            CheckoutTimestamp = checkout1Time,
            CheckoutOdometer = 5000
        });

        Assert.True(checkout1Result.IsSuccess);

        // =================================================================
        // Step 4: Try to rent the same vehicle again (should fail - already rented)
        // =================================================================
        var duplicateCheckoutResult = await _checkoutService.RegisterCheckoutAsync(new RegisterCheckoutRequest
        {
            BookingNumber = "BK-LUX-002",
            CustomerId = "VIP-002",
            RegistrationNumber = "LUX001",
            CheckoutTimestamp = checkout1Time.AddHours(1),
            CheckoutOdometer = 5000
        });

        Assert.False(duplicateCheckoutResult.IsSuccess);
        Assert.Contains("already rented", duplicateCheckoutResult.Error, StringComparison.OrdinalIgnoreCase);

        // =================================================================
        // Step 5: Rent second vehicle (should succeed)
        // =================================================================
        var checkout2Result = await _checkoutService.RegisterCheckoutAsync(new RegisterCheckoutRequest
        {
            BookingNumber = "BK-LUX-003",
            CustomerId = "VIP-002",
            RegistrationNumber = "LUX002",
            CheckoutTimestamp = checkout1Time.AddHours(1),
            CheckoutOdometer = 3000
        });

        Assert.True(checkout2Result.IsSuccess);

        // =================================================================
        // Step 6: Return first vehicle
        // =================================================================
        var return1Result = await _returnService.RegisterReturnAsync(new RegisterReturnRequest
        {
            BookingNumber = "BK-LUX-001",
            ReturnTimestamp = checkout1Time.AddDays(2),
            ReturnOdometer = 5300,
            PricingParameters = new PricingParameters
            {
                BaseDayRate = 200m,
                BaseKmPrice = 1m
            }
        });

        Assert.True(return1Result.IsSuccess);
        // Pricing: (200 * 2 * 2) + (1 * 300) = 800 + 300 = 1100
        Assert.Equal(1100m, return1Result.Value!.RentalPrice);

        // =================================================================
        // Step 7: Now the first vehicle can be rented again
        // =================================================================
        var reRentResult = await _checkoutService.RegisterCheckoutAsync(new RegisterCheckoutRequest
        {
            BookingNumber = "BK-LUX-004",
            CustomerId = "VIP-003",
            RegistrationNumber = "LUX001",
            CheckoutTimestamp = checkout1Time.AddDays(3),
            CheckoutOdometer = 5300 // Odometer was updated after return
        });

        Assert.True(reRentResult.IsSuccess);
    }

    [Fact]
    public async Task FullRentalWorkflow_CannotRentVehicle_WhenVehicleTypeDoesNotExist()
    {
        // =================================================================
        // Step 1: Add vehicle with a type that doesn't exist in the store
        // =================================================================
        var orphanVehicle = new Vehicle
        {
            RegistrationNumber = "ORPHAN001",
            VehicleTypeId = "nonexistent-type",
            CurrentOdometer = 1000,
            Location = "location-stockholm-001"
        };
        await _vehicleCatalog.AddVehicleAsync(orphanVehicle);

        // =================================================================
        // Step 2: Try to checkout - should fail because type doesn't exist
        // =================================================================
        var checkoutResult = await _checkoutService.RegisterCheckoutAsync(new RegisterCheckoutRequest
        {
            BookingNumber = "BK-ORPHAN-001",
            CustomerId = "CUST-001",
            RegistrationNumber = "ORPHAN001",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 1000
        });

        Assert.False(checkoutResult.IsSuccess);
        Assert.Contains("vehicle type", checkoutResult.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FullRentalWorkflow_AddVehicleType_ThenAddVehicle_ThenRent_WithComplexPricing()
    {
        // =================================================================
        // Step 1: Add vehicle type with complex pricing formula
        // =================================================================
        var truckType = new VehicleTypeDefinition
        {
            VehicleTypeId = "heavy-truck",
            DisplayName = "Heavy Duty Truck",
            // Complex formula: base rate doubled, plus km charge, plus fixed fee per day
            PricingFormula = "(baseDayRate * days * 2) + (baseKmPrice * km * 1.5) + (days * 50)"
        };
        _vehicleTypeStore.AddVehicleType(truckType);

        // =================================================================
        // Step 2: Add a heavy truck
        // =================================================================
        var truck = new Vehicle
        {
            RegistrationNumber = "TRUCK001",
            VehicleTypeId = "heavy-truck",
            CurrentOdometer = 150000,
            Location = "location-goteborg-003"
        };
        await _vehicleCatalog.AddVehicleAsync(truck);

        // =================================================================
        // Step 3: Checkout the truck for a long rental
        // =================================================================
        var checkoutTime = DateTimeOffset.Parse("2025-12-01T08:00:00Z");
        var checkoutResult = await _checkoutService.RegisterCheckoutAsync(new RegisterCheckoutRequest
        {
            BookingNumber = "BK-TRUCK-001",
            CustomerId = "COMPANY-LOGISTICS",
            RegistrationNumber = "TRUCK001",
            CheckoutTimestamp = checkoutTime,
            CheckoutOdometer = 150000
        });

        Assert.True(checkoutResult.IsSuccess);
        Assert.Equal("heavy-truck", checkoutResult.Value!.VehicleTypeId);

        // =================================================================
        // Step 4: Return after 10 days and 2000 km
        // =================================================================
        var returnResult = await _returnService.RegisterReturnAsync(new RegisterReturnRequest
        {
            BookingNumber = "BK-TRUCK-001",
            ReturnTimestamp = checkoutTime.AddDays(10),
            ReturnOdometer = 152000,
            PricingParameters = new PricingParameters
            {
                BaseDayRate = 300m,
                BaseKmPrice = 2m
            }
        });

        Assert.True(returnResult.IsSuccess);
        // Pricing: (300 * 10 * 2) + (2 * 2000 * 1.5) + (10 * 50) = 6000 + 6000 + 500 = 12500
        Assert.Equal(12500m, returnResult.Value!.RentalPrice);
    }

    [Fact]
    public async Task FullRentalWorkflow_VehicleAtDifferentLocation_CanBeRented()
    {
        // =================================================================
        // Step 1: Add vehicle type
        // =================================================================
        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "compact-car",
            DisplayName = "Compact Car",
            PricingFormula = "baseDayRate * days"
        });

        // =================================================================
        // Step 2: Add vehicles at different locations
        // =================================================================
        await _vehicleCatalog.AddVehicleAsync(new Vehicle
        {
            RegistrationNumber = "STHLM001",
            VehicleTypeId = "compact-car",
            CurrentOdometer = 20000,
            Location = "location-stockholm-001"
        });

        await _vehicleCatalog.AddVehicleAsync(new Vehicle
        {
            RegistrationNumber = "MALMO001",
            VehicleTypeId = "compact-car",
            CurrentOdometer = 15000,
            Location = "location-malmo-002"
        });

        await _vehicleCatalog.AddVehicleAsync(new Vehicle
        {
            RegistrationNumber = "GBG001",
            VehicleTypeId = "compact-car",
            CurrentOdometer = 18000,
            Location = "location-goteborg-003"
        });

        // =================================================================
        // Step 3: Rent vehicles at each location independently
        // =================================================================
        var baseTime = DateTimeOffset.Parse("2025-12-10T10:00:00Z");

        // Stockholm rental
        var sthlmResult = await _checkoutService.RegisterCheckoutAsync(new RegisterCheckoutRequest
        {
            BookingNumber = "BK-STHLM-001",
            CustomerId = "CUST-STHLM",
            RegistrationNumber = "STHLM001",
            CheckoutTimestamp = baseTime,
            CheckoutOdometer = 20000
        });
        Assert.True(sthlmResult.IsSuccess);

        // Malmö rental
        var malmoResult = await _checkoutService.RegisterCheckoutAsync(new RegisterCheckoutRequest
        {
            BookingNumber = "BK-MALMO-001",
            CustomerId = "CUST-MALMO",
            RegistrationNumber = "MALMO001",
            CheckoutTimestamp = baseTime,
            CheckoutOdometer = 15000
        });
        Assert.True(malmoResult.IsSuccess);

        // Göteborg rental
        var gbgResult = await _checkoutService.RegisterCheckoutAsync(new RegisterCheckoutRequest
        {
            BookingNumber = "BK-GBG-001",
            CustomerId = "CUST-GBG",
            RegistrationNumber = "GBG001",
            CheckoutTimestamp = baseTime,
            CheckoutOdometer = 18000
        });
        Assert.True(gbgResult.IsSuccess);

        // Verify all three rentals exist
        var allRentals = await _rentalRepository.GetAllAsync();
        Assert.Equal(3, allRentals.Count());
    }

    [Fact]
    public async Task FullRentalWorkflow_RemoveVehicle_CannotRentRemovedVehicle()
    {
        // =================================================================
        // Step 1: Add vehicle type and vehicle
        // =================================================================
        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "van",
            DisplayName = "Cargo Van",
            PricingFormula = "baseDayRate * days"
        });

        await _vehicleCatalog.AddVehicleAsync(new Vehicle
        {
            RegistrationNumber = "VAN001",
            VehicleTypeId = "van",
            CurrentOdometer = 50000,
            Location = "location-stockholm-001"
        });

        // Verify vehicle exists
        var vehicle = await _vehicleCatalog.GetByRegistrationNumberAsync("VAN001");
        Assert.NotNull(vehicle);

        // =================================================================
        // Step 2: Remove the vehicle from catalog
        // =================================================================
        var removeResult = await _vehicleCatalog.RemoveVehicleAsync("VAN001");
        Assert.True(removeResult);

        // Verify vehicle no longer exists
        vehicle = await _vehicleCatalog.GetByRegistrationNumberAsync("VAN001");
        Assert.Null(vehicle);

        // =================================================================
        // Step 3: Try to rent removed vehicle - should fail
        // =================================================================
        var checkoutResult = await _checkoutService.RegisterCheckoutAsync(new RegisterCheckoutRequest
        {
            BookingNumber = "BK-VAN-001",
            CustomerId = "CUST-001",
            RegistrationNumber = "VAN001",
            CheckoutTimestamp = DateTimeOffset.UtcNow,
            CheckoutOdometer = 50000
        });

        Assert.False(checkoutResult.IsSuccess);
        Assert.Contains("not found", checkoutResult.Error, StringComparison.OrdinalIgnoreCase);
    }
}
