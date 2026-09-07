namespace ALKAROS.QrOrdering.RelaySecurity;

/// <summary>
/// V12-QRS-002: the rate-limit and payload-size bounds a relay-facing
/// endpoint must enforce. Framework-agnostic constants — deliberately not
/// an ASP.NET Core rate limiter policy or endpoint filter: wiring these
/// into a concrete route is the job of whichever Host endpoint task
/// actually exposes one (`V12-QRO-001`), which is out of this task's
/// declared Owned surface. Defining the numbers once here means every
/// future relay-facing endpoint applies the same bounds instead of each
/// inventing its own.
/// </summary>
public static class RelayAbusePolicy
{
    /// <summary>A QR order's item list has no legitimate reason to exceed this.</summary>
    public const long MaxPayloadBytes = 8 * 1024;

    /// <summary>Requests a single table token may make within <see cref="Window"/>.</summary>
    public const int PerTokenRequestLimit = 30;

    /// <summary>Requests a single source IP may make within <see cref="Window"/>, regardless of which token(s) it presents.</summary>
    public const int PerIpRequestLimit = 60;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
}
