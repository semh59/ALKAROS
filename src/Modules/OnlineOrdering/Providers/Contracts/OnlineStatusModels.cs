namespace ALKAROS.OnlineOrdering.Providers.Contracts;

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
