namespace VehicleRental.Shared.Contracts;

/// <summary>
/// Notification sent from server to clients when vehicle types are updated.
/// </summary>
public sealed record VehicleTypeUpdateNotification
{
    /// <summary>
    /// Type of update that occurred.
    /// </summary>
    public required UpdateType UpdateType { get; init; }

    /// <summary>
    /// Timestamp when the update occurred on the server.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// List of vehicle type IDs that were affected by this update.
    /// </summary>
    public required List<string> AffectedTypeIds { get; init; }

    /// <summary>
    /// Optional human-readable message describing the update.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// New version number after the update.
    /// </summary>
    public int NewVersion { get; init; }
}

/// <summary>
/// Types of updates that can occur to vehicle types.
/// </summary>
public enum UpdateType
{
    /// <summary>
    /// One or more vehicle types were added.
    /// </summary>
    Added,

    /// <summary>
    /// One or more vehicle types were modified.
    /// </summary>
    Modified,

    /// <summary>
    /// One or more vehicle types were deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// Multiple types were added, modified, or deleted.
    /// </summary>
    Bulk
}
