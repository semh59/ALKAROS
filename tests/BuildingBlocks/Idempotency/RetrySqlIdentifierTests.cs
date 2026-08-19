using ALKAROS.Messaging;
using Npgsql;
using Xunit;

namespace ALKAROS.Idempotency.Tests;

/// <summary>
/// Verifies the retry SQL surface is closed to registered constant table
/// identifiers: only <c>inbox_messages</c> and <c>outbox_messages</c> pass
/// the guard, every other value fails closed with an
/// <see cref="ArgumentException"/> before any command is built.
/// </summary>
public sealed class RetrySqlIdentifierTests
{
    private static readonly string[] ExpectedIdentifiers =
        ["inbox_messages", "outbox_messages"];

    [Fact]
    public void AllowedTableNamesContainExactlyTheRegisteredIdentifiers()
    {
        Assert.Equal(
            ExpectedIdentifiers,
            RetryPolicy.AllowedTableNames.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("inbox_messages")]
    [InlineData("outbox_messages")]
    public async Task RegisteredIdentifiersPassTheGuardAndReachCommandExecution(string tableName)
    {
        using var connection = new NpgsqlConnection(
            "Host=localhost;Port=5432;Username=postgres;Database=postgres");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RetryPolicy.RecordFailureAsync(
                connection,
                tableName,
                Guid.NewGuid(),
                1,
                "boom",
                TimeSpan.FromSeconds(1)));
    }

    [Theory]
    [InlineData("user_payload")]
    [InlineData("inbox_messages; DROP TABLE inbox_messages;--")]
    [InlineData("OUTBOX_MESSAGES")]
    [InlineData("inbox_messages ")]
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
