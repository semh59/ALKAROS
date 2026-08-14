namespace ALKAROS.Tables.TableLifecycle;

/// <summary>
/// A zone row (table_mgmt.zones, PDF:III.5.1). Zones group tables on the
/// floor plan; a table without a zone id is an unzoned table.
/// </summary>
public sealed class Zone
{
    public Zone(Guid id, string code, string name, int sortOrder = 0, bool active = true)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Zone code cannot be empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Zone name cannot be empty.", nameof(name));

        Id = id;
        Code = code;
        Name = name;
        SortOrder = sortOrder;
        Active = active;
    }

    public Guid Id { get; }

    public string Code { get; }

    public string Name { get; }

    public int SortOrder { get; }

    public bool Active { get; }
}