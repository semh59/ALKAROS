using ALKAROS.Messaging;
using Npgsql;
using Xunit;

namespace ALKAROS.Messaging.Tests;

/// <summary>
/// Verifies the retry SQL surface is closed to registered constant table
/// identifiers: only <c>outbox_messages</c> passes the guard, every other
/// value fails closed with an <see cref="ArgumentException"/> before any
/// command is built. V1-RMD-134: "inbox_messages" was dropped from the
/// registry along with the rest of the dead Inbox pattern (Messaging's own
/// RetryPolicy.AllowedTableNames doc comment has the full story).
/// </summary>
public sealed class RetrySqlIdentifierTests
{
    private static readonly string[] ExpectedIdentifiers = ["outbox_messages"];

    [Fact]
    public void AllowedTableNamesContainExactlyTheRegisteredIdentifiers()
    {
        Assert.Equal(
            ExpectedIdentifiers,
            RetryPolicy.AllowedTableNames.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task TheRegisteredIdentifierPassesTheGuardAndReachesCommandExecution()
    {
        using var connection = new NpgsqlConnection(
            "Host=localhost;Port=5432;Username=postgres;Database=postgres");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RetryPolicy.RecordFailureAsync(
                connection,
                "outbox_messages",
                Guid.NewGuid(),
                1,
                "boom",
                TimeSpan.FromSeconds(1)));
    }

    [Theory]
    [InlineData("user_payload")]
    [InlineData("inbox_messages")]
    [InlineData("outbox_messages; DROP TABLE outbox_messages;--")]
    [InlineData("OUTBOX_MESSAGES")]
    [InlineData("outbox_messages ")]
    public async Task UnregisteredIdentifiersFailClosedBeforeAnyCommand(string tableName)
    {
        using var connection = new NpgsqlConnection(
            "Host=localhost;Port=5432;Username=postgres;Database=postgres");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            RetryPolicy.RecordFailureAsync(
                connection,
                tableName,
                Guid.NewGuid(),
                1,
                "boom",
                TimeSpan.FromSeconds(1)));
    }
}
