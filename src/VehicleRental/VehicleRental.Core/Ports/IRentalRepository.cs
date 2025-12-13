using VehicleRental.Core.Domain;

namespace VehicleRental.Core.Ports;

/// <summary>
/// Repository for managing rental records.
/// </summary>
public interface IRentalRepository
{
    /// <summary>
    /// Checks if a rental with the given booking number exists.
    /// </summary>
    /// <param name="bookingNumber">The booking number to check.</param>
    /// <returns>True if a rental with this booking number exists; otherwise, false.</returns>
    Task<bool> ExistsAsync(string bookingNumber);

    /// <summary>
    /// Retrieves a rental by its booking number.
    /// </summary>
    /// <param name="bookingNumber">The booking number to retrieve.</param>
    /// <returns>The rental if found; otherwise, null.</returns>
    Task<Rental?> GetByBookingNumberAsync(string bookingNumber);

    /// <summary>
    /// Checks if the specified vehicle currently has an active rental.
    /// </summary>
    /// <param name="registrationNumber">The vehicle registration number to check.</param>
    /// <returns>True if the vehicle has an active rental; otherwise, false.</returns>
    Task<bool> HasActiveRentalAsync(string registrationNumber);

    /// <summary>
    /// Adds a new rental to the repository.
    /// </summary>
    /// <param name="rental">The rental to add.</param>
    Task AddAsync(Rental rental);

    /// <summary>
    /// Updates an existing rental in the repository.
    /// </summary>
    /// <param name="rental">The rental with updated values.</param>
    Task UpdateAsync(Rental rental);
}
