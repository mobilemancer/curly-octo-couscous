namespace VehicleRental.Core.Tests.Helpers;

/// <summary>
/// Tests for timestamp parsing logic (simulates CLI timestamp parsing behavior)
/// </summary>
public class TimestampParsingTests
{
    [Theory]
    [InlineData("2025-12-13T10:00:00Z")] // UTC with Z
    [InlineData("2025-12-13T10:00:00+00:00")] // UTC with explicit offset
    [InlineData("2025-12-13T10:00:00-05:00")] // EST
    [InlineData("2025-12-13T10:00:00+09:00")] // JST
    [InlineData("2025-12-13T10:00:00+05:30")] // IST (half-hour offset)
    [InlineData("2025-12-13T10:00:00+05:45")] // Nepal (quarter-hour offset)
    public void ParseTimestamp_WithValidOffset_ParsesCorrectly(string timestamp)
    {
        // Act
        var success = DateTimeOffset.TryParse(timestamp, out var result);

        // Assert
        Assert.True(success);
        Assert.NotEqual(DateTimeOffset.MinValue, result);
    }

    [Fact]
    public void ParseTimestamp_UtcWithZ_PreservesUtcOffset()
    {
        // Arrange
        var timestamp = "2025-12-13T10:00:00Z";

        // Act
        var result = DateTimeOffset.Parse(timestamp);

        // Assert
        Assert.Equal(TimeSpan.Zero, result.Offset);
        Assert.Equal(new DateTime(2025, 12, 13, 10, 0, 0), result.UtcDateTime);
    }

    [Fact]
    public void ParseTimestamp_WithPositiveOffset_PreservesOffset()
    {
        // Arrange
        var timestamp = "2025-12-13T10:00:00+09:00";

        // Act
        var result = DateTimeOffset.Parse(timestamp);

        // Assert
        Assert.Equal(TimeSpan.FromHours(9), result.Offset);
        Assert.Equal(new DateTime(2025, 12, 13, 1, 0, 0), result.UtcDateTime); // 10:00 JST = 01:00 UTC
    }

    [Fact]
    public void ParseTimestamp_WithNegativeOffset_PreservesOffset()
    {
        // Arrange
        var timestamp = "2025-12-13T10:00:00-05:00";

        // Act
        var result = DateTimeOffset.Parse(timestamp);

        // Assert
        Assert.Equal(TimeSpan.FromHours(-5), result.Offset);
        Assert.Equal(new DateTime(2025, 12, 13, 15, 0, 0), result.UtcDateTime); // 10:00 EST = 15:00 UTC
    }

    [Theory]
    [InlineData("2025-12-13T10:00:00")] // Without offset
    [InlineData("2025-12-13 10:00:00")] // Space separator
    [InlineData("12/13/2025 10:00:00")] // US format
    public void ParseTimestamp_WithoutOffset_ParsesAsDateTime(string timestamp)
    {
        // Act - Without offset, it should parse as DateTime which can be converted to DateTimeOffset with local offset
        var success = DateTimeOffset.TryParse(timestamp, out var _);
        var successDt = DateTime.TryParse(timestamp, out var _);

        // Assert
        Assert.True(success || successDt);
    }

    [Theory]
    [InlineData("invalid-timestamp")]
    [InlineData("2025-13-45T10:00:00Z")] // Invalid month/day
    [InlineData("2025-12-13T25:00:00Z")] // Invalid hour
    [InlineData("2025-12-13T10:61:00Z")] // Invalid minute
    [InlineData("")]
    [InlineData("   ")]
    public void ParseTimestamp_WithInvalidFormat_FailsToParse(string timestamp)
    {
        // Act
        var success = DateTimeOffset.TryParse(timestamp, out _);

        // Assert
        Assert.False(success);
    }

    [Fact]
    public void ParseTimestamp_WithInvalidOffset_FailsToParse()
    {
        // Arrange - Offset must be between -14:00 and +14:00
        var timestamp = "2025-12-13T10:00:00+15:00";

        // Act
        var success = DateTimeOffset.TryParse(timestamp, out _);

        // Assert
        Assert.False(success);
    }

    [Theory]
    [InlineData("2025-12-13T10:00:00Z", "2025-12-13T11:00:00+01:00", true)] // Same instant
    [InlineData("2025-12-13T10:00:00+00:00", "2025-12-13T05:00:00-05:00", true)] // Same instant
    [InlineData("2025-12-13T10:00:00Z", "2025-12-13T10:00:00+01:00", false)] // Different instants
    public void ParseTimestamp_CompareAbsoluteInstants_WorksCorrectly(string timestamp1, string timestamp2, bool shouldBeEqual)
    {
        // Act
        var dt1 = DateTimeOffset.Parse(timestamp1);
        var dt2 = DateTimeOffset.Parse(timestamp2);

        // Assert
        Assert.Equal(shouldBeEqual, dt1.UtcDateTime == dt2.UtcDateTime);
    }

    [Fact]
    public void ParseTimestamp_LeapSecond_HandlesGracefully()
    {
        // Arrange - .NET doesn't support leap seconds, but should handle gracefully
        var timestamp = "2025-06-30T23:59:60Z"; // Hypothetical leap second

        // Act
        var success = DateTimeOffset.TryParse(timestamp, out var result);

        // Assert - .NET should either parse it as 23:59:59 or fail gracefully
        // This documents the behavior rather than enforcing it
        Assert.True(!success || result.Second == 59 || result.Second == 0);
    }

    [Theory]
    [InlineData("2024-02-29T10:00:00Z")] // Valid leap day
    [InlineData("2024-02-29T23:59:59Z")] // Valid leap day end
    public void ParseTimestamp_LeapDay_ParsesCorrectly(string timestamp)
    {
        // Act
        var success = DateTimeOffset.TryParse(timestamp, out var result);

        // Assert
        Assert.True(success);
        Assert.Equal(29, result.Day);
        Assert.Equal(2, result.Month);
    }

    [Theory]
    [InlineData("2025-02-29T10:00:00Z")] // Invalid - 2025 is not a leap year
    [InlineData("2023-02-29T10:00:00Z")] // Invalid - 2023 is not a leap year
    public void ParseTimestamp_NonLeapDayFebruary29_FailsToParse(string timestamp)
    {
        // Act
        var success = DateTimeOffset.TryParse(timestamp, out _);

        // Assert
        Assert.False(success);
    }

    [Theory]
    [InlineData("2025-12-31T23:59:59Z")]
    [InlineData("2025-01-01T00:00:00Z")]
    [InlineData("2025-12-13T00:00:00Z")]
    public void ParseTimestamp_BoundaryTimes_ParsesCorrectly(string timestamp)
    {
        // Act
        var success = DateTimeOffset.TryParse(timestamp, out var result);

        // Assert
        Assert.True(success);
        Assert.NotEqual(DateTimeOffset.MinValue, result);
    }

    [Fact]
    public void ParseTimestamp_Milliseconds_ParsesCorrectly()
    {
        // Arrange
        var timestamp = "2025-12-13T10:00:00.123Z";

        // Act
        var result = DateTimeOffset.Parse(timestamp);

        // Assert
        Assert.Equal(123, result.Millisecond);
    }

    [Fact]
    public void ParseTimestamp_Microseconds_ParsesOrTruncates()
    {
        // Arrange - .NET typically supports up to 7 decimal places (ticks)
        var timestamp = "2025-12-13T10:00:00.123456Z";

        // Act
        var success = DateTimeOffset.TryParse(timestamp, out _);

        // Assert - Should parse, precision may vary by framework
        Assert.True(success);
    }
}
