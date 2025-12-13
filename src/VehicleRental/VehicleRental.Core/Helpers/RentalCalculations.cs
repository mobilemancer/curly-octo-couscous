namespace VehicleRental.Core.Helpers;

/// <summary>
/// Helper methods for rental calculations.
/// </summary>
public static class RentalCalculations
{
    /// <summary>
    /// Calculates the number of rental days between checkout and return timestamps.
    /// Always returns at least 1 day, and rounds up to the nearest whole day.
    /// </summary>
    /// <param name="checkoutTimestamp">The checkout timestamp.</param>
    /// <param name="returnTimestamp">The return timestamp.</param>
    /// <returns>The number of days (minimum 1).</returns>
    public static int CalculateDays(DateTimeOffset checkoutTimestamp, DateTimeOffset returnTimestamp)
    {
        var duration = returnTimestamp.UtcDateTime - checkoutTimestamp.UtcDateTime;
        var totalDays = duration.TotalDays;
        return Math.Max(1, (int)Math.Ceiling(totalDays));
    }

    /// <summary>
    /// Calculates the distance traveled based on odometer readings.
    /// </summary>
    /// <param name="checkoutOdometer">The odometer reading at checkout.</param>
    /// <param name="returnOdometer">The odometer reading at return.</param>
    /// <returns>The distance traveled in kilometers.</returns>
    public static decimal CalculateDistance(decimal checkoutOdometer, decimal returnOdometer)
    {
        return returnOdometer - checkoutOdometer;
    }

    /// <summary>
    /// Rounds the final rental price up to the nearest whole currency unit.
    /// </summary>
    /// <param name="rawPrice">The calculated price before rounding.</param>
    /// <returns>The price rounded up to the nearest integer.</returns>
    public static decimal RoundFinalPrice(decimal rawPrice)
    {
        return Math.Ceiling(rawPrice);
    }
}
