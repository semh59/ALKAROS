namespace ALKAROS.Kitchen.PrintQueue;

using System.Net.Sockets;
using System.Text;

/// <summary>
/// V1-RMD-130: real network transport for standard 80mm thermal ESC/POS
/// kitchen printers (Epson TM-T88 / Star / Bixolon), which accept raw
/// ESC/POS bytes on a plain TCP socket (conventionally port 9100 — "raw"
/// / JetDirect-style printing, the universal mode every such printer
/// supports regardless of vendor driver).
/// </summary>
public sealed class TcpEscPosPrinterTransport : IPrinterTransport
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StatusTimeout = TimeSpan.FromMilliseconds(500);

    private readonly PrinterProfile _profile;

    public TcpEscPosPrinterTransport() : this(EscPosEncoding.FromEnvironment())
    {
    }

    public TcpEscPosPrinterTransport(PrinterProfile profile) => _profile = profile;

    public async Task SendAsync(string ipAddress, int port, string payload, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            throw new PrinterUnreachableException("Printer has no IP address configured.");
        if (port is < 1 or > 65535)
            throw new PrinterUnreachableException($"Printer has an invalid port configured ({port}).");
        ArgumentNullException.ThrowIfNull(payload);

        using var client = new TcpClient();

        // Nothing has been sent yet at this stage — any failure here (refused,
        // unreachable, DNS, or our own timeout) means the printer was never
        // actually reached, so it is always safe to retry.
        using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            connectCts.CancelAfter(ConnectTimeout);
            try
            {
                await client.ConnectAsync(ipAddress, port, connectCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new PrinterUnreachableException(
                    $"Connecting to printer {ipAddress}:{port} timed out after {ConnectTimeout.TotalSeconds:0}s.");
            }
            catch (SocketException ex)
            {
                throw new PrinterUnreachableException($"Could not connect to printer {ipAddress}:{port}: {ex.Message}", ex);
            }
        }

        // Once the socket is open, a failure here is ambiguous — the printer
        // may have received and acted on some or all of the bytes before the
        // failure. This must not be auto-retried (see IPrinterTransport's own
        // doc comment).
        var bytes = BuildEscPosPayload(payload, _profile);
        using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        writeCts.CancelAfter(WriteTimeout);
        try
        {
            var stream = client.GetStream();
            await stream.WriteAsync(bytes, writeCts.Token).ConfigureAwait(false);
            await stream.FlushAsync(writeCts.Token).ConfigureAwait(false);
            await ThrowIfPrinterReportsAProblemAsync(stream, ipAddress, port, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new PrinterTransmissionUncertainException(
                $"Writing to printer {ipAddress}:{port} timed out after {WriteTimeout.TotalSeconds:0}s; delivery is unconfirmed.");
        }
        catch (IOException ex)
        {
            throw new PrinterTransmissionUncertainException(
                $"Connection to printer {ipAddress}:{port} was lost while writing; delivery is unconfirmed: {ex.Message}", ex);
        }
        catch (SocketException ex)
        {
            throw new PrinterTransmissionUncertainException(
                $"Connection to printer {ipAddress}:{port} failed while writing; delivery is unconfirmed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Wraps already-formatted ticket text (<see cref="EscPosTicketFormatter.FormatToPrintableText"/>,
    /// the same text stored as the print job's own payload for admin
    /// visibility) with the printer init/cut control codes — the same
    /// wrapping <see cref="EscPosTicketFormatter.FormatToEscPosBytes"/> applies,
    /// just operating on the stored text directly instead of re-formatting
    /// from the ticket.
    /// </summary>
    private static byte[] BuildEscPosPayload(string payload, PrinterProfile profile)
    {
        var textBytes = EscPosEncoding.Encode(payload, profile);
        using var ms = new MemoryStream(textBytes.Length + 16);
        ms.Write(EscPosTicketFormatter.InitializePrinter);
        ms.Write(EscPosEncoding.SelectTable(profile));
        ms.Write(textBytes);
        ms.Write(EscPosTicketFormatter.CutPaperWithFeed);
        return ms.ToArray();
    }

    /// <summary>
    /// Asks the printer for its real-time status after the data went out (DLE EOT 1: bit 3 = offline; DLE EOT 2: bit 2 =
    /// cover open, bit 5 = paper end, bit 6 = error). A printer that does not answer is treated as before: nothing is known.
    /// </summary>
    private static async Task ThrowIfPrinterReportsAProblemAsync(NetworkStream stream, string ipAddress, int port, CancellationToken ct)
    {
        if (await QueryStatusAsync(stream, 1, ct).ConfigureAwait(false) is not { } printer || (printer & 0x08) == 0)
            return;

        var cause = await QueryStatusAsync(stream, 2, ct).ConfigureAwait(false) ?? 0;
        var reason = (cause & 0x20) != 0 ? "out of paper"
            : (cause & 0x04) != 0 ? "cover open"
            : (cause & 0x40) != 0 ? "in an error state"
            : "offline";
        throw new PrinterTransmissionUncertainException(
            $"Printer {ipAddress}:{port} reports it is {reason} after the ticket was sent; the ticket may print later, delivery is unconfirmed.");
    }

    private static async Task<byte?> QueryStatusAsync(NetworkStream stream, byte kind, CancellationToken ct)
    {
        using var statusCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        statusCts.CancelAfter(StatusTimeout);
        try
        {
            await stream.WriteAsync(new byte[] { 0x10, 0x04, kind }, statusCts.Token).ConfigureAwait(false);
            var answer = new byte[1];
            return await stream.ReadAsync(answer, statusCts.Token).ConfigureAwait(false) == 1 ? answer[0] : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null; // a printer without status support stays silent
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            return null; // the data already went out; a failing status read does not undo that
        }
    }
}
