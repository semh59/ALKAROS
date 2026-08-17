namespace ALKAROS.Kitchen.PrintQueue;

/// <summary>
/// Lifecycle status of a persistent kitchen print job (PDF:I.16-I.20, PDF:II.5.7-II.5.8).
/// </summary>
public enum PrintJobStatus
{
    /// <summary>
    /// Job is created and waiting to be claimed by a print worker.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Job is claimed/locked with an active lease by a worker node.
    /// </summary>
    Leased = 2,

    /// <summary>
    /// Job payload is currently in flight / transmitting to physical printer.
    /// </summary>
    Printing = 3,

    /// <summary>
    /// Physical printing succeeded and acknowledged (Terminal Success).
    /// </summary>
    Printed = 4,

    /// <summary>
    /// Job failed an attempt, waiting for exponential backoff retry.
    /// </summary>
    Failed = 5,

    /// <summary>
    /// Maximum attempts exceeded, queued for manual operational intervention (Terminal Failure).
    /// </summary>
    DeadLetter = 6,

    /// <summary>
    /// Job cancelled due to ticket cancellation (Terminal Cancelled).
    /// </summary>
    Cancelled = 7
}
