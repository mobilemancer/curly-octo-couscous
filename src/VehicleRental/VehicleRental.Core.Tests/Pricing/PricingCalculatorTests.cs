using Microsoft.Extensions.Logging.Abstractions;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Infrastructure.Testing;
using VehicleRental.Core.Pricing;

namespace VehicleRental.Core.Tests.Pricing;

public class PricingCalculatorTests
{
    private readonly PricingCalculator _calculator;
    private readonly FakeVehicleTypeStore _vehicleTypeStore;
    private readonly SafeFormulaEvaluator _formulaEvaluator;

    public PricingCalculatorTests()
    {
        _vehicleTypeStore = new FakeVehicleTypeStore();
        _formulaEvaluator = new SafeFormulaEvaluator();
        _calculator = new PricingCalculator(
            _vehicleTypeStore,
            _formulaEvaluator,
            NullLogger<PricingCalculator>.Instance);

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

    #region Happy Path Tests

    [Fact]
    public async Task CalculateAsync_SmallCarFormula_ReturnsCorrectPrice()
    {
        // Arrange
        var parameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m };

        // Act
        var result = await _calculator.CalculateAsync("small-car", parameters, days: 3, km: 150);

        // Assert
        Assert.Equal(300, result); // baseDayRate * days = 100 * 3
    }

    [Fact]
    public async Task CalculateAsync_StationWagonFormula_ReturnsCorrectPrice()
    {
        // Arrange
        var parameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m };

        // Act
        var result = await _calculator.CalculateAsync("station-wagon", parameters, days: 3, km: 150);

        // Assert
        // (100 * 3 * 1.3) + (0.5 * 150) = 390 + 75 = 465
        Assert.Equal(465, result);
    }

    [Fact]
    public async Task CalculateAsync_ZeroKilometers_CalculatesCorrectly()
    {
        // Arrange
        var parameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m };

        // Act
        var result = await _calculator.CalculateAsync("station-wagon", parameters, days: 2, km: 0);

        // Assert
        // (100 * 2 * 1.3) + (0.5 * 0) = 260 + 0 = 260
        Assert.Equal(260, result);
    }

    [Fact]
    public async Task CalculateAsync_ZeroDays_CalculatesCorrectly()
    {
        // Arrange - edge case, though business logic would prevent this
        var parameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m };

        // Act
        var result = await _calculator.CalculateAsync("small-car", parameters, days: 0, km: 100);

        // Assert
        Assert.Equal(0, result); // baseDayRate * 0 = 0
    }

    #endregion

    #region Vehicle Type ID Normalization Tests

    [Theory]
    [InlineData("Small-Car")]
    [InlineData("SMALL-CAR")]
    [InlineData("  small-car  ")]
    [InlineData("SMALL-car")]
    public async Task CalculateAsync_VehicleTypeIdVariants_NormalizesAndFindsType(string vehicleTypeId)
    {
        // Arrange
        var parameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m };

        // Act
        var result = await _calculator.CalculateAsync(vehicleTypeId, parameters, days: 2, km: 100);

        // Assert
        Assert.Equal(200, result); // baseDayRate * days = 100 * 2
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CalculateAsync_InvalidVehicleTypeId_ThrowsArgumentException(string? vehicleTypeId)
    {
        // Arrange
        var parameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _calculator.CalculateAsync(vehicleTypeId!, parameters, days: 2, km: 100));
    }

    #endregion

    #region Vehicle Type Not Found Tests

    [Fact]
    public async Task CalculateAsync_VehicleTypeNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var parameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _calculator.CalculateAsync("unknown-type", parameters, days: 2, km: 100));

        Assert.Contains("Vehicle type not found", ex.Message);
        Assert.Contains("unknown-type", ex.Message);
    }

    #endregion

    #region Formula Evaluation Failure Tests

    [Fact]
    public async Task CalculateAsync_FormulaWithDivisionByZero_ThrowsInvalidOperationException()
    {
        // Arrange - Add a vehicle type with a formula that divides by a variable
        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "div-by-zero-test",
            DisplayName = "Test",
            PricingFormula = "baseDayRate / km" // Will fail when km = 0
        });

        var parameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _calculator.CalculateAsync("div-by-zero-test", parameters, days: 2, km: 0));

        Assert.Contains("Failed to evaluate pricing formula", ex.Message);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public async Task CalculateAsync_InvalidFormula_ThrowsInvalidOperationException()
    {
        // Arrange - Add a vehicle type with an invalid formula
        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "invalid-formula-test",
            DisplayName = "Test",
            PricingFormula = "baseDayRate * unknownVariable" // Unknown variable
        });

        var parameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _calculator.CalculateAsync("invalid-formula-test", parameters, days: 2, km: 100));

        Assert.Contains("Failed to evaluate pricing formula", ex.Message);
        Assert.NotNull(ex.InnerException);
    }

    #endregion

    #region Constructor Validation Tests

    [Fact]
    public void Constructor_NullVehicleTypeStore_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PricingCalculator(
            null!,
            _formulaEvaluator,
            NullLogger<PricingCalculator>.Instance));
    }

    [Fact]
    public void Constructor_NullFormulaEvaluator_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PricingCalculator(
            _vehicleTypeStore,
            null!,
            NullLogger<PricingCalculator>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PricingCalculator(
            _vehicleTypeStore,
            _formulaEvaluator,
            null!));
    }

    #endregion

    #region Complex Formula Tests

    [Fact]
    public async Task CalculateAsync_ComplexFormula_CalculatesCorrectly()
    {
        // Arrange - Add a complex vehicle type formula
        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "luxury-suv",
            DisplayName = "Luxury SUV",
            PricingFormula = "(baseDayRate * days * 2.0) + (baseKmPrice * km * 1.5)"
        });

        var parameters = new PricingParameters { BaseDayRate = 150, BaseKmPrice = 0.75m };

        // Act
        var result = await _calculator.CalculateAsync("luxury-suv", parameters, days: 5, km: 300);

        // Assert
        // (150 * 5 * 2.0) + (0.75 * 300 * 1.5) = 1500 + 337.5 = 1837.5
        Assert.Equal(1837.5m, result);
    }

    [Fact]
    public async Task CalculateAsync_FractionalResult_ReturnsExactValue()
    {
        // Arrange - Formula that produces fractional result
        _vehicleTypeStore.AddVehicleType(new VehicleTypeDefinition
        {
            VehicleTypeId = "fractional-test",
            DisplayName = "Test",
            PricingFormula = "baseDayRate * days * 1.7"
        });

        var parameters = new PricingParameters { BaseDayRate = 100, BaseKmPrice = 0.5m };

        // Act
        var result = await _calculator.CalculateAsync("fractional-test", parameters, days: 3, km: 0);

        // Assert - Raw price before rounding
        Assert.Equal(510m, result); // 100 * 3 * 1.7 = 510
    }

    #endregion
}
