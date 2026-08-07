namespace ALKAROS.Catalog.ProductCatalog;

/// <summary>
/// Represents a group of modifiers (e.g., "Toppings", "Sizes") (PDF III.4.5).
/// </summary>
public sealed class ModifierGroup
{
    public ModifierGroup(
        Guid id,
        string code,
        string name,
        SelectionType selectionType,
        int minSelections = 0,
        int maxSelections = 1,
        bool active = true)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Modifier group code cannot be empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Modifier group name cannot be empty.", nameof(name));
        if (minSelections < 0)
            throw new ArgumentOutOfRangeException(nameof(minSelections), "Minimum selection cannot be negative.");
        if (maxSelections < minSelections)
            throw new ArgumentOutOfRangeException(nameof(maxSelections), "Maximum selection must be >= minimum selection.");

        Id = id;
        Code = code;
        Name = name;
        SelectionType = selectionType;
        MinSelections = minSelections;
        MaxSelections = maxSelections;
        Active = active;
    }

    public Guid Id { get; }
    public string Code { get; }
    public string Name { get; }
    public SelectionType SelectionType { get; }
    public int MinSelections { get; }
    public int MaxSelections { get; }
    public bool Active { get; }
}
