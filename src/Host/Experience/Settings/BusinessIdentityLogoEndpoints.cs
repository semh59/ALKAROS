using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Settings.BusinessIdentity;
using ALKAROS.Settings.TypedSettings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.Settings;

/// <summary>
/// V1-SET-008: the business's own QR-page logo, mirroring V1-CDP-001's
/// screensaver upload (same single-global-row storage, magic-number
/// validation, size cap and Kestrel body-size override). Gated by the same
/// `settings.manage` manager surface V1-RMD-246 already built for
/// business.name/business.accent_theme, rather than reusing Catalog's
/// `catalog.manage` the way the screensaver did — this endpoint mutates a
/// business-identity setting, not a customer-display concern, so it belongs
/// under the same manager permission as its two sibling settings.
/// </summary>
public static class BusinessIdentityLogoEndpoints
{
    private const long MaxLogoImageBytes = 5 * 1024 * 1024;
    // Same reasoning as V1-RMD-235's screensaver request-body cap: a little
    // headroom over the real 5 MB image cap so a legitimate upload's own
    // multipart framing overhead is never rejected by the transport limit
    // before this handler's own (more informative) size check ever runs.
    private const long MaxLogoRequestBodyBytes = 6 * 1024 * 1024;
    private static readonly string[] AllowedLogoContentTypes = ["image/png", "image/jpeg", "image/webp"];
    private const string LogoPath = "/api/v1/management/business-identity/logo";

    public static IServiceCollection AddBusinessIdentityLogoExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // Same DbDataSource/ISettingsService gap every other settings-backed
        // Host experience registration already has to close on its own
        // (QrOrderingEndpoints.AddQrOrderingExperience,
        // SettingsManagementEndpoints.AddSettingsManagementExperience) — all
        // TryAdd, so whichever experience registers first wins, harmlessly.
        services.TryAddSingleton<System.Data.Common.DbDataSource>(
            serviceProvider => serviceProvider.GetRequiredService<NpgsqlDataSource>());
        services.TryAddSingleton<ISettingValidator, SettingValidator>();
        services.TryAddSingleton<ISettingsRepository, PostgresSettingsRepository>();
        services.TryAddSingleton<ISettingsService, SettingsService>();
        services.TryAddSingleton<IBusinessLogoStore, BusinessLogoStore>();
        services.TryAddScoped<SettingsManagerAuthentication>();
        // SettingsManagerEndpointFilter's own dependency chain — same gap
        // AddSettingsManagementExperience already has to close.
        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        return services;
    }

    public static RouteGroupBuilder MapBusinessIdentityLogoApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // V1-RMD-235's own screensaver comment applies identically here: an
        // IFormFile parameter is bound eagerly by the minimal-API model
        // binder before any IEndpointFilter runs, so the Kestrel body-size
        // limit has to be raised from plain host-level middleware ahead of
        // that binding step, path-scoped so no other route is affected.
        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsPut(context.Request.Method)
                && context.Request.Path.StartsWithSegments(LogoPath, StringComparison.OrdinalIgnoreCase))
            {
                var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (bodySizeFeature is { IsReadOnly: false })
                    bodySizeFeature.MaxRequestBodySize = MaxLogoRequestBodyBytes;
            }
            await next(context);
        });

        var management = app.MapGroup("/api/v1/management/business-identity")
            .WithTags("BusinessIdentityLogo")
            .AddEndpointFilter<SettingsManagerEndpointFilter>();

        management.MapPut("/logo", async (
            IFormFile? file,
            IBusinessLogoStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (file is null || file.Length == 0)
                return LogoValidationError(context, "Bir logo dosyası seçilmedi.");
            if (!AllowedLogoContentTypes.Contains(file.ContentType))
                return LogoValidationError(context, "Yalnız PNG, JPEG veya WEBP görseli yüklenebilir.");
            if (file.Length > MaxLogoImageBytes)
                return LogoValidationError(context, "Logo 5 MB'ı aşamaz.");

            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            var content = stream.ToArray();

            // Same reasoning as V1-RMD-235: the declared Content-Type header
            // is entirely client-controlled — this checks the file's own
            // first bytes independently, before anything gets persisted as
            // "the business's logo."
            if (!HasValidLogoMagicNumber(content))
                return LogoValidationError(context, "Dosya içeriği beyan edilen türle eşleşmiyor.");

            await store.SaveAsync(content, file.ContentType, cancellationToken);
            return Results.NoContent();
        })
            // No AddAntiforgery/UseAntiforgery is registered anywhere in this
            // host (same fact V1-RMD-235 documented for the screensaver's
            // identical multipart upload) — this is a no-op today, kept
            // explicit rather than removed.
            .DisableAntiforgery();

        management.MapDelete("/logo", async (
            IBusinessLogoStore store,
            CancellationToken cancellationToken) =>
        {
            await store.DeleteAsync(cancellationToken);
            return Results.NoContent();
        });

        return management;
    }

    private static IResult LogoValidationError(HttpContext context, string message)
        => Results.Json(
            new SettingsApiErrorEnvelopeV1(new SettingsApiErrorV1("INVALID_LOGO", message, StatusCodes.Status400BadRequest, context.TraceIdentifier)),
            statusCode: StatusCodes.Status400BadRequest);

    // Recognizes only the exact formats this endpoint allows (PNG/JPEG/WEBP)
    // — same signatures V1-RMD-235's screensaver validation already checks.
    private static bool HasValidLogoMagicNumber(byte[] content) =>
        HasPngMagicNumber(content) || HasJpegMagicNumber(content) || HasWebpMagicNumber(content);

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
}
