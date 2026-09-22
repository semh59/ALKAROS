using ALKAROS.Settings.BusinessIdentity.Tests.Fixtures;
using FluentAssertions;
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
    public async Task DeleteWhenNoLogoExistsIsANoOpNotAnError()
        => await FluentActions.Invoking(() => _store.DeleteAsync(CancellationToken.None))
            .Should().NotThrowAsync();
}
