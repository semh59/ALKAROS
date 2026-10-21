using ALKAROS.Settings.BusinessIdentity.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Settings.BusinessIdentity.Tests;

/// <summary>
/// One fresh isolated database per test — same reasoning as
/// BusinessNameSettingTests: the single `id = 1` row would otherwise make
/// tests order-dependent.
/// </summary>
public sealed class BusinessLogoStoreTests : IAsyncLifetime
{
    private readonly BusinessIdentityTestDatabase _db = new();
    private BusinessLogoStore _store = null!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _store = new BusinessLogoStore(_db.DataSource);
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task GetReturnsNullWhenNoLogoHasEverBeenSet()
        => (await _store.GetAsync(CancellationToken.None)).Should().BeNull();

    [Fact]
    public async Task SaveThenGetRoundTripsTheExactBytesAndContentType()
    {
        byte[] content = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

        await _store.SaveAsync(content, "image/png", CancellationToken.None);
        var logo = await _store.GetAsync(CancellationToken.None);

        logo.Should().NotBeNull();
        logo!.Content.Should().Equal(content);
        logo.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task SavingASecondTimeReplacesTheSingleRowRatherThanAddingASecondOne()
    {
        await _store.SaveAsync([1, 2, 3], "image/png", CancellationToken.None);
        await _store.SaveAsync([4, 5, 6], "image/webp", CancellationToken.None);

        var logo = await _store.GetAsync(CancellationToken.None);

        logo!.Content.Should().Equal([4, 5, 6]);
        logo.ContentType.Should().Be("image/webp");
    }

    [Fact]
    public async Task ETagChangesWhenTheLogoIsReplaced()
    {
        await _store.SaveAsync([1, 2, 3], "image/png", CancellationToken.None);
        var first = await _store.GetAsync(CancellationToken.None);

        await Task.Delay(TimeSpan.FromMilliseconds(5));
        await _store.SaveAsync([4, 5, 6], "image/png", CancellationToken.None);
        var second = await _store.GetAsync(CancellationToken.None);

        second!.ETag.Should().NotBe(first!.ETag);
    }

    [Fact]
    public async Task DeleteRemovesTheLogoSoGetReturnsNullAgain()
    {
        await _store.SaveAsync([1, 2, 3], "image/png", CancellationToken.None);

        await _store.DeleteAsync(CancellationToken.None);

        (await _store.GetAsync(CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task MetaIsNullWithoutALogoAndMatchesTheFullReadOtherwise()
    {
        (await _store.GetMetaAsync(CancellationToken.None)).Should().BeNull();

        await _store.SaveAsync([1, 2, 3], "image/webp", CancellationToken.None);
        var meta = await _store.GetMetaAsync(CancellationToken.None);
        var full = await _store.GetAsync(CancellationToken.None);

        meta!.ContentType.Should().Be("image/webp");
        meta.ETag.Should().Be(full!.ETag);
        meta.UpdatedAt.Should().Be(full.UpdatedAt);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("IMAGE/PNG")]
    [InlineData("text/html")]
    public async Task TheDatabaseRefusesAContentTypeOutsideTheThreeAllowedImageTypes(string contentType)
    {
        var act = () => InsertRawAsync(new byte[] { 1 }, contentType);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23514");
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public async Task TheDatabaseAcceptsEachOfTheThreeAllowedImageTypes(string contentType)
    {
        await InsertRawAsync(new byte[] { 1 }, contentType);

        (await _store.GetAsync(CancellationToken.None))!.ContentType.Should().Be(contentType);
    }

    [Fact]
    public async Task TheDatabaseRefusesAnEmptyLogoAndOneByteOverFiveMegabytesButAcceptsTheLimit()
    {
        var empty = () => InsertRawAsync([], "image/png");
        var over = () => InsertRawAsync(new byte[5 * 1024 * 1024 + 1], "image/png");

        (await empty.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23514");
        (await over.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23514");

        await InsertRawAsync(new byte[5 * 1024 * 1024], "image/webp");
        (await _store.GetAsync(CancellationToken.None))!.Content.Should().HaveCount(5 * 1024 * 1024);
    }

    private async Task InsertRawAsync(byte[] content, string contentType)
    {
        await using var command = _db.DataSource.CreateCommand(
            "INSERT INTO settings.business_logo (id, content, content_type, updated_at) VALUES (1, @content, @type, now());");
        command.Parameters.AddWithValue("content", content);
        command.Parameters.AddWithValue("type", contentType);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task DeleteWhenNoLogoExistsIsANoOpNotAnError()
        => await FluentActions.Invoking(() => _store.DeleteAsync(CancellationToken.None))
            .Should().NotThrowAsync();
}
