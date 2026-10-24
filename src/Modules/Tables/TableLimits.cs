namespace ALKAROS.Tables;

/// <summary>Upper bounds that keep a table command's free text and counts within what a floor can mean.</summary>
public static class TableLimits
{
    public const int MaxReasonLength = 500;
    public const int MaxPartySize = 200;
    public const int MaxMergeParticipants = 20;
    public const int MaxCapacity = 500;

    public static void RequireReason(string? reason, string label, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException($"{label} cannot be empty.", parameterName);
        if (reason.Length > MaxReasonLength)
            throw new ArgumentException($"{label} cannot be longer than {MaxReasonLength} characters.", parameterName);
    }
}
