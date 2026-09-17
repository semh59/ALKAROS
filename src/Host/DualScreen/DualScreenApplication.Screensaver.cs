using ALKAROS.Host.Experience.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ALKAROS.Host.DualScreen;

public static partial class DualScreenApplication
{
    private const long MaxScreensaverImageBytes = 5 * 1024 * 1024;
    // V1-CDP-004: a video is a whole different content class (an ambient,
    // silent, looping storefront reel, not a still card) — Semih's own call
    // was a separate, larger cap for it rather than raising the image limit
    // to match, so a 6 MB image is still rejected the same as before.
    private const long MaxScreensaverVideoBytes = 20 * 1024 * 1024;
    private static readonly string[] AllowedScreensaverImageContentTypes = ["image/png", "image/jpeg", "image/webp"];
    private static readonly string[] AllowedScreensaverVideoContentTypes = ["video/mp4"];

    /// <summary>
    /// V1-CDP-001: the business's own idle-screen image (V1-CUI-007's Faz 0
    /// decision leaves the default branded card in place when none is set;
    /// this only lets a manager replace it). PUT/DELETE reuse Catalog's own
    /// manager gate (`catalog.manage`, the same permission System-health
    /// uses) rather than inventing a new one; GET reuses the display
    /// principal's existing read-only session (RequireDisplayAsync,
    /// DualScreenApplication.Endpoints.cs) since a screensaver is
    /// per-business, not per-terminal. V1-CDP-004: the same PUT also accepts
    /// a short looping video (`video/mp4`) — the storage/GET side already
    /// carries content bytes + content-type opaquely, so nothing there
    /// needed to change, only which types/sizes this validation allows.
    /// </summary>
    public static RouteGroupBuilder MapCustomerDisplayScreensaverApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var management = app.MapGroup("/api/v1/management/customer-display")
            .WithTags("CustomerDisplayScreensaver")
            .RequireRateLimiting("terminal-write")
            .AddEndpointFilter<CatalogManagerEndpointFilter>();

        management.MapPut("/screensaver", async (
            IFormFile? file,
            DualScreenStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (file is null || file.Length == 0)
                return ScreensaverValidationError(context, "Bir görsel veya video dosyası seçilmedi.");
            var isImage = AllowedScreensaverImageContentTypes.Contains(file.ContentType);
            var isVideo = AllowedScreensaverVideoContentTypes.Contains(file.ContentType);
            if (!isImage && !isVideo)
                return ScreensaverValidationError(context, "Yalnız PNG, JPEG, WEBP görseli veya MP4 videosu yüklenebilir.");
            if (isImage && file.Length > MaxScreensaverImageBytes)
                return ScreensaverValidationError(context, "Görsel 5 MB'ı aşamaz.");
            if (isVideo && file.Length > MaxScreensaverVideoBytes)
                return ScreensaverValidationError(context, "Video 20 MB'ı aşamaz.");

            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            await store.SaveScreensaverAsync(stream.ToArray(), file.ContentType, cancellationToken);
            return Results.NoContent();
        }).DisableAntiforgery();

        management.MapDelete("/screensaver", async (
            DualScreenStore store,
            CancellationToken cancellationToken) =>
        {
            await store.DeleteScreensaverAsync(cancellationToken);
            return Results.NoContent();
        });

        app.MapGet("/api/v1/customer-displays/{displayId:guid}/screensaver", async (
            Guid displayId,
            HttpContext context,
            DualScreenStore store,
            CancellationToken cancellationToken) =>
        {
            await RequireDisplayAsync(context, displayId, store, cancellationToken);
            var image = await store.GetScreensaverAsync(cancellationToken)
                ?? throw new DualScreenNotFoundException("No screensaver image has been set.");

            if (context.Request.Headers.IfNoneMatch.Contains(image.ETag))
                return Results.StatusCode(StatusCodes.Status304NotModified);

            context.Response.Headers.ETag = image.ETag;
            context.Response.Headers.CacheControl = "private, max-age=300";
            return Results.File(image.Content, image.ContentType);
        }).RequireRateLimiting("display-read");

        return management;
    }

    private static IResult ScreensaverValidationError(HttpContext context, string message)
        => Error(context, StatusCodes.Status400BadRequest, "INVALID_SCREENSAVER", message);
}
