namespace ALKAROS.Menu.StaticMenu;

public sealed class Menu
{
    public Guid Id { get; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Menu(
        Guid id,
        string code,
        string name,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Menu id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Menu code cannot be empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Menu name cannot be empty.", nameof(name));

        Id = id;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Menu Create(string code, string name)
    {
        var now = DateTimeOffset.UtcNow;
        return new Menu(Guid.NewGuid(), code, name, true, now, now);
    }

    public void Update(string name, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Menu name cannot be empty.", nameof(name));

        Name = name.Trim();
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
