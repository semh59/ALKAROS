namespace ALKAROS.Operations.OffsiteBackup;

/// <summary>
/// Filesystem-backed <see cref="IOffsiteBackupTarget"/>: a second directory
/// tree (on a different volume/host in a real deployment) standing in for a
/// vendor object store. Same atomic-write discipline as
/// <c>LocalBackupEngine</c> (V1-OPS-002): write a sibling temp file, flush,
/// then create the final name with <see cref="FileMode.CreateNew"/> so a
/// concurrent/duplicate upload cannot silently overwrite an existing object.
/// </summary>
public sealed class LocalDirectoryOffsiteBackupTarget : IOffsiteBackupTarget
{
    private readonly string _rootDirectory;

    public LocalDirectoryOffsiteBackupTarget(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = rootDirectory;
        Directory.CreateDirectory(_rootDirectory);
    }

    public async Task<string> UploadAsync(string artifactId, ReadOnlyMemory<byte> encryptedContent, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        var finalPath = PathFor(artifactId);

        if (File.Exists(finalPath))
            throw new OffsiteBackupImmutabilityViolationException(artifactId);

        var temporaryPath = finalPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                options: FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(encryptedContent, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            try
            {
                File.Move(temporaryPath, finalPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(finalPath))
            {
                // Lost a race with a concurrent upload of the same artifact id.
                throw new OffsiteBackupImmutabilityViolationException(artifactId);
            }

            temporaryPath = null;
            return finalPath;
        }
        finally
        {
            if (temporaryPath is not null)
                File.Delete(temporaryPath);
        }
    }

    public async Task<byte[]> DownloadAsync(string artifactId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        var path = PathFor(artifactId);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Artifact '{artifactId}' is not present at the off-site target.", path);

        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    public Task<bool> ExistsAsync(string artifactId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        return Task.FromResult(File.Exists(PathFor(artifactId)));
    }

    public Task<IReadOnlyList<string>> ListArtifactIdsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> ids = Directory.EnumerateFiles(_rootDirectory, "*.enc")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToArray();
        return Task.FromResult(ids);
    }

    private string PathFor(string artifactId)
    {
        if (artifactId.Any(c => c is '/' or '\\' or '\0') || artifactId.Contains(".."))
            throw new ArgumentException("Artifact id must not contain path separators or '..'.", nameof(artifactId));
        return Path.Combine(_rootDirectory, artifactId + ".enc");
    }
}
