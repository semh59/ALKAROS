namespace ALKAROS.Orders.SubmitOrder;

using System.Security.Cryptography;
using System.Text;

public static class SubmitOrderRequestHash
{
    /// <summary>
    /// Deliberately excludes <see cref="SubmitOrderCommand.SubmittedAt"/>:
    /// found by an independent audit (2026-09-06, surfaced by V1-RMD-113's
    /// own regression test — the very first caller to actually retry this
    /// handler with a genuine, separately-timestamped second HTTP request)
    /// to make a real client retry indistinguishable from "a different
    /// request reusing the same key". A caller always passes
    /// <c>DateTimeOffset.UtcNow</c> fresh on every call, so a legitimate
    /// retry (the original request's response was lost to a dropped
    /// connection, say) never carries the same wall-clock instant as the
    /// first attempt — hashing it turned every retry into a false
    /// IDEMPOTENCY_KEY_CONFLICT instead of a clean replay. The remaining
    /// fields already uniquely identify "this logical submission".
    /// </summary>
    public static string Compute(SubmitOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var raw = FormattableString.Invariant(
            $"{command.OrderId:D}:{command.ExpectedRowVersion}:{(command.ChangedBy?.ToString("D") ?? string.Empty)}:{command.Reason ?? string.Empty}");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
