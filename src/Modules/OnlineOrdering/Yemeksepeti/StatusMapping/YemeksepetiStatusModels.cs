namespace ALKAROS.OnlineOrdering.Yemeksepeti.StatusMapping;

/// <summary>
/// The provider vocabulary this mapping was written against: the public Yemeksepeti Partner
/// API v2.0.2 document and its POS partner-picking FAQ (EXT:YSP-PARTNER-2.0.2). It is not
/// sandbox-verified — V0-YSP-001 is Blocked — so every status outside it is typed unknown.
/// </summary>
public static class YemeksepetiStatusVocabulary
{
    public const string Version = "YSP-PARTNER-2.0.2";
}

/// <summary>An order status as the provider's webhook reports it, exactly as received.</summary>
public sealed record YemeksepetiStatusSignal(
    string ExternalOrderId,
    string? Status,
    string? TransportType,
    YemeksepetiCancellationSignal? Cancellation = null);

/// <summary>The provider's <c>cancellation</c> object: <c>cancelled_by</c>, <c>reason</c>, <c>post_picked_up</c>.</summary>
public sealed record YemeksepetiCancellationSignal(string? CancelledBy, string? Reason, bool PostPickedUp);

public sealed class InvalidStatusSignalException : Exception
{
    public InvalidStatusSignalException(string message) : base(message) { }
}
