namespace ALKAROS.Kitchen.PrintQueue;

/// <summary>
/// Sends a formatted print payload to a physical printer over the network.
/// V1-RMD-130: the real "last mile" of the print queue — everything upstream
/// (routing, the persistent lease-based queue, the crash-window-safe
/// PhysicalPrintDelivery model) already existed; nothing ever implemented
/// this interface for real hardware.
///
/// The two failure exceptions below encode a safety distinction the rest of
/// the pipeline depends on: a connection that never opened can be retried
/// automatically (nothing physical happened), but a connection that opened
/// and then failed mid-transmission is ambiguous — the printer may or may
/// not have produced a partial or full ticket — and automatic retry there
/// risks a duplicate physical print in the kitchen. Callers must route
/// <see cref="PrinterUnreachableException"/> through the print queue's own
/// backoff/retry, and <see cref="PrinterTransmissionUncertainException"/>
/// through <c>IPhysicalPrintRecoveryService</c>'s operator-approval flow
/// instead.
/// </summary>
public interface IPrinterTransport
{
    /// <summary>
    /// Sends <paramref name="payload"/> (plain formatted ticket text — ESC/POS
    /// control bytes are added by the transport itself) to the printer at
    /// <paramref name="ipAddress"/>:<paramref name="port"/>.
    /// </summary>
    Task SendAsync(string ipAddress, int port, string payload, CancellationToken ct = default);
}

/// <summary>
/// The printer was never reached — connection refused, DNS failure, or the
/// connection attempt itself timed out. No bytes were ever written, so
/// nothing physical could have happened: safe for the caller to retry
/// through the print queue's normal exponential backoff.
/// </summary>
public sealed class PrinterUnreachableException : Exception
{
    public PrinterUnreachableException(string message) : base(message) { }
    public PrinterUnreachableException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// A connection was established and at least some bytes may have been sent
/// before the transport failed (write error, write timeout, or the
/// connection was reset mid-transmission). Whether the printer actually
/// produced a ticket is now unknown — the caller must NOT auto-retry; this
/// must be routed to <c>IPhysicalPrintRecoveryService.ReportCrashWindowUncertaintyAsync</c>
/// so a human operator resolves it (PDF:I.16-I.20, V1-KIT-004).
/// </summary>
public sealed class PrinterTransmissionUncertainException : Exception
{
    public PrinterTransmissionUncertainException(string message) : base(message) { }
    public PrinterTransmissionUncertainException(string message, Exception innerException) : base(message, innerException) { }
}
