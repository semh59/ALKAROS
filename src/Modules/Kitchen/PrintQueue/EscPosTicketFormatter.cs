namespace ALKAROS.Kitchen.PrintQueue;

using System.Globalization;
using System.Text;
using ALKAROS.Kitchen.TicketLifecycle;

/// <summary>
/// Formats a KitchenTicket into standard 80mm (42/48-column) ESC/POS thermal printer commands and printable layout.
/// </summary>
public static class EscPosTicketFormatter
{
    // ESC/POS Command Constants
    public static readonly byte[] InitializePrinter = [0x1B, 0x40];           // ESC @
    public static readonly byte[] AlignCenter = [0x1B, 0x61, 0x01];             // ESC a 1
    public static readonly byte[] AlignLeft = [0x1B, 0x61, 0x00];               // ESC a 0
    public static readonly byte[] AlignRight = [0x1B, 0x61, 0x02];              // ESC a 2
    public static readonly byte[] DoubleHeightWidth = [0x1D, 0x21, 0x11];       // GS ! 0x11 (2x H, 2x W)
    public static readonly byte[] NormalText = [0x1D, 0x21, 0x00];              // GS ! 0x00
    public static readonly byte[] BoldOn = [0x1B, 0x45, 0x01];                  // ESC E 1
    public static readonly byte[] BoldOff = [0x1B, 0x45, 0x00];                 // ESC E 0
    public static readonly byte[] CutPaperWithFeed = [0x1D, 0x56, 0x42, 0x03];   // GS V 'B' 3

    private const int LineWidth = 42;

    public static string FormatToPrintableText(
        KitchenTicket ticket,
        string? tableNumber = null,
        string? orderNumber = null,
        DateTimeOffset? printTimestamp = null)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var ts = printTimestamp ?? DateTimeOffset.UtcNow;
        var sb = new StringBuilder();

        sb.AppendLine(new string('=', LineWidth));
        sb.AppendLine(CenterText($"MUTFAK SIPARIS FISI - {ticket.StationId.ToUpperInvariant()}", LineWidth));
        sb.AppendLine(new string('=', LineWidth));

        if (!string.IsNullOrWhiteSpace(tableNumber))
        {
            var tablePart = string.Create(CultureInfo.InvariantCulture, $"MASA: {tableNumber}").PadRight(LineWidth / 2);
            var ticketPart = string.Create(CultureInfo.InvariantCulture, $"FIS NO: {ticket.TicketNumber}").PadLeft(LineWidth / 2);
            sb.AppendLine(tablePart + ticketPart);
        }
        else
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"FIS NO: {ticket.TicketNumber}");
        }

        if (!string.IsNullOrWhiteSpace(orderNumber))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"SIPARIS: {orderNumber}");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"TARIH: {ts:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('-', LineWidth));

        sb.AppendLine("ADET  URUN / ACIKLAMA");
        sb.AppendLine(new string('-', LineWidth));

        foreach (var item in ticket.Items)
        {
            var quantityStr = item.Quantity.ToString("0.##", CultureInfo.InvariantCulture).PadRight(5);
            sb.AppendLine(CultureInfo.InvariantCulture, $"{quantityStr} {item.ProductNameSnapshot}");

            if (!string.IsNullOrWhiteSpace(item.ModifiersSummary))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"      + {item.ModifiersSummary}");
            }

            if (!string.IsNullOrWhiteSpace(item.Notes))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"      NOT: {item.Notes}");
            }
        }

        sb.AppendLine(new string('-', LineWidth));
        sb.AppendLine(CenterText($"*** {ticket.Status.ToString().ToUpperInvariant()} ***", LineWidth));
        sb.AppendLine(new string('=', LineWidth));
        sb.AppendLine();

        return sb.ToString();
    }

    public static byte[] FormatToEscPosBytes(
        KitchenTicket ticket,
        string? tableNumber = null,
        string? orderNumber = null,
        DateTimeOffset? printTimestamp = null)
    {
        var text = FormatToPrintableText(ticket, tableNumber, orderNumber, printTimestamp);
        var textBytes = Encoding.UTF8.GetBytes(text);

        using var ms = new MemoryStream();
        ms.Write(InitializePrinter);
        ms.Write(textBytes);
        ms.Write(CutPaperWithFeed);

        return ms.ToArray();
    }

    private static string CenterText(string text, int width)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (text.Length >= width)
            return text;

        var totalPadding = width - text.Length;
        var padLeft = totalPadding / 2;
        return text.PadLeft(text.Length + padLeft).PadRight(width);
    }
}
