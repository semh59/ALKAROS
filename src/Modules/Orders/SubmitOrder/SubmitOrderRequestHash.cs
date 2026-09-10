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
    ///
    /// V1-RMD-155 excludes <see cref="SubmitOrderCommand.ExpectedRowVersion"/>
    /// for exactly the same reason. A retry after a lost response re-reads the
    /// order first, and by then the row version has moved — the draft replay
    /// hands back the *current* one — so every genuine retry produced a
    /// different hash and was rejected as a conflicting reuse. The waiter's
    /// client files a 4xx as permanently failed, so the round was moved to the
    /// failed list and re-keyed by hand, and the kitchen cooked it twice.
    ///
    /// Nothing is weakened by dropping it: the row version is an optimistic
    /// concurrency token, still enforced where it belongs, in SaveAsync's own
    /// <c>WHERE row_version = @expected_row_version</c>. It answers "is the
    /// order still as you last saw it", not "which request is this".
    /// </summary>
    public static string Compute(SubmitOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var raw = FormattableString.Invariant(
            $"{command.OrderId:D}:{(command.ChangedBy?.ToString("D") ?? string.Empty)}:{command.Reason ?? string.Empty}");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
