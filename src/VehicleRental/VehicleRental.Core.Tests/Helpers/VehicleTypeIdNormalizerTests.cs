using VehicleRental.Core.Helpers;

namespace VehicleRental.Core.Tests.Helpers;

public class VehicleTypeIdNormalizerTests
{
    [Theory]
    [InlineData("Small-Car", "small-car")]
    [InlineData("TRUCK", "truck")]
    [InlineData("  station-wagon  ", "station-wagon")]
    [InlineData("Premium-SUV", "premium-suv")]
    public void Normalize_ValidIds_ReturnsLowercaseAndTrimmed(string input, string expected)
    {
        // Act
        var result = VehicleTypeIdNormalizer.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Normalize_InvalidIds_ThrowsArgumentException(string? input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => VehicleTypeIdNormalizer.Normalize(input!));
    }
}
