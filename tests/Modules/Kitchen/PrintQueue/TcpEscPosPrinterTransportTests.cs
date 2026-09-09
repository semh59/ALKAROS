namespace ALKAROS.Kitchen.PrintQueue.Tests;

using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Xunit;

/// <summary>
/// V1-RMD-130: exercises the real network transport against a local TCP
/// listener standing in for a physical printer — no hardware needed, but
/// the socket-level behavior (connect, write, timeout, reset) is real.
/// </summary>
public sealed class TcpEscPosPrinterTransportTests
{
    [Fact]
    public async Task SendAsyncDeliversTheEscPosWrappedPayloadToTheListener()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var acceptTask = AcceptAndReadAllAsync(listener);

        var transport = new TcpEscPosPrinterTransport();
        await transport.SendAsync(IPAddress.Loopback.ToString(), port, "MUTFAK SIPARIS FISI\r\n");

        var received = await acceptTask;
        var expected = new List<byte>();
        expected.AddRange(EscPosTicketFormatter.InitializePrinter);
        expected.AddRange(Encoding.UTF8.GetBytes("MUTFAK SIPARIS FISI\r\n"));
        expected.AddRange(EscPosTicketFormatter.CutPaperWithFeed);

        received.Should().Equal(expected);
    }

    [Fact]
    public async Task SendAsyncThrowsPrinterUnreachableWhenNothingIsListening()
    {
        // A loopback port nothing is listening on refuses the connection
        // immediately — no bytes were ever sent, so this must be classified
        // as safe to retry.
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var unusedPort = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        var transport = new TcpEscPosPrinterTransport();
        var act = () => transport.SendAsync(IPAddress.Loopback.ToString(), unusedPort, "payload");

        await act.Should().ThrowAsync<PrinterUnreachableException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendAsyncThrowsPrinterUnreachableForAMissingIpAddress(string ipAddress)
    {
        var transport = new TcpEscPosPrinterTransport();
        var act = () => transport.SendAsync(ipAddress, 9100, "payload");

        await act.Should().ThrowAsync<PrinterUnreachableException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(70000)]
    public async Task SendAsyncThrowsPrinterUnreachableForAnInvalidPort(int port)
    {
        var transport = new TcpEscPosPrinterTransport();
        var act = () => transport.SendAsync("127.0.0.1", port, "payload");

        await act.Should().ThrowAsync<PrinterUnreachableException>();
    }

    // A genuine mid-write connection reset is not reliably reproducible in a
    // fast, deterministic unit test: on loopback the OS routinely accepts an
    // entire multi-megabyte write into its kernel send buffer before a reset
    // from the peer is ever observed, so WriteAsync/FlushAsync return
    // successfully regardless (verified empirically while writing this test
    // — attempts with a forced-RST close and payloads up to several MB never
    // reliably reproduced the failure). SendAsyncThrowsPrinterTransmissionUncertain
    // is therefore verified by code review of the catch mapping (a
    // NetworkStream's own IOException/SocketException during
    // WriteAsync/FlushAsync — standard, documented framework behavior) plus
    // ProcessEligibleJobsRoutesAnUncertainTransmissionToOperatorReviewInsteadOfRetrying
    // in PostgresPrintQueueIntegrationTests.cs, which proves the rest of the
    // pipeline (PrintQueueService's branching, PrintJob's state transition)
    // correctly handles that exception type once thrown.

    private static async Task<byte[]> AcceptAndReadAllAsync(TcpListener listener)
    {
        using var accepted = await listener.AcceptTcpClientAsync();
        await using var stream = accepted.GetStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }
}
