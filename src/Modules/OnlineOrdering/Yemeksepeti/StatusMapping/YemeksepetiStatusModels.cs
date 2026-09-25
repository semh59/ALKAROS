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

public enum StatusMappingKind
{
    /// <summary>The status asks for exactly one internal order command.</summary>
    Command,

    /// <summary>The status is known and deliberately changes nothing internally.</summary>
    NoOp,

    /// <summary>The status (or its combination with the delivery kind) is outside the vocabulary; the order must not change.</summary>
    Unknown
}

public enum InternalOrderCommand
{
    AcceptIncomingOrder,
    CancelOrder
}

public enum StatusNoOpReason
{
    /// <summary>The provider echoes a status this restaurant itself sent.</summary>
    OwnOutboundStatusEcho,

    /// <summary>A platform courier collected the order; no internal lifecycle state follows from it.</summary>
    HandedToCourier,

    /// <summary>The platform reports delivery; payment and closing, not the provider, finish the internal order.</summary>
    DeliveredByPlatform
}

public enum CancellationParty
{
    Customer,
    Vendor,
    Logistics,

    /// <summary>The provider named a party outside the documented three; kept raw for review.</summary>
    Unrecognized
}

public sealed record CancellationDetail(CancellationParty Party, string? RawParty, string? Reason, bool AfterPickup);

/// <summary>
/// Evidence of a status the mapping did not recognise. <see cref="EvidenceId"/> is derived only
/// from the vocabulary version and the raw values, so a provider retry of the same status yields
/// the same evidence and a store keyed on it records it once.
/// </summary>
public sealed record UnknownStatusEvidence(
    Guid EvidenceId,
    string VocabularyVersion,
    string ExternalOrderId,
    string? RawStatus,
    string? RawTransportType);

/// <summary>The single outcome of one provider status. Exactly one of the detail fields matches <see cref="Kind"/>.</summary>
public sealed record StatusMappingResult(
    StatusMappingKind Kind,
    InternalOrderCommand? Command,
    CancellationDetail? Cancellation,
    StatusNoOpReason? NoOpReason,
    UnknownStatusEvidence? Evidence);

public sealed class InvalidStatusSignalException : Exception
{
    public InvalidStatusSignalException(string message) : base(message) { }
}
