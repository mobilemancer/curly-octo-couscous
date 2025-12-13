using VehicleRental.Core.Helpers;

namespace VehicleRental.Core.Tests.Helpers;

public class TimeZoneHandlingTests
{
    [Fact]
    public void CalculateDays_SameInstantDifferentOffsets_ReturnsSameDays()
    {
        // Arrange - Same absolute instant expressed in different time zones
        // 2025-12-10 10:00 UTC = 2025-12-10 05:00 EST (UTC-5) = 2025-12-10 18:00 JST (UTC+8)
        var checkoutUtc = new DateTimeOffset(2025, 12, 10, 10, 0, 0, TimeSpan.Zero);
        var checkoutEst = new DateTimeOffset(2025, 12, 10, 5, 0, 0, TimeSpan.FromHours(-5));
        var checkoutJst = new DateTimeOffset(2025, 12, 10, 18, 0, 0, TimeSpan.FromHours(8));

        // 2025-12-13 10:00 UTC (3 days later)
        var returnUtc = new DateTimeOffset(2025, 12, 13, 10, 0, 0, TimeSpan.Zero);
        var returnEst = new DateTimeOffset(2025, 12, 13, 5, 0, 0, TimeSpan.FromHours(-5));
        var returnJst = new DateTimeOffset(2025, 12, 13, 18, 0, 0, TimeSpan.FromHours(8));

        // Act - All combinations should yield same result
        var daysUtc = RentalCalculations.CalculateDays(checkoutUtc, returnUtc);
        var daysEstToUtc = RentalCalculations.CalculateDays(checkoutEst, returnUtc);
        var daysUtcToJst = RentalCalculations.CalculateDays(checkoutUtc, returnJst);
        var daysEstToJst = RentalCalculations.CalculateDays(checkoutEst, returnJst);
        var daysJst = RentalCalculations.CalculateDays(checkoutJst, returnJst);

        // Assert - All should be 3 days since the absolute duration is identical
        Assert.Equal(3, daysUtc);
        Assert.Equal(3, daysEstToUtc);
        Assert.Equal(3, daysUtcToJst);
        Assert.Equal(3, daysEstToJst);
        Assert.Equal(3, daysJst);
    }

    [Fact]
    public void CalculateDays_CrossingDstBoundary_CalculatesCorrectly()
    {
        // Arrange - In US, DST typically ends first Sunday in November (clocks back 1 hour)
        // Testing a rental that spans DST transition

        // Before DST ends: November 2, 2025 at 10:00 EDT (UTC-4)
        var checkoutBeforeDst = new DateTimeOffset(2025, 11, 2, 10, 0, 0, TimeSpan.FromHours(-4));

        // After DST ends: November 3, 2025 at 10:00 EST (UTC-5)
        var returnAfterDst = new DateTimeOffset(2025, 11, 3, 10, 0, 0, TimeSpan.FromHours(-5));

        // Act
        var days = RentalCalculations.CalculateDays(checkoutBeforeDst, returnAfterDst);

        // Assert - Should be 1 day based on UTC duration
        // (Nov 2 10:00 EDT = Nov 2 14:00 UTC, Nov 3 10:00 EST = Nov 3 15:00 UTC)
        // Duration: 25 hours in wall clock time, but calculation uses UTC instants
        Assert.Equal(2, days); // Ceiling of 1.04 days
    }

    [Fact]
    public void CalculateDays_PositiveOffsetVsNegativeOffset_CalculatesCorrectly()
    {
        // Arrange - Test with positive and negative UTC offsets
        // Tokyo (UTC+9) checkout
        var checkoutTokyo = new DateTimeOffset(2025, 12, 10, 18, 0, 0, TimeSpan.FromHours(9));

        // New York (UTC-5) return, same absolute instant + 2 days
        var returnNy = new DateTimeOffset(2025, 12, 12, 4, 0, 0, TimeSpan.FromHours(-5));

        // Act
        var days = RentalCalculations.CalculateDays(checkoutTokyo, returnNy);

        // Assert - Should be 2 days (checkout: Dec 10 09:00 UTC, return: Dec 12 09:00 UTC)
        Assert.Equal(2, days);
    }

    [Fact]
    public void CalculateDays_MidnightTransitions_HandlesCorrectly()
    {
        // Arrange - Checkout and return at different midnight times
        var checkoutMidnightUtc = new DateTimeOffset(2025, 12, 10, 0, 0, 0, TimeSpan.Zero);
        var returnMidnightLocal = new DateTimeOffset(2025, 12, 11, 0, 0, 0, TimeSpan.FromHours(-8)); // PST

        // Act
        var days = RentalCalculations.CalculateDays(checkoutMidnightUtc, returnMidnightLocal);

        // Assert - Dec 10 00:00 UTC to Dec 11 00:00 PST = Dec 11 08:00 UTC = 32 hours
        Assert.Equal(2, days); // Ceiling of 1.33 days
    }

    [Theory]
    [InlineData(0)]      // UTC
    [InlineData(-5)]     // EST
    [InlineData(-8)]     // PST
    [InlineData(1)]      // CET
    [InlineData(9)]      // JST
    [InlineData(5, 30)]  // IST (India Standard Time, UTC+5:30)
    public void CalculateDays_VariousTimeZones_ProducesConsistentResults(int hours, int minutes = 0)
    {
        // Arrange - Same absolute duration, different time zones
        var offset = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes);
        var checkout = new DateTimeOffset(2025, 12, 10, 10, 0, 0, offset);
        var returnTime = checkout.AddDays(5); // Exactly 5 days later in the same zone

        // Act
        var days = RentalCalculations.CalculateDays(checkout, returnTime);

        // Assert - Should always be 5 days regardless of time zone
        Assert.Equal(5, days);
    }

    [Fact]
    public void CalculateDays_SubDayDurationDifferentZones_RoundsToMinimumOneDay()
    {
        // Arrange - Checkout in one zone, return few hours later in different zone
        var checkoutUtc = new DateTimeOffset(2025, 12, 10, 10, 0, 0, TimeSpan.Zero);
        var returnPst = new DateTimeOffset(2025, 12, 10, 5, 0, 0, TimeSpan.FromHours(-8)); // 3 hours later UTC

        // Act
        var days = RentalCalculations.CalculateDays(checkoutUtc, returnPst);

        // Assert - Less than 1 day should round to 1
        Assert.Equal(1, days);
    }
}
