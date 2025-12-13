using VehicleRental.Core.Helpers;

namespace VehicleRental.Core.Tests.Helpers;

public class RentalCalculationsTests
{
    [Theory]
    [InlineData("2025-12-10T10:00:00Z", "2025-12-10T11:00:00Z", 1)] // Less than 1 day -> rounds to 1
    [InlineData("2025-12-10T10:00:00Z", "2025-12-11T10:00:00Z", 1)] // Exactly 1 day
    [InlineData("2025-12-10T10:00:00Z", "2025-12-11T10:00:01Z", 2)] // Just over 1 day -> rounds to 2
    [InlineData("2025-12-10T10:00:00Z", "2025-12-12T09:59:59Z", 2)] // Just under 2 days -> rounds to 2
    [InlineData("2025-12-10T10:00:00Z", "2025-12-12T10:00:00Z", 2)] // Exactly 2 days
    [InlineData("2025-12-10T10:00:00Z", "2025-12-17T10:00:00Z", 7)] // Exactly 7 days
    public void CalculateDays_VariousDurations_ReturnsExpectedDays(string checkoutStr, string returnStr, int expectedDays)
    {
        // Arrange
        var checkout = DateTimeOffset.Parse(checkoutStr);
        var returnTime = DateTimeOffset.Parse(returnStr);

        // Act
        var result = RentalCalculations.CalculateDays(checkout, returnTime);

        // Assert
        Assert.Equal(expectedDays, result);
    }

    [Fact]
    public void CalculateDays_DifferentTimeZonesSameInstant_ReturnsSameDays()
    {
        // Arrange - same instant expressed in different time zones
        var checkoutUtc = new DateTimeOffset(2025, 12, 10, 10, 0, 0, TimeSpan.Zero);
        var checkoutEst = new DateTimeOffset(2025, 12, 10, 5, 0, 0, TimeSpan.FromHours(-5));
        var returnUtc = new DateTimeOffset(2025, 12, 12, 10, 0, 0, TimeSpan.Zero);
        var returnEst = new DateTimeOffset(2025, 12, 12, 5, 0, 0, TimeSpan.FromHours(-5));

        // Act
        var resultUtc = RentalCalculations.CalculateDays(checkoutUtc, returnUtc);
        var resultMixed = RentalCalculations.CalculateDays(checkoutEst, returnUtc);
        var resultEst = RentalCalculations.CalculateDays(checkoutEst, returnEst);

        // Assert - all should be 2 days since it's the same absolute duration
        Assert.Equal(2, resultUtc);
        Assert.Equal(2, resultMixed);
        Assert.Equal(2, resultEst);
    }

    [Theory]
    [InlineData(1000.0, 1500.0, 500.0)]
    [InlineData(50000.5, 50100.3, 99.8)]
    [InlineData(0.0, 0.0, 0.0)]
    public void CalculateDistance_ValidOdometerReadings_ReturnsCorrectDistance(decimal checkout, decimal returnOdo, decimal expected)
    {
        // Act
        var result = RentalCalculations.CalculateDistance(checkout, returnOdo);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(100.0, 100)]
    [InlineData(100.1, 101)]
    [InlineData(100.9, 101)]
    [InlineData(99.01, 100)]
    [InlineData(1234.567, 1235)]
    public void RoundFinalPrice_VariousPrices_RoundsUpCorrectly(decimal rawPrice, decimal expected)
    {
        // Act
        var result = RentalCalculations.RoundFinalPrice(rawPrice);

        // Assert
        Assert.Equal(expected, result);
    }
}
