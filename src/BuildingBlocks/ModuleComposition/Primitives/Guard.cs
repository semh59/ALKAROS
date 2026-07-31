namespace ALKAROS.ModuleComposition.Primitives;

/// <summary>
/// Reusable pre-condition helpers. Contains no business logic.
/// </summary>
public static class Guard
{
    public static T NotNull<T>(T? value, string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        return value;
    }

    public static string NotNullOrWhiteSpace(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty or whitespace.", parameterName);
        return value;
    }

    public static T InRange<T>(T value, T min, T max, string parameterName)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Value must be between {min} and {max}.");
        return value;
    }
}