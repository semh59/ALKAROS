namespace ALKAROS.Kitchen.PhysicalPrintRecovery;

/// <summary>
/// Status of physical print delivery and crash-window lifecycle (PDF:I.16-I.20, PDF:II.5.7-II.5.8, V1-KIT-004).
/// </summary>
public enum PhysicalPrintDeliveryStatus
{
    /// <summary>
    /// Bytes are actively being transmitted over physical socket/serial transport to printer.
    /// </summary>
    InFlight = 1,

    /// <summary>
    /// Positive physical ACK / flush confirmation received from printer (Terminal Normal Success).
    /// </summary>
    Printed = 2,

    /// <summary>
    /// Transport broke / socket crashed during send-ack window before ACK received.
    /// Automatic retries are strictly prohibited to prevent duplicate orders in the kitchen.
    /// Requires manual operator confirmation.
    /// </summary>
    Unknown = 3,

    /// <summary>
    /// Operator inspected kitchen / station, confirmed ticket didn't print, and authorized reprint.
    /// </summary>
    ReprintApproved = 4,

    /// <summary>
    /// Operator inspected kitchen / station, confirmed ticket did print, and dismissed reprint.
    /// </summary>
    ReprintRejected = 5,

    /// <summary>
    /// Operator-approved reprint executed with explicit reprint banner (Terminal Reprint Success).
    /// </summary>
    Reprinted = 6
}
