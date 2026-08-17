namespace ALKAROS.Kitchen.Routing;

/// <summary>
/// Domain entity representing a kitchen station / physical printer (kitchen.printers).
/// </summary>
public sealed class Printer
{
    public Printer(
        Guid id,
        string name,
        string stationId,
        string? ipAddress = null,
        int? port = null,
        bool isActive = true,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Printer ID cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Printer name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(stationId))
            throw new ArgumentException("Station ID cannot be empty.", nameof(stationId));
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

        Id = id;
        Name = name.Trim();
        StationId = stationId.Trim();
        IpAddress = ipAddress?.Trim();
        Port = port;
        IsActive = isActive;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string StationId { get; }
    public string? IpAddress { get; }
    public int? Port { get; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public Printer SetActive(bool active, DateTimeOffset? timestamp = null)
    {
        return new Printer(
            Id,
            Name,
            StationId,
            IpAddress,
            Port,
            isActive: active,
            createdAt: CreatedAt,
            updatedAt: timestamp ?? DateTimeOffset.UtcNow);
    }
}
