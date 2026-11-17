namespace ALKAROS.Kitchen.PrintQueue.Tests;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Xunit;

/// <summary>The drawer pulse goes to a local TCP listener standing in for the receipt printer; no hardware needed.</summary>
public sealed class CashDrawerKickTests
{
    [Fact]
    public void ThePulseIsEscPAtPinZeroFiftyMillisecondsOnAndFiveHundredOff()
        => EscPosCashDrawer.PulseBytes.ToArray().Should().Equal(0x1B, 0x70, 0x00, 0x19, 0xFA);

    [Fact]
    public async Task KickAsyncDeliversExactlyThePulseAndNothingElse()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = ReadAllAsync(listener);

        var result = await new TcpCashDrawerKicker().KickAsync(IPAddress.Loopback.ToString(), port);

        result.Should().Be(new CashDrawerKickResult(true, null));
        (await received).Should().Equal(0x1B, 0x70, 0x00, 0x19, 0xFA);
    }

    [Fact]
    public async Task KickAsyncReportsARefusedConnectionWithoutThrowing()
    {
        var port = FreePort();

        // Windows retries a refused loopback connection for about two seconds before it reports the refusal.
        var result = await new TcpCashDrawerKicker(TimeSpan.FromSeconds(10)).KickAsync(IPAddress.Loopback.ToString(), port);

        result.Delivered.Should().BeFalse();
        result.Failure.Should().Be("ConnectionRefused");
    }

    [Theory]
    [InlineData("", 9100)]
    [InlineData("192.168.1.50", 70000)]
    public async Task KickAsyncReportsAnInvalidAddressWithoutThrowing(string host, int port)
        => (await new TcpCashDrawerKicker().KickAsync(host, port)).Should().Be(new CashDrawerKickResult(false, "invalid address"));

    [Fact]
    public async Task KickAsyncReportsACancelledCallerWithoutThrowing()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var result = await new TcpCashDrawerKicker().KickAsync(IPAddress.Loopback.ToString(), port, cancelled.Token);

        result.Delivered.Should().BeFalse();
        result.Failure.Should().Contain("cancel");
    }

    [Fact]
    public async Task KickAsyncGivesUpAtItsTimeLimitOnAnAddressThatNeverAnswers()
    {
        var watch = Stopwatch.StartNew();

        // 192.0.2.1 is reserved for documentation (RFC 5737): packets are dropped or refused, never answered.
        var result = await new TcpCashDrawerKicker(TimeSpan.FromMilliseconds(300)).KickAsync("192.0.2.1", 9100);

        result.Delivered.Should().BeFalse();
        result.Failure.Should().NotBe("cancelled", "a time limit is not the caller hanging up");
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void TheDefaultTimeLimitKeepsAPaymentWaitingTwoSecondsAtMost()
        => TcpCashDrawerKicker.DefaultTimeLimit.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(2));

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private static async Task<byte[]> ReadAllAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync();
        using var buffer = new MemoryStream();
        await client.GetStream().CopyToAsync(buffer);
        return buffer.ToArray();
    }
}
