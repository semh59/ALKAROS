using System.Security.Cryptography;
using System.Text;
using ALKAROS.OnlineOrdering.Providers.Contracts;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.StatusMapping;

/// <summary>
/// Maps every documented Yemeksepeti order status to one result. The documented statuses
/// (RECEIVED, READY_FOR_PICKUP, DISPATCHED, CANCELLED, DELIVERED) and delivery kinds
/// (VENDOR_DELIVERY: the restaurant delivers and itself reports DISPATCHED; LOGISTICS_DELIVERY:
/// a platform courier collects after the restaurant reports READY_FOR_PICKUP) come from the
/// public Partner API v2.0.2 document. A combination the document does not describe — e.g.
/// READY_FOR_PICKUP for a restaurant-delivered order — is unknown, not guessed. Pure and
/// deterministic: the same signal always yields the same result.
/// </summary>
public static class YemeksepetiStatusMapper
{
    private const int MaxExternalOrderIdLength = 64;
    private const int MaxRawValueLength = 64;
    private const int MaxReasonLength = 200;

    private const string VendorDelivery = "VENDOR_DELIVERY";
    private const string LogisticsDelivery = "LOGISTICS_DELIVERY";

    public static StatusMappingResult Map(YemeksepetiStatusSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        if (string.IsNullOrWhiteSpace(signal.ExternalOrderId))
            throw new InvalidStatusSignalException("ExternalOrderId is required to identify the order a status belongs to.");
        var externalOrderId = signal.ExternalOrderId.Trim();
        if (externalOrderId.Length > MaxExternalOrderIdLength || externalOrderId.Any(char.IsControl))
            throw new InvalidStatusSignalException("ExternalOrderId is not a provider order identifier.");

        var transport = signal.TransportType?.Trim();
        var isVendor = string.Equals(transport, VendorDelivery, StringComparison.Ordinal);
        var isLogistics = string.Equals(transport, LogisticsDelivery, StringComparison.Ordinal);
        if (!isVendor && !isLogistics)
            return Unknown(externalOrderId, signal);

        return signal.Status?.Trim() switch
        {
            "RECEIVED" => Command(InternalOrderCommand.AcceptIncomingOrder, null),
            "CANCELLED" => Command(InternalOrderCommand.CancelOrder, Cancellation(signal.Cancellation)),
            "READY_FOR_PICKUP" when isLogistics => NoOp(StatusNoOpReason.OwnOutboundStatusEcho),
            "DISPATCHED" when isVendor => NoOp(StatusNoOpReason.OwnOutboundStatusEcho),
            "DISPATCHED" when isLogistics => NoOp(StatusNoOpReason.HandedToCourier),
            "DELIVERED" when isLogistics => NoOp(StatusNoOpReason.DeliveredByPlatform),
            _ => Unknown(externalOrderId, signal)
        };
    }

    private static StatusMappingResult Command(InternalOrderCommand command, CancellationDetail? cancellation) =>
        new(StatusMappingKind.Command, command, cancellation, null, null);

    private static StatusMappingResult NoOp(StatusNoOpReason reason) =>
        new(StatusMappingKind.NoOp, null, null, reason, null);

    private static CancellationDetail Cancellation(YemeksepetiCancellationSignal? cancellation)
    {
        var rawParty = Clip(cancellation?.CancelledBy, MaxRawValueLength);
        var party = rawParty?.ToUpperInvariant() switch
        {
            "CUSTOMER" => CancellationParty.Customer,
            "VENDOR" => CancellationParty.Vendor,
            "LOGISTICS" => CancellationParty.Logistics,
            _ => CancellationParty.Unrecognized
        };
        return new CancellationDetail(party, rawParty, Clip(cancellation?.Reason, MaxReasonLength), cancellation?.PostPickedUp ?? false);
    }

    private static StatusMappingResult Unknown(string externalOrderId, YemeksepetiStatusSignal signal)
    {
        var rawStatus = Clip(signal.Status, MaxRawValueLength);
        var rawTransport = Clip(signal.TransportType, MaxRawValueLength);
        var identity = string.Join(
            '\u001f', YemeksepetiStatusVocabulary.Version, externalOrderId, rawStatus ?? string.Empty, rawTransport ?? string.Empty);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        var evidence = new UnknownStatusEvidence(
            new Guid(hash.AsSpan(0, 16)), YemeksepetiStatusVocabulary.Version, externalOrderId, rawStatus, rawTransport);
        return new StatusMappingResult(StatusMappingKind.Unknown, null, null, null, evidence);
    }

    private static string? Clip(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var cleaned = new string(value.Trim().Where(c => !char.IsControl(c)).ToArray());
        if (cleaned.Length == 0)
            return null;
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
