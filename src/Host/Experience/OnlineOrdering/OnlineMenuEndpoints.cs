using System.Globalization;
using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.OnlineOrdering.Credentials;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.Menu;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;
using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.OnlineOrdering;

/// <summary>
/// V12-OUI-005: the online food screen's Menu tab for a manager (<c>integrations.manage</c>) — per platform, every catalog
/// product with its platform code (mapping), the price the platform was last sent, whether the platform shows it on sale,
/// the store's own platform menu where the platform lets it be read (Trendyol Go), the catalog menus that can be
/// published and the latest publications. Mapping changes go through the shared mapping service; publishing uses the
/// existing catalog publication endpoint. No platform error text reaches the screen.
/// </summary>
public static class OnlineMenuEndpoints
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/online-menu";
    public const int MaxProducts = 1000;

    /// <summary>The catalog publication channel of each platform (the name its publishers and availability states use).</summary>
    public static readonly IReadOnlyDictionary<string, string> Channels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["yemeksepeti"] = "Yemeksepeti",
        [TrendyolGoEvents.Provider] = TrendyolGoAvailabilityPublisher.ChannelName,
    };

    public static IServiceCollection AddOnlineMenuExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        services.TryAddTransient<ISecretProvider, EnvironmentVariableSecretProvider>();
        services.TryAddTransient<IOnlinePlatformCredentialStore, PostgresOnlinePlatformCredentialStore>();
        services.TryAddTransient<IProductRepository, PostgresProductRepository>();
        services.TryAddTransient<IProductModifierGroupRepository, PostgresProductModifierGroupRepository>();
        services.TryAddTransient<IModifierGroupRepository, PostgresModifierGroupRepository>();
        services.TryAddTransient<OnlineMenuExceptionFilter>();
        // TryAdd defers to OnlineOrderingModule in the real Host (one client, platform settings stored first).
        services.TryAddSingleton(provider => new TrendyolGoMenuClient(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
            new StoredOnlinePlatformSecretProvider(
                provider.GetRequiredService<IOnlinePlatformCredentialStore>(), provider.GetRequiredService<ISecretProvider>()),
            TimeProvider.System));
        return services;
    }

    public static RouteGroupBuilder MapOnlineMenuApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("OnlineMenu")
            .AddEndpointFilter<OnlineMenuExceptionFilter>();

        group.MapGet("/{provider}", async (
            Guid terminalId,
            string provider,
            NpgsqlDataSource dataSource,
            TrendyolGoMenuClient trendyolGoMenu,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireManagerAsync(context, terminalId, dualStore, authorization, cancellationToken);
            var channel = ChannelOf(provider);
            var products = await ReadProductsAsync(dataSource, provider, channel, cancellationToken);
            IReadOnlyList<OnlineMenuPlatformProductV1>? platformProducts = null;
            var platformMenuUnavailable = false;
            if (provider == TrendyolGoEvents.Provider)
            {
                var menu = trendyolGoMenu;
                if (menu.IsConfigured)
                {
                    try
                    {
                        var mappedBySku = products.Where(p => p.ExternalSku is not null).ToDictionary(p => p.ExternalSku!, p => p.ProductId, StringComparer.Ordinal);
                        platformProducts = (await menu.MenuProductsAsync(cancellationToken))
                            .Select(p => new OnlineMenuPlatformProductV1(
                                p.Id.ToString(CultureInfo.InvariantCulture), p.Name, p.Active,
                                mappedBySku.TryGetValue(p.Id.ToString(CultureInfo.InvariantCulture), out var mapped) ? mapped : null))
                            .ToList();
                    }
                    catch (Exception ex) when (ex is TrendyolGoMenuException or HttpRequestException or TaskCanceledException)
                    {
                        platformMenuUnavailable = true;
                    }
                }
                else
                {
                    platformMenuUnavailable = true;
                }
            }

            return Results.Ok(new OnlineMenuV1(
                provider, channel, products, platformProducts, platformMenuUnavailable,
                await ReadMenusAsync(dataSource, cancellationToken), await ReadPublicationsAsync(dataSource, channel, cancellationToken)));
        }).RequireRateLimiting("terminal-read");

        group.MapPut("/{provider}/mappings/{productId:guid}", async (
            Guid terminalId,
            string provider,
            Guid productId,
            SaveOnlineMappingRequestV1 request,
            NpgsqlDataSource dataSource,
            IProductRepository products,
            IProductModifierGroupRepository productModifierGroups,
            IModifierGroupRepository modifierGroups,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireManagerAsync(context, terminalId, dualStore, authorization, cancellationToken);
            ChannelOf(provider);
            var sku = (request.ExternalSku ?? "").Trim();
            if (sku.Length is 0 or > 100 || sku.Any(char.IsControl))
                return Refuse(StatusCodes.Status400BadRequest, "INVALID_CODE", "Platform ürün kodu geçersiz.");
            // Trendyol Go numbers its products; anything else could never be sent to it.
            if (provider == TrendyolGoEvents.Provider && !(long.TryParse(sku, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0))
                return Refuse(StatusCodes.Status400BadRequest, "INVALID_CODE", "Trendyol Go ürün kodu yalnız rakamlardan oluşur.");

            var mappings = new PostgresYemeksepetiProductMappingService(dataSource, products, productModifierGroups, modifierGroups, provider);
            // A code that already stands for another product is never moved silently.
            var mapping = await mappings.MapIfUnownedAsync(sku, productId, DateTimeOffset.UtcNow, userId, cancellationToken);
            return mapping is null
                ? Refuse(StatusCodes.Status409Conflict, "CODE_TAKEN", "Bu platform kodu başka bir ürüne eşli; önce o eşlemeyi kaldırın.")
                : Results.Ok(new OnlineMappingV1(mapping.ProductId, mapping.ExternalSku));
        }).RequireRateLimiting("terminal-write");

        group.MapDelete("/{provider}/mappings/{productId:guid}", async (
            Guid terminalId,
            string provider,
            Guid productId,
            NpgsqlDataSource dataSource,
            IProductRepository products,
            IProductModifierGroupRepository productModifierGroups,
            IModifierGroupRepository modifierGroups,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireManagerAsync(context, terminalId, dualStore, authorization, cancellationToken);
            ChannelOf(provider);
            var mappings = new PostgresYemeksepetiProductMappingService(dataSource, products, productModifierGroups, modifierGroups, provider);
            return await mappings.CloseOpenMappingAsync(productId, DateTimeOffset.UtcNow, userId, cancellationToken)
                ? Results.NoContent()
                : Refuse(StatusCodes.Status404NotFound, "NOT_MAPPED", "Bu ürünün bu platformda eşlemesi yok.");
        }).RequireRateLimiting("terminal-write");

        return group;
    }

    private static string ChannelOf(string provider) =>
        Channels.TryGetValue(provider, out var channel) ? channel : throw new UnknownOnlinePlatformException(provider);

    private static IResult Refuse(int status, string code, string message) =>
        Results.Json(new { error = new { code, message } }, statusCode: status);

    private static async Task<IReadOnlyList<OnlineMenuProductV1>> ReadProductsAsync(
        NpgsqlDataSource dataSource, string provider, string channel, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            $"""
            SELECT p.product_id, p.name, p.sku, p.current_price, p.active, m.external_sku,
                   (SELECT i.price FROM online_ordering.catalog_publication_items i
                    JOIN online_ordering.catalog_publications c ON c.publication_id = i.publication_id
                    WHERE c.channel = $2 AND i.product_id = p.product_id AND c.status = 'Delivered'
                    ORDER BY c.delivered_at DESC LIMIT 1),
                   a.delivered_quantity
            FROM catalog.products p
            LEFT JOIN online_ordering.provider_product_mappings m
                   ON m.provider = $1 AND m.product_id = p.product_id AND m.effective_to IS NULL
            LEFT JOIN online_ordering.availability_states a ON a.channel = $2 AND a.product_id = p.product_id
            ORDER BY p.active DESC, p.name, p.product_id
            LIMIT {MaxProducts};
            """);
        command.Parameters.AddWithValue(provider);
        command.Parameters.AddWithValue(channel);
        var rows = new List<OnlineMenuProductV1>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new OnlineMenuProductV1(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetDecimal(3), reader.GetBoolean(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                reader.IsDBNull(7) ? OnlineMenuSaleState.Unknown : reader.GetInt32(7) > 0 ? OnlineMenuSaleState.OnSale : OnlineMenuSaleState.SoldOut));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<OnlineMenuCatalogMenuV1>> ReadMenusAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT menu_id, name FROM menu.menus WHERE active ORDER BY name, menu_id LIMIT 100;");
        var menus = new List<OnlineMenuCatalogMenuV1>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            menus.Add(new OnlineMenuCatalogMenuV1(reader.GetGuid(0), reader.GetString(1)));
        return menus;
    }

    private static async Task<IReadOnlyList<OnlineMenuPublicationV1>> ReadPublicationsAsync(
        NpgsqlDataSource dataSource, string channel, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT publication_id, status, requested_at, item_count, jsonb_array_length(validation_errors),
                   last_error IS NOT NULL, delivered_at
            FROM online_ordering.catalog_publications
            WHERE channel = $1
            ORDER BY requested_at DESC, publication_id
            LIMIT 5;
            """);
        command.Parameters.AddWithValue(channel);
        var publications = new List<OnlineMenuPublicationV1>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            publications.Add(new OnlineMenuPublicationV1(
                reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2), reader.GetInt32(3), reader.GetInt32(4),
                reader.GetBoolean(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6)));
        }

        return publications;
    }

    private static async Task<Guid> RequireManagerAsync(
        HttpContext context, Guid terminalId, DualScreenStore store, IAuthorizationService authorization, CancellationToken cancellationToken)
    {
        var cashierToken = context.Request.Cookies[DualScreenApplication.CashierCookieName];
        if (string.IsNullOrWhiteSpace(cashierToken))
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                cashierToken = authHeader["Bearer ".Length..].Trim();
        }

        var principal = await store.AuthenticateCashierAsync(cashierToken, terminalId, cancellationToken)
            ?? throw new DualScreenUnauthorizedException("Cashier authentication is required.");
        await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.IntegrationsManage, cancellationToken);
        return principal.UserId;
    }
}

/// <summary>What the platform shows for a product: on sale, sold out, or not reported yet.</summary>
public static class OnlineMenuSaleState
{
    public const string OnSale = "OnSale";
    public const string SoldOut = "SoldOut";
    public const string Unknown = "Unknown";
}

public sealed record OnlineMenuProductV1(
    Guid ProductId, string Name, string CatalogSku, decimal? CatalogPrice, bool Active, string? ExternalSku, decimal? PublishedPrice, string SaleState);

public sealed record OnlineMenuPlatformProductV1(string Id, string Name, bool Active, Guid? MappedProductId);

public sealed record OnlineMenuCatalogMenuV1(Guid MenuId, string Name);

public sealed record OnlineMenuPublicationV1(
    Guid PublicationId, string Status, DateTimeOffset RequestedAt, int ItemCount, int ValidationErrorCount, bool HasError, DateTimeOffset? DeliveredAt);

public sealed record OnlineMenuV1(
    string Provider,
    string Channel,
    IReadOnlyList<OnlineMenuProductV1> Products,
    IReadOnlyList<OnlineMenuPlatformProductV1>? PlatformProducts,
    bool PlatformMenuUnavailable,
    IReadOnlyList<OnlineMenuCatalogMenuV1> Menus,
    IReadOnlyList<OnlineMenuPublicationV1> Publications);

public sealed record SaveOnlineMappingRequestV1(string? ExternalSku);

public sealed record OnlineMappingV1(Guid ProductId, string ExternalSku);

/// <summary>Turkish messages for every failure; a mapping refusal names its reason.</summary>
public sealed class OnlineMenuExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(5440, nameof(LogRequestFailure)),
            "Online menu request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<OnlineMenuExceptionFilter> _logger;

    public OnlineMenuExceptionFilter(ILogger<OnlineMenuExceptionFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception exception)
        {
            var (status, code, message) = Map(exception);
            if (status >= StatusCodes.Status500InternalServerError)
                LogRequestFailure(_logger, context.HttpContext.Request.Path, context.HttpContext.TraceIdentifier, exception);
            return Results.Json(new { error = new { code, message } }, statusCode: status);
        }
    }

    public static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        DualScreenUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
        AuthorizationDeniedException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
        UnknownOnlinePlatformException => (404, "PLATFORM_NOT_FOUND", "Bu online platform tanınmıyor."),
        ProductMappingRejectedException rejected => (409, "MAPPING_REFUSED", rejected.Rejection switch
        {
            ProductMappingRejection.ProductNotFound => "Ürün bulunamadı.",
            ProductMappingRejection.ProductInactive => "Pasif ürün platformda satılamaz.",
            ProductMappingRejection.ProductRequiresModifierChoice => "Zorunlu seçimi olan ürün bu platformda eksiksiz sipariş edilemez.",
            ProductMappingRejection.LaterMappingExists => "Bu kod için ileri tarihli bir eşleme var.",
            ProductMappingRejection.ProductMappedToAnotherSku => "Bu ürün zaten başka bir platform koduna eşli; önce o eşlemeyi kaldırın.",
            _ => "Eşleme yapılamadı.",
        }),
        InvalidProductMappingRequestException or ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        SensitiveDataEncryptionException => (503, "ENCRYPTION_UNAVAILABLE", "Güvenli depolama şu anda kullanılamıyor."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}
