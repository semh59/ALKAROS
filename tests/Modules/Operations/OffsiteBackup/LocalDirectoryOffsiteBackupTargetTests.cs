using Xunit;

namespace ALKAROS.Operations.OffsiteBackup.Tests;

public sealed class LocalDirectoryOffsiteBackupTargetTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "alkaros-bkp001-target-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task UploadAsyncThenDownloadAsyncReturnsTheSameBytes()
    {
        var target = new LocalDirectoryOffsiteBackupTarget(_root);
        byte[] content = [1, 2, 3, 4, 5];

        await target.UploadAsync("artifact-1", content);
        var downloaded = await target.DownloadAsync("artifact-1");

        Assert.Equal(content, downloaded);
    }

    [Fact]
    public async Task UploadAsyncDuplicateArtifactIdThrowsImmutabilityViolation()
    {
        var target = new LocalDirectoryOffsiteBackupTarget(_root);
        await target.UploadAsync("artifact-1", new byte[] { 1 });

        await Assert.ThrowsAsync<OffsiteBackupImmutabilityViolationException>(
            () => target.UploadAsync("artifact-1", new byte[] { 2 }));
    }

    [Fact]
    public async Task ExistsAsyncUnknownArtifactReturnsFalse()
    {
        var target = new LocalDirectoryOffsiteBackupTarget(_root);
        Assert.False(await target.ExistsAsync("missing"));
    }

    [Fact]
    public async Task DownloadAsyncUnknownArtifactThrowsFileNotFound()
    {
        var target = new LocalDirectoryOffsiteBackupTarget(_root);
        await Assert.ThrowsAsync<FileNotFoundException>(() => target.DownloadAsync("missing"));
    }

    [Fact]
    public async Task ListArtifactIdsAsyncReturnsEveryUploadedArtifactId()
    {
        var target = new LocalDirectoryOffsiteBackupTarget(_root);
        await target.UploadAsync("a", new byte[] { 1 });
        await target.UploadAsync("b", new byte[] { 2 });

        var ids = await target.ListArtifactIdsAsync();

        Assert.Equal(["a", "b"], ids.OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task UploadAsyncArtifactIdWithPathSeparatorsIsRejected()
    {
        var target = new LocalDirectoryOffsiteBackupTarget(_root);
        await Assert.ThrowsAsync<ArgumentException>(() => target.UploadAsync("../escape", new byte[] { 1 }));
    }
}
