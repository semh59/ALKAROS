namespace ALKAROS.Kitchen.PhysicalPrintRecovery;

using System.Globalization;
using System.Text;

/// <summary>
/// Enriches kitchen ticket payloads with prominent duplicate-risk reprint warning banners.
/// </summary>
public static class ReprintTicketBannerFormatter
{
    private const int LineWidth = 42;

    public static string WrapWithReprintBanner(
        string originalPayload,
        string operatorId,
        string reason,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(originalPayload);
        if (string.IsNullOrWhiteSpace(operatorId))
            throw new ArgumentException("Operator ID cannot be empty.", nameof(operatorId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reprint reason cannot be empty.", nameof(reason));

        var sb = new StringBuilder();
        sb.AppendLine(new string('#', LineWidth));
        sb.AppendLine(CenterText("*** TEKRAR BASKI / REPRINT ***", LineWidth));
        sb.AppendLine(CenterText("*** MUKERRER RISKLI KOPYA ***", LineWidth));
        sb.AppendLine(new string('#', LineWidth));
        sb.AppendLine(CultureInfo.InvariantCulture, $"ONAYLAYAN: {operatorId}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"NEDEN: {reason}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"TEKRAR TARIHI: {now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('-', LineWidth));
        sb.AppendLine(originalPayload);
        sb.AppendLine(new string('#', LineWidth));
        sb.AppendLine(CenterText("*** TEKRAR BASKI SONU ***", LineWidth));
        sb.AppendLine(new string('#', LineWidth));

        return sb.ToString();
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
