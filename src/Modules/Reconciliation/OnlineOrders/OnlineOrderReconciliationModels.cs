using System.Text.Json;
using System.Text.Json.Serialization;
using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.Payments;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>The kinds of local/provider divergence V12-REC-001 turns into reconciliation cases.</summary>
public static class OnlineOrderDivergenceKind
{
    /// <summary>The provider accepted an order this restaurant refused (unmapped item, no stock); no local order exists.</summary>
    public const string ProviderAcceptedLocallyRefused = "ProviderAcceptedLocallyRefused";

    /// <summary>A stored provider event kept failing and was closed unprocessed.</summary>
    public const string ProviderEventFailed = "ProviderEventFailed";

    /// <summary>A local order changed status but the provider was never told (the outbox message is dead).</summary>
    public const string LocallyAcceptedProviderUnknown = "LocallyAcceptedProviderUnknown";

    /// <summary>The provider cancelled an order that had already been handed over locally.</summary>
    public const string CancelledAfterHandover = "CancelledAfterHandover";

    /// <summary>A channel was never told a product's current sellable quantity.</summary>
    public const string AvailabilityNotDelivered = "AvailabilityNotDelivered";

    /// <summary>V12-RMD-004: the provider's sub-total differed from the lines the local order was built from.</summary>
    public const string ProviderTotalMismatch = "ProviderTotalMismatch";

    /// <summary>V12-RMD-004: a provider event with a status or delivery kind the mapper does not know; nothing was done locally.</summary>
    public const string ProviderStatusUnknown = "ProviderStatusUnknown";

    /// <summary>V12-RMD-008: the provider priced one or more items differently from the catalog.</summary>
    public const string ProviderPriceMismatch = "ProviderPriceMismatch";

    /// <summary>V12-ONL-009: a platform's order polling keeps failing; orders its webhook missed are not arriving.</summary>
    public const string ProviderPollingFailing = "ProviderPollingFailing";

    /// <summary>An accepted online order stayed open for hours: nobody handed it over or cancelled it.</summary>
    public const string NotHandedOver = "NotHandedOver";
}

/// <summary>The safe next action each case carries; only the first three can be retried from the case.</summary>
public static class OnlineOrderNextAction
{
    /// <summary>Fix the product mapping, then process the stored provider event again.</summary>
    public const string ReprocessProviderEvent = "ReprocessProviderEvent";

    /// <summary>Send the cancellation the provider was never told about again.</summary>
    public const string ResendProviderCancellation = "ResendProviderCancellation";

    /// <summary>Send the status update the provider was never told about again.</summary>
    public const string ResendProviderUpdate = "ResendProviderUpdate";

    /// <summary>Nothing can be retried: settle it with the provider and record the outcome.</summary>
    public const string SettleWithProvider = "SettleWithProvider";

    /// <summary>The availability publisher already retries every round; check the channel's connection.</summary>
    public const string CheckChannelConnection = "CheckChannelConnection";

    /// <summary>Nothing to retry: hand the order over or cancel it on the online orders screen.</summary>
    public const string HandOverOrCancelOrder = "HandOverOrCancelOrder";
}

/// <summary>What a case's details carry: the kind, the next action and the source identifiers it came from.</summary>
public sealed record OnlineOrderCaseDetails(
    string Kind,
    string NextAction,
    string? ExternalOrderId = null,
    Guid? InboxId = null,
    Guid? OrderId = null,
    Guid? OutboxMessageId = null,
    string? Channel = null,
    Guid? ProductId = null,
    string? Reason = null,
    string? Provider = null)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Null when the details are missing or were not written by V12-REC-001.</summary>
    public static OnlineOrderCaseDetails? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            var details = JsonSerializer.Deserialize<OnlineOrderCaseDetails>(json, Options);
            return details is { Kind: not null, NextAction: not null } ? details : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// An online order source pair: an <see cref="IReconciliationSourcePair"/> (V13-REC-001's extension point)
/// that also names the divergence kind it reports, so a case can be checked again against its own source.
/// </summary>
public interface IOnlineOrderSourcePair : IReconciliationSourcePair
{
    string Kind { get; }

    /// <summary>
    /// True when the source cannot show the divergence ending (a cancellation after handover stays in the
    /// source forever): such a case is resolved by a person's recorded decision, not by a re-check.
    /// </summary>
    bool RequiresManualResolution { get; }
}

public enum OnlineOrderRetryOutcome
{
    Requeued,
    NothingToRetry,
    NotRetryable,
    CaseNotActive,
    CaseNotFound,
    NotAnOnlineOrderCase
}

public sealed record OnlineOrderRetryResult(OnlineOrderRetryOutcome Outcome, string? NextAction);

public enum OnlineOrderResolveOutcome
{
    Resolved,
    StillDiverged,
    CaseNotFound,
    NotAnOnlineOrderCase
}

public sealed record OnlineOrderResolveResult(OnlineOrderResolveOutcome Outcome, ReconciliationCaseRecord? Case);
