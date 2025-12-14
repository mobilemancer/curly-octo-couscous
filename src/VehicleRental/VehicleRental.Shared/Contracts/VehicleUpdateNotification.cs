namespace VehicleRental.Shared.Contracts;

/// <summary>
/// Notification sent to clients when vehicles are added, removed, or modified.
/// </summary>
public sealed record VehicleUpdateNotification
{
    /// <summary>
    /// Gets the type of update.
    /// </summary>
    public required VehicleUpdateType UpdateType { get; init; }

    /// <summary>
    /// Gets the affected vehicle.
    /// </summary>
    public VehicleDto? Vehicle { get; init; }

    /// <summary>
    /// Gets the registration number of the removed vehicle (for Delete operations).
    /// </summary>
    public string? RegistrationNumber { get; init; }

    /// <summary>
    /// Gets the location where the change occurred.
    /// </summary>
    public required string Location { get; init; }
}

/// <summary>
/// Type of vehicle update notification.
/// </summary>
public enum VehicleUpdateType
{
    /// <summary>
    /// A new vehicle was added to the fleet.
    /// </summary>
    Added,

    /// <summary>
    /// An existing vehicle was removed from the fleet.
    /// </summary>
    Removed,

    /// <summary>
    /// An existing vehicle was modified (e.g., odometer update).
    /// </summary>
    Modified
}
