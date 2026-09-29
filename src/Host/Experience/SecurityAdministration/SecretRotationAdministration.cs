using ALKAROS.Host.DualScreen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Secrets;
using ALKAROS.Security.SecretRotation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ALKAROS.Host.Experience.SecurityAdministration;

/// <summary>
/// V1-RMD-269: versioned secret rotation (V15-SEC-001) had no caller in the
/// running host. This mounts list / initialize / rotate / revoke / rollback on
/// the security administration group (manager session + security.manage).
///
/// The key MATERIAL never passes through this API: it lives in the deployment
/// environment (<c>ALKAROS_SECRET_{NAME}_V{N}</c>, see
/// EnvironmentVariableSecretProvider). Rotation only changes which version is
/// active, and refuses to activate a version whose material is not present, so
/// a manager cannot rotate the system into a state that cannot decrypt.
/// Responses and audit events carry version numbers and timestamps, never values.
/// </summary>
public static class SecretRotationAdministration
{
    /// <summary>Logical secret names an operator may manage. Extending it is a deliberate, reviewed change.</summary>
    public static readonly IReadOnlySet<string> KnownSecrets =
        new HashSet<string>(StringComparer.Ordinal) { "offsite-backup" };

    public const int MaxOverlapHours = 24 * 30;

    public static RouteGroupBuilder MapSecretRotation(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/secrets", (ISecretRotationStore store) =>
            Results.Ok(KnownSecrets.OrderBy(name => name, StringComparer.Ordinal).Select(name =>
            {
                var record = store.Find(name);
                return new SecretRotationStateV1(name, record is null ? null : SecretRotationSnapshot.From(record));
            })));

        group.MapGet("/secrets/{name}/rotation", (string name, ISecretRotationStore store, HttpContext context) =>
        {
            if (!KnownSecrets.Contains(name))
                return NotFound(context, "İstenen gizli anahtar tanımlı değil.");
            var record = store.Find(name);
            return record is null
                ? NotFound(context, "Bu gizli anahtar için henüz bir sürüm kaydı yok.")
                : Results.Ok(SecretRotationSnapshot.From(record));
        });

        group.MapPost("/secrets/{name}/rotation/initialize", async (
            string name, ISecretRotationStore store, ISecretProvider secrets,
            IAuditEventStore audit, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (!KnownSecrets.Contains(name))
                return NotFound(context, "İstenen gizli anahtar tanımlı değil.");
            if (store.Find(name) is not null)
                return Conflict(context, "ALREADY_INITIALIZED", "Bu gizli anahtar için sürüm kaydı zaten var.");
            if (MissingMaterial(secrets, name, 1) is { } missing)
                return Conflict(context, "KEY_MATERIAL_MISSING", missing);

            var record = SecretRotationRecord.Initialize(name, DateTimeOffset.UtcNow);
            store.Save(record);
            await AuditAsync(audit, context, "initialize", name, 1, cancellationToken);
            return Results.Ok(SecretRotationSnapshot.From(record));
        });

        group.MapPost("/secrets/{name}/rotation/rotate", async (
            string name, RotateSecretRequestV1 request, ISecretRotationStore store, ISecretProvider secrets,
            IAuditEventStore audit, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (!KnownSecrets.Contains(name))
                return NotFound(context, "İstenen gizli anahtar tanımlı değil.");
            if (request.OverlapHours is < 0 or > MaxOverlapHours)
                return BadRequest(context, $"Geçiş süresi 0 ile {MaxOverlapHours} saat arasında olmalı.");
            var record = store.Find(name);
            if (record is null)
                return NotFound(context, "Bu gizli anahtar için henüz bir sürüm kaydı yok; önce başlatın.");

            var nextVersion = record.Versions.Max(version => version.Version) + 1;
            if (MissingMaterial(secrets, name, nextVersion) is { } missing)
                return Conflict(context, "KEY_MATERIAL_MISSING", missing);

            var rotated = record.Rotate(DateTimeOffset.UtcNow, TimeSpan.FromHours(request.OverlapHours));
            store.Save(rotated);
            await AuditAsync(audit, context, "rotate", name, nextVersion, cancellationToken);
            return Results.Ok(SecretRotationSnapshot.From(rotated));
        });

        group.MapPost("/secrets/{name}/rotation/revoke", async (
            string name, SecretVersionRequestV1 request, ISecretRotationStore store,
            IAuditEventStore audit, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (!KnownSecrets.Contains(name))
                return NotFound(context, "İstenen gizli anahtar tanımlı değil.");
            var record = store.Find(name);
            if (record is null)
                return NotFound(context, "Bu gizli anahtar için henüz bir sürüm kaydı yok.");
            if (record.ActiveVersion?.Version == request.Version)
                return Conflict(context, "ACTIVE_VERSION", "Etkin sürüm iptal edilemez; önce yeni bir sürüme döndürün.");

            var revoked = record.Revoke(request.Version, DateTimeOffset.UtcNow);
            store.Save(revoked);
            await AuditAsync(audit, context, "revoke", name, request.Version, cancellationToken);
            return Results.Ok(SecretRotationSnapshot.From(revoked));
        });

        group.MapPost("/secrets/{name}/rotation/rollback", async (
            string name, SecretVersionRequestV1 request, ISecretRotationStore store, ISecretProvider secrets,
            IAuditEventStore audit, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (!KnownSecrets.Contains(name))
                return NotFound(context, "İstenen gizli anahtar tanımlı değil.");
            var record = store.Find(name);
            if (record is null)
                return NotFound(context, "Bu gizli anahtar için henüz bir sürüm kaydı yok.");
            if (MissingMaterial(secrets, name, request.Version) is { } missing)
                return Conflict(context, "KEY_MATERIAL_MISSING", missing);

            var rolledBack = record.Rollback(request.Version, DateTimeOffset.UtcNow);
            store.Save(rolledBack);
            await AuditAsync(audit, context, "rollback", name, request.Version, cancellationToken);
            return Results.Ok(SecretRotationSnapshot.From(rolledBack));
        });

        return group;
    }

    /// <summary>The Turkish explanation when a version's key material is not in the environment; null when present.</summary>
    private static string? MissingMaterial(ISecretProvider secrets, string name, int version)
    {
        var reference = SecretVersionReferenceNaming.ReferenceFor(name, version);
        if (secrets.GetValue(reference) is not null)
            return null;
        var variable = "ALKAROS_SECRET_" + reference.Name.ToUpperInvariant().Replace('-', '_');
        return $"{version}. sürümün anahtarı ortamda tanımlı değil; önce {variable} değişkenini tanımlayın.";
    }

    private static Task AuditAsync(
        IAuditEventStore audit, HttpContext context, string operation, string name, int version, CancellationToken cancellationToken)
        => audit.AppendAsync(
            new AuditEvent(
                id: Guid.NewGuid(),
                eventName: $"secret.rotation.{operation}",
                aggregateType: "Secret",
                aggregateId: AggregateIdFor(name),
                actorType: "User",
                correlationId: context.TraceIdentifier,
                actorId: SecurityAdministrationEndpointFilter.RequireActorId(context),
                metadataJson: JsonSerializer.Serialize(new { secretName = name, version })),
            cancellationToken);

    /// <summary>A stable id per logical secret name, so its whole history is queryable by aggregate.</summary>
    public static Guid AggregateIdFor(string name)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes("secret:" + name))[..16]);

    private static IResult Error(HttpContext context, int status, string code, string message)
        => Results.Json(
            new ApiErrorEnvelope(new ApiError(code, message, status, context.TraceIdentifier)),
            statusCode: status);

    private static IResult NotFound(HttpContext context, string message)
        => Error(context, StatusCodes.Status404NotFound, "NOT_FOUND", message);

    private static IResult Conflict(HttpContext context, string code, string message)
        => Error(context, StatusCodes.Status409Conflict, code, message);

    private static IResult BadRequest(HttpContext context, string message)
        => Error(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED", message);
}

public sealed record SecretRotationStateV1(string Name, SecretRotationSnapshot? Rotation);

public sealed record RotateSecretRequestV1(int OverlapHours);

public sealed record SecretVersionRequestV1(int Version);
