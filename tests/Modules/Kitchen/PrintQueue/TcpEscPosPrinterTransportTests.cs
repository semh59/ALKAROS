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
        expected.AddRange(new byte[] { 0x1B, 0x74, 13 });
        expected.AddRange(Encoding.ASCII.GetBytes("MUTFAK SIPARIS FISI\r\n"));
        expected.AddRange(EscPosTicketFormatter.CutPaperWithFeed);
        expected.AddRange(new byte[] { 0x10, 0x04, 0x01 }); // the status question; a silent printer is not an error

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

    private const string Turkish = "ÇçĞğİıÖöŞşÜü";
    private static readonly byte[] TurkishInCodePage857 = [0x80, 0x87, 0xA6, 0xA7, 0x98, 0x8D, 0x99, 0x94, 0x9E, 0x9F, 0x9A, 0x81];

    [Theory]
    [InlineData(PrinterProfile.Epson, new byte[] { 0x1B, 0x74, 13 })]
    [InlineData(PrinterProfile.Bixolon, new byte[] { 0x1B, 0x74, 37 })]
    [InlineData(PrinterProfile.Star, new byte[] { 0x1B, 0x1D, 0x74, 12 })]
    public async Task EachProfileSelectsItsOwnTurkishTableAndSendsCodePage857(PrinterProfile profile, byte[] selectTable)
    {
        var received = await SendToFakePrinterAsync(profile, Turkish, new Dictionary<byte, byte>());

        received.Take(2 + selectTable.Length).Should().Equal([0x1B, 0x40, .. selectTable]);
        received.Skip(2 + selectTable.Length).Take(TurkishInCodePage857.Length).Should().Equal(TurkishInCodePage857);
    }

    [Fact]
    public async Task TheAsciiProfileSelectsNoTableAndFoldsTurkishLetters()
    {
        var received = await SendToFakePrinterAsync(PrinterProfile.Ascii, Turkish, new Dictionary<byte, byte>());

        received.Skip(2).Take(Turkish.Length).Should().Equal(Encoding.ASCII.GetBytes("CcGgIiOoSsUu"));
    }

    [Fact]
    public async Task AnOnlinePrinterIsNotAnError()
    {
        var act = () => SendToFakePrinterAsync(PrinterProfile.Epson, "x", new Dictionary<byte, byte> { [1] = 0x16 });

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(0x32, "out of paper")]
    [InlineData(0x16, "cover open")]
    [InlineData(0x52, "in an error state")]
    [InlineData(0x12, "offline")]
    public async Task AnOfflinePrinterIsReportedWithItsCauseAsAnUnconfirmedDelivery(int cause, string reason)
    {
        var act = () => SendToFakePrinterAsync(PrinterProfile.Epson, "x", new Dictionary<byte, byte> { [1] = 0x1E, [2] = (byte)cause });

        await act.Should().ThrowAsync<PrinterTransmissionUncertainException>().WithMessage($"*reports it is {reason}*");
    }

    /// <summary>A printer stand-in: collects what it receives and answers each <c>DLE EOT n</c> with the scripted status byte.</summary>
    private static async Task<byte[]> SendToFakePrinterAsync(PrinterProfile profile, string payload, Dictionary<byte, byte> statusAnswers)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var printer = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptTcpClientAsync();
            await using var stream = accepted.GetStream();
            var received = new List<byte>();
            var chunk = new byte[256];
            int read;
            while ((read = await stream.ReadAsync(chunk)) > 0)
            {
                for (var i = 0; i < read; i++)
                {
                    received.Add(chunk[i]);
                    var count = received.Count;
                    if (count >= 3 && received[count - 3] == 0x10 && received[count - 2] == 0x04 && statusAnswers.TryGetValue(received[count - 1], out var answer))
                        await stream.WriteAsync(new[] { answer });
                }
            }

            return received.ToArray();
        });

        try
        {
            await new TcpEscPosPrinterTransport(profile).SendAsync(IPAddress.Loopback.ToString(), port, payload);
        }
        finally
        {
            listener.Stop();
        }

        return await printer;
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
