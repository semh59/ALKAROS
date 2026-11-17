namespace ALKAROS.Kitchen.PrintQueue;

using System.Net.Sockets;

/// <summary>
/// The cash drawer is cabled to the receipt printer and opens on a pulse command. It is sent on its own, never as part
/// of a kitchen ticket: a ticket must not open the drawer.
/// </summary>
public static class EscPosCashDrawer
{
    /// <summary>ESC p m t1 t2: pin 0, 50 ms on (t1 = 25 x 2 ms), 500 ms off (t2 = 250 x 2 ms).</summary>
    public static ReadOnlySpan<byte> PulseBytes => [0x1B, 0x70, 0x00, 0x19, 0xFA];
}

/// <summary>A failed send is a value, not an exception: the caller records it and the payment or request goes on.</summary>
public readonly record struct CashDrawerKickResult(bool Delivered, string? Failure);

public interface ICashDrawerKicker
{
    Task<CashDrawerKickResult> KickAsync(string host, int port, CancellationToken cancellationToken = default);
}

/// <summary>
/// One attempt over a raw TCP printer port, bounded by a time limit. No status query and no retry: opening the
/// drawer must never hold up a payment.
/// </summary>
public sealed class TcpCashDrawerKicker : ICashDrawerKicker
{
    public static readonly TimeSpan DefaultTimeLimit = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _timeLimit;

    public TcpCashDrawerKicker() : this(DefaultTimeLimit)
    {
    }

    public TcpCashDrawerKicker(TimeSpan timeLimit) => _timeLimit = timeLimit;

    public async Task<CashDrawerKickResult> KickAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(_timeLimit);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, limit.Token);
            var stream = client.GetStream();
            await stream.WriteAsync(EscPosCashDrawer.PulseBytes.ToArray(), limit.Token);
            await stream.FlushAsync(limit.Token);
            return new CashDrawerKickResult(true, null);
        }
        catch (OperationCanceledException)
        {
            return new CashDrawerKickResult(false, cancellationToken.IsCancellationRequested ? "cancelled" : "timed out");
        }
        catch (ArgumentException)
        {
            return new CashDrawerKickResult(false, "invalid address");
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            return new CashDrawerKickResult(false, exception is SocketException socket ? socket.SocketErrorCode.ToString() : "write failed");
        }
    }
}
