namespace ALKAROS.Kitchen.PrintQueue;

using System.Text;

public enum PrinterSimulatedState
{
    Online = 1,
    PaperOut = 2,
    Offline = 3,
    BufferFull = 4
}

public sealed record SimulatedPrinterResult(
    bool Success,
    string? ErrorMessage,
    int BytesReceived,
    DateTimeOffset Timestamp)
{
    public static SimulatedPrinterResult Succeeded(int bytes) =>
        new(true, null, bytes, DateTimeOffset.UtcNow);

    public static SimulatedPrinterResult Failed(string error) =>
        new(false, error, 0, DateTimeOffset.UtcNow);
}

/// <summary>
/// Simulates a standard 80mm thermal ESC/POS kitchen printer (Epson TM-T88 / Star / Bixolon)
/// for local execution, testing, and hardware-independent verification.
/// </summary>
public sealed class KitchenPrinterSimulator
{
    private readonly List<PrintJob> _printedHistory = [];
    private readonly List<string> _printedPayloads = [];

    public KitchenPrinterSimulator(Guid printerId, string name, PrinterSimulatedState initialState = PrinterSimulatedState.Online)
    {
        PrinterId = printerId;
        Name = name;
        State = initialState;
    }

    public Guid PrinterId { get; }
    public string Name { get; }
    public PrinterSimulatedState State { get; set; }
    public IReadOnlyList<PrintJob> PrintedHistory => _printedHistory.AsReadOnly();
    public IReadOnlyList<string> PrintedPayloads => _printedPayloads.AsReadOnly();

    public Task<SimulatedPrinterResult> PrintAsync(PrintJob job, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        return State switch
        {
            PrinterSimulatedState.PaperOut =>
                Task.FromResult(SimulatedPrinterResult.Failed("PRINTER_PAPER_OUT: Kagit bitti (Paper out sensor triggered).")),

            PrinterSimulatedState.Offline =>
                Task.FromResult(SimulatedPrinterResult.Failed("PRINTER_OFFLINE: Yazici ag baglantisi zaman asimi (Socket timeout / Host unreachable).")),

            PrinterSimulatedState.BufferFull =>
                Task.FromResult(SimulatedPrinterResult.Failed("PRINTER_BUFFER_FULL: Yazici arabellek dolu (Buffer overflow).")),

            PrinterSimulatedState.Online =>
                ExecuteOnlinePrint(job),

            _ => Task.FromResult(SimulatedPrinterResult.Failed($"Unknown printer state: {State}"))
        };
    }

    private Task<SimulatedPrinterResult> ExecuteOnlinePrint(PrintJob job)
    {
        var byteCount = Encoding.UTF8.GetByteCount(job.Payload);
        _printedHistory.Add(job);
        _printedPayloads.Add(job.Payload);

        return Task.FromResult(SimulatedPrinterResult.Succeeded(byteCount));
    }

    public void ClearHistory()
    {
        _printedHistory.Clear();
        _printedPayloads.Clear();
    }
}
