namespace ALKAROS.Identity.Authorization;

/// <summary>
/// Server-side authorization check. Throws <see cref="AuthorizationDeniedException"/>
/// when the actor is unknown, inactive, or lacks the named permission; every
/// denial is recorded through <see cref="IDenialEventSink"/>.
/// </summary>
public interface IAuthorizationService
{
    Task AuthorizeAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default);
}