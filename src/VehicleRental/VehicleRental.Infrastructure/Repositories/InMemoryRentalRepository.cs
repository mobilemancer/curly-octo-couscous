using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using VehicleRental.Core.Domain;
using VehicleRental.Core.Ports;

namespace VehicleRental.Infrastructure.Repositories;

/// <summary>
/// In-memory implementation of rental repository for Phase 1.
/// </summary>
public class InMemoryRentalRepository : IRentalRepository
{
    private readonly ConcurrentDictionary<string, Rental> _rentals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<InMemoryRentalRepository> _logger;

    public InMemoryRentalRepository(ILogger<InMemoryRentalRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<bool> ExistsAsync(string bookingNumber)
    {
        if (string.IsNullOrWhiteSpace(bookingNumber))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_rentals.ContainsKey(bookingNumber));
    }

    public Task<Rental?> GetByBookingNumberAsync(string bookingNumber)
    {
        if (string.IsNullOrWhiteSpace(bookingNumber))
        {
            return Task.FromResult<Rental?>(null);
        }

        _rentals.TryGetValue(bookingNumber, out var rental);
        return Task.FromResult(rental);
    }

    public Task<bool> HasActiveRentalAsync(string registrationNumber)
    {
        if (string.IsNullOrWhiteSpace(registrationNumber))
        {
            return Task.FromResult(false);
        }

        var hasActive = _rentals.Values.Any(r =>
            r.RegistrationNumber.Equals(registrationNumber, StringComparison.OrdinalIgnoreCase) &&
            r.IsActive);

        return Task.FromResult(hasActive);
    }

    public Task AddAsync(Rental rental)
    {
        if (rental is null)
        {
            throw new ArgumentNullException(nameof(rental));
        }

        if (!_rentals.TryAdd(rental.BookingNumber, rental))
        {
            _logger.LogError("Failed to add rental: booking number {BookingNumber} already exists", rental.BookingNumber);
            throw new InvalidOperationException($"Rental with booking number '{rental.BookingNumber}' already exists.");
        }

        _logger.LogInformation("Added rental {BookingNumber} for vehicle {RegistrationNumber}",
            rental.BookingNumber, rental.RegistrationNumber);

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Rental rental)
    {
        if (rental is null)
        {
            throw new ArgumentNullException(nameof(rental));
        }

        _rentals[rental.BookingNumber] = rental;

        _logger.LogInformation("Updated rental {BookingNumber}", rental.BookingNumber);

        return Task.CompletedTask;
    }

    public Task<IEnumerable<Rental>> GetAllAsync()
    {
        var rentals = _rentals.Values.OrderByDescending(r => r.CheckoutTimestamp);
        return Task.FromResult<IEnumerable<Rental>>(rentals);
    }
}
