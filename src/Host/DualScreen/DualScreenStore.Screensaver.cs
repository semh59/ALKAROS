using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.DualScreen;

public sealed partial class DualScreenStore
{
    /// <summary>
    /// V1-CDP-001: upserts the single global screensaver row. There is
    /// never more than one — the table's own `id = 1` check constraint
    /// enforces it at the database level, this is just the matching
    /// application-side upsert.
    /// </summary>
    public async Task SaveScreensaverAsync(byte[] content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO customer_display.screensaver_images (id, content, content_type, updated_at, row_version)
            VALUES (1, @content, @content_type, @updated_at, 1)
            ON CONFLICT (id) DO UPDATE SET
                content = EXCLUDED.content,
                content_type = EXCLUDED.content_type,
                updated_at = EXCLUDED.updated_at,
                row_version = customer_display.screensaver_images.row_version + 1;
            """);
        command.Parameters.Add("content", NpgsqlDbType.Bytea).Value = content;
        command.Parameters.AddWithValue("content_type", contentType);
        command.Parameters.AddWithValue("updated_at", DateTimeOffset.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>V1-CDP-001: removes the screensaver row so the display falls back to its default branded idle card.</summary>
    public async Task DeleteScreensaverAsync(CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            "DELETE FROM customer_display.screensaver_images WHERE id = 1;");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>V1-CDP-001: null when no screensaver has been set — the caller renders the default branded idle card.</summary>
    public async Task<ScreensaverImage?> GetScreensaverAsync(CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT content, content_type, updated_at FROM customer_display.screensaver_images WHERE id = 1;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        var content = (byte[])reader[0];
        var contentType = reader.GetString(1);
        var updatedAt = reader.GetFieldValue<DateTimeOffset>(2);
        return new ScreensaverImage(content, contentType, updatedAt);
    }
}

/// <summary>V1-CDP-001: the screensaver row's content, read back for the display's GET.</summary>
public sealed record ScreensaverImage(byte[] Content, string ContentType, DateTimeOffset UpdatedAt)
{
    /// <summary>A weak ETag derived from the row's own update timestamp - two reads of the same row always agree, any real change always changes it.</summary>
    public string ETag => $"\"{UpdatedAt.UtcTicks:x}\"";
}
