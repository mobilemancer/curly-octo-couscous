using VehicleRental.Core.Domain;
using VehicleRental.Core.Ports;

namespace VehicleRental.Core.Infrastructure.Testing;

public class FakeRentalRepository : IRentalRepository
{
    private readonly Dictionary<string, Rental> _rentals = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> ExistsAsync(string bookingNumber)
    {
        return Task.FromResult(_rentals.ContainsKey(bookingNumber));
    }

    public Task<Rental?> GetByBookingNumberAsync(string bookingNumber)
    {
        _rentals.TryGetValue(bookingNumber, out var rental);
        return Task.FromResult(rental);
    }

    public Task<bool> HasActiveRentalAsync(string registrationNumber)
    {
        var hasActive = _rentals.Values.Any(r =>
            r.RegistrationNumber.Equals(registrationNumber, StringComparison.OrdinalIgnoreCase) &&
            r.IsActive);
        return Task.FromResult(hasActive);
    }

    public Task AddAsync(Rental rental)
    {
        _rentals[rental.BookingNumber] = rental;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Rental rental)
    {
        _rentals[rental.BookingNumber] = rental;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<Rental>> GetAllAsync()
    {
        IEnumerable<Rental> rentals = _rentals.Values.ToList();
        return Task.FromResult(rentals);
    }
}
