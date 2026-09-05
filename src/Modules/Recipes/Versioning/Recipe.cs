namespace ALKAROS.Recipes.Versioning;

/// <summary>
/// Root recipe entity defining a product or menu item recipe identity.
/// </summary>
public sealed class Recipe
{
    public Recipe(
        Guid id,
        string code,
        string name,
        string? description,
        DateTimeOffset createdAt,
        int rowVersion = 1)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Recipe code cannot be empty.", nameof(code));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Recipe name cannot be empty.", nameof(name));

        Id = id;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = description?.Trim();
        CreatedAt = createdAt;
        RowVersion = rowVersion;
    }

    public Guid Id { get; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public int RowVersion { get; internal set; }

    public static Recipe Create(string code, string name, string? description = null, DateTimeOffset? createdAt = null)
    {
        return new Recipe(
            id: Guid.NewGuid(),
            code: code,
            name: name,
            description: description,
            createdAt: createdAt ?? DateTimeOffset.UtcNow,
            rowVersion: 1);
    }

    public void UpdateDetails(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Recipe name cannot be empty.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim();
    }
}
