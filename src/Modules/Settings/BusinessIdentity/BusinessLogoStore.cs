using System.Data.Common;

namespace ALKAROS.Settings.BusinessIdentity;

/// <summary>
/// V1-SET-008: the business's own logo image for the QR customer pages —
/// one global row, no per-terminal branding, same single-row model as
/// V1-SET-007's business.name/business.accent_theme and V1-CDP-001's
/// customer_display.screensaver_images (the direct precedent this mirrors).
/// </summary>
public interface IBusinessLogoStore
{
    Task SaveAsync(byte[] content, string contentType, CancellationToken cancellationToken);
    Task DeleteAsync(CancellationToken cancellationToken);
    Task<BusinessLogoImage?> GetAsync(CancellationToken cancellationToken);
}

/// <summary>The logo row's content, read back for both the manager's own confirmation and the public QR read.</summary>
public sealed record BusinessLogoImage(byte[] Content, string ContentType, DateTimeOffset UpdatedAt)
{
    /// <summary>A weak ETag derived from the row's own update timestamp - two reads of the same row always agree, any real change always changes it.</summary>
    public string ETag => $"\"{UpdatedAt.UtcTicks:x}\"";
}

public sealed class BusinessLogoStore : IBusinessLogoStore
{
    private readonly DbDataSource _dataSource;

    public BusinessLogoStore(DbDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <summary>Upserts the single global logo row — the table's own `id = 1` check constraint enforces there is never more than one.</summary>
    public async Task SaveAsync(byte[] content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO settings.business_logo (id, content, content_type, updated_at, row_version)
            VALUES (1, @content, @content_type, @updated_at, 1)
            ON CONFLICT (id) DO UPDATE SET
                content = EXCLUDED.content,
                content_type = EXCLUDED.content_type,
                updated_at = EXCLUDED.updated_at,
                row_version = settings.business_logo.row_version + 1;
            """);
        AddParameter(command, "content", content);
        AddParameter(command, "content_type", contentType);
        AddParameter(command, "updated_at", DateTimeOffset.UtcNow);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Removes the logo row so the QR customer pages fall back to no logo (ALKAROS's own default, not a placeholder image).</summary>
    public async Task DeleteAsync(CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand("DELETE FROM settings.business_logo WHERE id = 1;");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Null when no logo has been set — the caller renders no logo, never a placeholder pretending one exists.</summary>
    public async Task<BusinessLogoImage?> GetAsync(CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT content, content_type, updated_at FROM settings.business_logo WHERE id = 1;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        var content = (byte[])reader[0];
        var contentType = reader.GetString(1);
        var updatedAt = reader.GetFieldValue<DateTimeOffset>(2);
        return new BusinessLogoImage(content, contentType, updatedAt);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
