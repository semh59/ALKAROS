namespace ALKAROS.Orders.SubmitOrder;

using System.Security.Cryptography;
using System.Text;

public static class SubmitOrderRequestHash
{
    public static string Compute(SubmitOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var raw = FormattableString.Invariant(
            $"{command.OrderId:D}:{command.ExpectedRowVersion}:{(command.ChangedBy?.ToString("D") ?? string.Empty)}:{(command.SubmittedAt?.ToUniversalTime().ToString("O") ?? string.Empty)}:{command.Reason ?? string.Empty}");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
