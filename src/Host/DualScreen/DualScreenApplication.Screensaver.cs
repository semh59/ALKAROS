using ALKAROS.Host.Experience.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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
    // V1-RMD-235: found by the 2026-09-17 Kasa audit — without an explicit
    // limit here, Kestrel's own implicit default (~28.6 MB) was the only
    // thing standing between a request and the server, leaving barely any
    // margin over the 20 MB video cap above (a client could send right up
    // to Kestrel's own ceiling before either this code or Kestrel itself
    // rejected it). A little headroom over the real video cap, not equal to
    // it, so a legitimate ~20 MB upload's own multipart framing overhead
    // never gets rejected by the transport limit before this handler's own
    // (more informative) size check ever runs.
    private const long MaxScreensaverRequestBodyBytes = 25 * 1024 * 1024;
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

        // V1-RMD-235: this group's own PUT binds an IFormFile, which ASP.NET
        // Core's minimal-API model binder reads eagerly as part of argument
        // binding — before any IEndpointFilter (including
        // CatalogManagerEndpointFilter below) ever runs, and RouteGroupBuilder
        // itself has no `Use` (it is not an IApplicationBuilder). A Kestrel
        // body-size limit has to be set from plain host-level middleware
        // ahead of that binding step to have any effect, path-scoped here so
        // no other route's own limit is touched.
        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsPut(context.Request.Method)
                && context.Request.Path.StartsWithSegments(
                    "/api/v1/management/customer-display/screensaver", StringComparison.OrdinalIgnoreCase))
            {
                var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (bodySizeFeature is { IsReadOnly: false })
                    bodySizeFeature.MaxRequestBodySize = MaxScreensaverRequestBodyBytes;
            }
            await next(context);
        });

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
            var content = stream.ToArray();

            // V1-RMD-235: the declared Content-Type header above is entirely
            // client-controlled and already matched the allowlist by this
            // point — this checks the file's own first bytes (magic number)
            // actually match what it claims to be, independent of that
            // header, before anything gets persisted as "the business's
            // screensaver."
            if (!HasValidScreensaverMagicNumber(content, file.ContentType))
                return ScreensaverValidationError(context, "Dosya içeriği beyan edilen türle eşleşmiyor.");

            await store.SaveScreensaverAsync(content, file.ContentType, cancellationToken);
            return Results.NoContent();
        })
            // V1-RMD-235: a no-op today, kept deliberately explicit rather
            // than removed — no AddAntiforgery/UseAntiforgery is registered
            // anywhere in this host, so minimal API's antiforgery
            // enforcement (which only activates when those services ARE
            // registered) was never actually gating this endpoint. This
            // documents that fact for the next reader instead of implying
            // (as the bare call once did) that removing it would open a real
            // CSRF gap; CSRF defense here is the terminal session cookie's
            // own SameSite=Strict attribute, unaffected by this call either
            // way. If antiforgery services are ever added host-wide, this
            // multipart form upload is the one endpoint that would need this
            // to stay exempt.
            .DisableAntiforgery();

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

    // V1-RMD-235: the Content-Type header this file arrived with is entirely
    // client-declared and already checked against the allowlist by the
    // caller — this checks the actual bytes independently, so a file
    // relabeled with a spoofed header (e.g. an executable saved as
    // "screensaver.png") is still caught. Checks the magic number against
    // the SPECIFIC declared Content-Type, not "any of the allowed formats"
    // (2026-09-22 independent audit finding: the original version accepted
    // any recognized signature regardless of which type was declared, e.g.
    // a real PNG declared as image/webp would pass).
    private static bool HasValidScreensaverMagicNumber(byte[] content, string declaredContentType) =>
        declaredContentType switch
        {
            "image/png" => HasPngMagicNumber(content),
            "image/jpeg" => HasJpegMagicNumber(content),
            "image/webp" => HasWebpMagicNumber(content),
            "video/mp4" => HasMp4MagicNumber(content),
            _ => false,
        };

    private static bool HasPngMagicNumber(byte[] content) =>
        content.Length >= 8
        && content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47
        && content[4] == 0x0D && content[5] == 0x0A && content[6] == 0x1A && content[7] == 0x0A;

    private static bool HasJpegMagicNumber(byte[] content) =>
        content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF;

    private static bool HasWebpMagicNumber(byte[] content) =>
        content.Length >= 12
        && content[0] == 'R' && content[1] == 'I' && content[2] == 'F' && content[3] == 'F'
        && content[8] == 'W' && content[9] == 'E' && content[10] == 'B' && content[11] == 'P';

    // MP4 (ISO base media file format): the first box is almost always
    // "ftyp" starting at byte offset 4 (bytes 0-3 are that box's own
    // big-endian size, byte content varies with the file, not checked
    // here) — the same signature browsers and file-type sniffers use to
    // recognize the container.
    private static bool HasMp4MagicNumber(byte[] content) =>
        content.Length >= 8
        && content[4] == 'f' && content[5] == 't' && content[6] == 'y' && content[7] == 'p';
}
