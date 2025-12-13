namespace VehicleRental.Core.Helpers;

/// <summary>
/// Normalizes vehicle type IDs for consistent comparison and storage.
/// </summary>
public static class VehicleTypeIdNormalizer
{
    /// <summary>
    /// Normalizes a vehicle type ID by trimming whitespace and converting to lowercase.
    /// </summary>
    /// <param name="vehicleTypeId">The vehicle type ID to normalize.</param>
    /// <returns>The normalized vehicle type ID.</returns>
    /// <exception cref="ArgumentException">Thrown when the vehicle type ID is null, empty, or whitespace.</exception>
    public static string Normalize(string vehicleTypeId)
    {
        if (string.IsNullOrWhiteSpace(vehicleTypeId))
        {
            throw new ArgumentException("Vehicle type ID cannot be null, empty, or whitespace.", nameof(vehicleTypeId));
        }

        return vehicleTypeId.Trim().ToLowerInvariant();
    }
}
