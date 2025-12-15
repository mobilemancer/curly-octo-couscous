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

    [Fact]
    public void CalculateDays_LeapYearFebruary29_CalculatesCorrectly()
    {
        // Arrange - Rental spanning Feb 29 in leap year 2024
        var checkoutLeapDay = new DateTimeOffset(2024, 2, 28, 12, 0, 0, TimeSpan.Zero);
        var returnAfterLeapDay = new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero);

        // Act
        var days = RentalCalculations.CalculateDays(checkoutLeapDay, returnAfterLeapDay);

        // Assert - Should be 2 days (Feb 28 -> Feb 29 -> Mar 1)
        Assert.Equal(2, days);
    }

    [Fact]
    public void CalculateDays_LeapYearWithDifferentTimeZones_CalculatesCorrectly()
    {
        // Arrange - Checkout on Feb 28 in Tokyo, return on Mar 2 in New York
        // This tests leap year + timezone combination
        var checkoutTokyo = new DateTimeOffset(2024, 2, 28, 20, 0, 0, TimeSpan.FromHours(9)); // Feb 28 20:00 JST = Feb 28 11:00 UTC
        var returnNy = new DateTimeOffset(2024, 3, 2, 6, 0, 0, TimeSpan.FromHours(-5)); // Mar 2 06:00 EST = Mar 2 11:00 UTC

        // Act
        var days = RentalCalculations.CalculateDays(checkoutTokyo, returnNy);

        // Assert - Duration is exactly 3 days in UTC (Feb 28 11:00 UTC to Mar 2 11:00 UTC)
        Assert.Equal(3, days);
    }

    [Fact]
    public void CalculateDays_NonLeapYearFebruary_CalculatesCorrectly()
    {
        // Arrange - Rental in non-leap year (2025 is not a leap year)
        var checkoutFeb28 = new DateTimeOffset(2025, 2, 28, 12, 0, 0, TimeSpan.Zero);
        var returnMar1 = new DateTimeOffset(2025, 3, 1, 12, 0, 0, TimeSpan.Zero);

        // Act
        var days = RentalCalculations.CalculateDays(checkoutFeb28, returnMar1);

        // Assert - Should be 1 day (Feb 28 -> Mar 1, no Feb 29)
        Assert.Equal(1, days);
    }

    [Theory]
    [InlineData("2024-02-29T10:00:00Z", "2024-03-01T10:00:00Z", 1)] // Leap year
    [InlineData("2024-02-28T10:00:00Z", "2024-03-02T10:00:00Z", 3)] // Spanning leap day
    [InlineData("2025-02-28T10:00:00Z", "2025-03-01T10:00:00Z", 1)] // Non-leap year
    [InlineData("2024-12-31T23:59:59Z", "2025-01-01T00:00:01Z", 1)] // Year boundary
    public void CalculateDays_SpecialDateBoundaries_CalculatesCorrectly(string checkoutStr, string returnStr, int expectedDays)
    {
        // Arrange
        var checkout = DateTimeOffset.Parse(checkoutStr);
        var returnTime = DateTimeOffset.Parse(returnStr);

        // Act
        var days = RentalCalculations.CalculateDays(checkout, returnTime);

        // Assert
        Assert.Equal(expectedDays, days);
    }

    [Fact]
    public void CalculateDays_SpringForwardDst_CalculatesCorrectly()
    {
        // Arrange - US Spring DST transition (clocks forward, "lose" an hour)
        // March 9, 2025 at 2:00 AM becomes 3:00 AM (EDT starts)

        // Before DST: March 8, 2025 at 10:00 EST (UTC-5)
        var checkoutBeforeDst = new DateTimeOffset(2025, 3, 8, 10, 0, 0, TimeSpan.FromHours(-5));

        // After DST: March 10, 2025 at 10:00 EDT (UTC-4)
        var returnAfterDst = new DateTimeOffset(2025, 3, 10, 10, 0, 0, TimeSpan.FromHours(-4));

        // Act
        var days = RentalCalculations.CalculateDays(checkoutBeforeDst, returnAfterDst);

        // Assert - March 8 10:00 EST = March 8 15:00 UTC, March 10 10:00 EDT = March 10 14:00 UTC
        // Duration: 47 hours = 1.958 days -> ceiling = 2 days
        Assert.Equal(2, days);
    }

    [Fact]
    public void CalculateDays_FallBackDst_CalculatesCorrectly()
    {
        // Arrange - US Fall DST transition (clocks back, "gain" an hour)
        // November 2, 2025 at 2:00 AM becomes 1:00 AM (EST starts)

        // Before DST ends: November 1, 2025 at 10:00 EDT (UTC-4)
        var checkoutBeforeDst = new DateTimeOffset(2025, 11, 1, 10, 0, 0, TimeSpan.FromHours(-4));

        // After DST ends: November 3, 2025 at 10:00 EST (UTC-5)
        var returnAfterDst = new DateTimeOffset(2025, 11, 3, 10, 0, 0, TimeSpan.FromHours(-5));

        // Act
        var days = RentalCalculations.CalculateDays(checkoutBeforeDst, returnAfterDst);

        // Assert - Nov 1 10:00 EDT = Nov 1 14:00 UTC, Nov 3 10:00 EST = Nov 3 15:00 UTC
        // Duration: 49 hours = 2.041 days -> ceiling = 3 days
        Assert.Equal(3, days);
    }

    [Fact]
    public void CalculateDays_ExtremeTimeZoneOffsets_CalculatesCorrectly()
    {
        // Arrange - Test with extreme UTC offsets
        // Kiribati (UTC+14) - easternmost timezone
        var checkoutKiribati = new DateTimeOffset(2025, 12, 10, 18, 0, 0, TimeSpan.FromHours(14));

        // Baker Island (UTC-12) - westernmost timezone
        var returnBaker = new DateTimeOffset(2025, 12, 12, 16, 0, 0, TimeSpan.FromHours(-12));

        // Act
        var days = RentalCalculations.CalculateDays(checkoutKiribati, returnBaker);

        // Assert - Dec 10 18:00 UTC+14 = Dec 10 04:00 UTC, Dec 12 16:00 UTC-12 = Dec 13 04:00 UTC
        // Duration: exactly 3 days
        Assert.Equal(3, days);
    }

    [Fact]
    public void CalculateDays_HalfHourOffset_CalculatesCorrectly()
    {
        // Arrange - India Standard Time (UTC+5:30)
        var checkoutIndia = new DateTimeOffset(2025, 12, 10, 14, 30, 0, TimeSpan.FromHours(5.5));

        // Afghanistan Time (UTC+4:30)
        var returnAfghanistan = new DateTimeOffset(2025, 12, 13, 13, 30, 0, TimeSpan.FromHours(4.5));

        // Act
        var days = RentalCalculations.CalculateDays(checkoutIndia, returnAfghanistan);

        // Assert - Dec 10 14:30 IST = Dec 10 09:00 UTC, Dec 13 13:30 AFT = Dec 13 09:00 UTC
        // Duration: exactly 3 days
        Assert.Equal(3, days);
    }

    [Fact]
    public void CalculateDays_QuarterHourOffset_CalculatesCorrectly()
    {
        // Arrange - Nepal Time (UTC+5:45) - one of the few quarter-hour offsets
        var checkoutNepal = new DateTimeOffset(2025, 12, 10, 14, 45, 0, new TimeSpan(5, 45, 0));
        var returnNepal = new DateTimeOffset(2025, 12, 15, 14, 45, 0, new TimeSpan(5, 45, 0));

        // Act
        var days = RentalCalculations.CalculateDays(checkoutNepal, returnNepal);

        // Assert - Exactly 5 days
        Assert.Equal(5, days);
    }

    [Fact]
    public void CalculateDays_CrossingInternationalDateLine_CalculatesCorrectly()
    {
        // Arrange - Crossing International Date Line
        // Checkout in Samoa (UTC-11), return in Kiribati (UTC+14)
        // These locations are adjacent but have 25-hour time difference

        var checkoutSamoa = new DateTimeOffset(2025, 12, 10, 22, 0, 0, TimeSpan.FromHours(-11));
        var returnKiribati = new DateTimeOffset(2025, 12, 12, 23, 0, 0, TimeSpan.FromHours(14));

        // Act
        var days = RentalCalculations.CalculateDays(checkoutSamoa, returnKiribati);

        // Assert - Dec 10 22:00 UTC-11 = Dec 11 09:00 UTC, Dec 12 23:00 UTC+14 = Dec 12 09:00 UTC
        // Duration: exactly 1 day
        Assert.Equal(1, days);
    }

    [Theory]
    [InlineData(23, 59, 59, 1)] // Almost 1 full day
    [InlineData(24, 0, 1, 2)]   // Just over 1 day
    [InlineData(48, 0, 0, 2)]   // Exactly 2 days
    [InlineData(47, 59, 59, 2)] // Just under 2 days
    [InlineData(72, 30, 0, 4)]  // 3.0125 days -> ceiling = 4
    public void CalculateDays_PreciseDurationInHours_RoundsCorrectly(int hours, int minutes, int seconds, int expectedDays)
    {
        // Arrange
        var checkout = new DateTimeOffset(2025, 12, 10, 10, 0, 0, TimeSpan.Zero);
        var returnTime = checkout.AddHours(hours).AddMinutes(minutes).AddSeconds(seconds);

        // Act
        var days = RentalCalculations.CalculateDays(checkout, returnTime);

        // Assert
        Assert.Equal(expectedDays, days);
    }

    [Fact]
    public void CalculateDays_LongRentalMultipleMonths_CalculatesCorrectly()
    {
        // Arrange - 90-day rental across different months
        var checkout = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.FromHours(-5)); // EST
        var returnTime = new DateTimeOffset(2025, 4, 1, 12, 0, 0, TimeSpan.FromHours(-4)); // EDT (DST started)

        // Act
        var days = RentalCalculations.CalculateDays(checkout, returnTime);

        // Assert - Jan 1 12:00 EST = Jan 1 17:00 UTC, Apr 1 12:00 EDT = Apr 1 16:00 UTC
        // Duration: 89 days 23 hours -> ceiling = 90 days
        Assert.Equal(90, days);
    }

    [Fact]
    public void CalculateDays_YearLongRental_CalculatesCorrectly()
    {
        // Arrange - 1 year rental
        var checkout = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var returnTime = new DateTimeOffset(2024, 12, 31, 23, 59, 59, TimeSpan.Zero);

        // Act
        var days = RentalCalculations.CalculateDays(checkout, returnTime);

        // Assert - 2024 is a leap year, so 366 days total (minus 1 second)
        Assert.Equal(366, days);
    }

    [Fact]
    public void CalculateDays_CheckoutAndReturnAtDifferentSecondsOfDay_HandlesCorrectly()
    {
        // Arrange - Testing precision at second level
        var checkoutMorning = new DateTimeOffset(2025, 12, 10, 9, 30, 45, TimeSpan.Zero);
        var returnEvening = new DateTimeOffset(2025, 12, 10, 21, 45, 30, TimeSpan.Zero);

        // Act
        var days = RentalCalculations.CalculateDays(checkoutMorning, returnEvening);

        // Assert - 12 hours 14 minutes 45 seconds = 0.5102 days -> ceiling = 1 day
        Assert.Equal(1, days);
    }
}
