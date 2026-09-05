using System.Globalization;
using System.Text.Json;
using ALKAROS.Catalog.Pricing;
using ALKAROS.Catalog.ProductCatalog;
using Npgsql;

namespace ALKAROS.Host.Experience.Catalog;

public sealed class CatalogManagementStore
{
    private const int MaximumCursorLength = 1024;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ICategoryRepository _categories;
    private readonly ITaxProfileRepository _taxProfiles;
    private readonly IProductRepository _products;
    private readonly IModifierGroupRepository _modifierGroups;
    private readonly IModifierRepository _modifiers;
    private readonly IProductModifierGroupRepository _assignments;
    private readonly IPricingRepository _prices;

    public CatalogManagementStore(
        NpgsqlDataSource dataSource,
        ICategoryRepository categories,
        ITaxProfileRepository taxProfiles,
        IProductRepository products,
        IModifierGroupRepository modifierGroups,
        IModifierRepository modifiers,
        IProductModifierGroupRepository assignments,
        IPricingRepository prices)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _categories = categories ?? throw new ArgumentNullException(nameof(categories));
        _taxProfiles = taxProfiles ?? throw new ArgumentNullException(nameof(taxProfiles));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _modifierGroups = modifierGroups ?? throw new ArgumentNullException(nameof(modifierGroups));
        _modifiers = modifiers ?? throw new ArgumentNullException(nameof(modifiers));
        _assignments = assignments ?? throw new ArgumentNullException(nameof(assignments));
        _prices = prices ?? throw new ArgumentNullException(nameof(prices));
    }

    public async Task<CategoryV1> CreateCategoryAsync(
        CreateCategoryV1 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureIdentifier(request.Id, nameof(request.Id));
        if (request.ParentId == request.Id)
            throw new ArgumentException("A category cannot be its own parent.", nameof(request));
        var category = new Category(
            request.Id,
            NormalizeRequired(request.Code, 50, nameof(request.Code)),
            NormalizeRequired(request.Name, 200, nameof(request.Name)),
            request.ParentId,
            request.SortOrder,
            request.Active);
        await _categories.AddAsync(category, cancellationToken);
        return ToDto(category);
    }

    public async Task<TaxProfileV1> CreateTaxProfileAsync(
        CreateTaxProfileV1 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureIdentifier(request.Id, nameof(request.Id));
        var profile = new TaxProfile(
            request.Id,
            NormalizeRequired(request.Code, 50, nameof(request.Code)),
            NormalizeRequired(request.Name, 200, nameof(request.Name)),
            request.VatRate,
            request.Active);
        await _taxProfiles.AddAsync(profile, cancellationToken);
        return ToDto(profile);
    }

    public async Task<ProductV1> CreateProductAsync(
        CreateProductV1 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureIdentifier(request.Id, nameof(request.Id));
        EnsureDefined(request.ProductType, nameof(request.ProductType));
        EnsureDefined(request.StockMode, nameof(request.StockMode));
        var product = new Product(
            request.Id,
            NormalizeRequired(request.Sku, 100, nameof(request.Sku)),
            NormalizeRequired(request.Name, 300, nameof(request.Name)),
            request.ProductType,
            request.StockMode,
            request.CategoryId,
            request.TaxProfileId,
            NormalizeOptional(request.Description, null, nameof(request.Description)),
            NormalizeOptional(request.PrinterRoutePolicy, 200, nameof(request.PrinterRoutePolicy)),
            request.DisplayOrder,
            request.CurrentPrice,
            request.Active,
            request.IsAvailable);
        await _products.AddAsync(product, cancellationToken);
        return ToDto(product);
    }

    public async Task<ProductV1?> SetProductAvailabilityAsync(
        Guid productId,
        SetProductAvailabilityV1 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureIdentifier(productId, nameof(productId));
        var product = await _products.GetByIdAsync(productId, cancellationToken);
        if (product is null)
            return null;
        var updated = request.IsAvailable ? product.Restore() : product.Suspend();
        await _products.UpdateAsync(updated, product.RowVersion, cancellationToken);
        return ToDto(updated);
    }

    public async Task<ModifierGroupV1> CreateModifierGroupAsync(
        CreateModifierGroupV1 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureIdentifier(request.Id, nameof(request.Id));
        EnsureDefined(request.SelectionType, nameof(request.SelectionType));
        var group = new ModifierGroup(
            request.Id,
            NormalizeRequired(request.Code, 50, nameof(request.Code)),
            NormalizeRequired(request.Name, 200, nameof(request.Name)),
            request.SelectionType,
            request.MinSelections,
            request.MaxSelections,
            request.Active);
        await _modifierGroups.AddAsync(group, cancellationToken);
        return ToDto(group);
    }

    public async Task<ModifierV1> CreateModifierAsync(
        CreateModifierV1 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureIdentifier(request.Id, nameof(request.Id));
        EnsureIdentifier(request.ModifierGroupId, nameof(request.ModifierGroupId));
        var modifier = new Modifier(
            request.Id,
            request.ModifierGroupId,
            NormalizeRequired(request.Code, 50, nameof(request.Code)),
            NormalizeRequired(request.Name, 200, nameof(request.Name)),
            request.PriceDelta,
            request.ProductId,
            request.Active);
        await _modifiers.AddAsync(modifier, cancellationToken);
        return ToDto(modifier);
    }

    public async Task<ProductModifierAssignmentV1> CreateAssignmentAsync(
        CreateProductModifierAssignmentV1 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureIdentifier(request.Id, nameof(request.Id));
        EnsureIdentifier(request.ProductId, nameof(request.ProductId));
        EnsureIdentifier(request.ModifierGroupId, nameof(request.ModifierGroupId));
        var assignment = new ProductModifierGroup(request.Id, request.ProductId, request.ModifierGroupId);
        await _assignments.AddAsync(assignment, cancellationToken);
        return ToDto(assignment);
    }

    public async Task<ProductPriceV1> CreatePriceAsync(
        CreateProductPriceV1 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureDefined(request.PriceType, nameof(request.PriceType));
        var price = new ProductPrice(
            request.Id,
            request.ProductId,
            request.PriceType,
            request.Price,
            request.EffectiveFrom,
            NormalizeCurrency(request.CurrencyCode),
            request.EffectiveTo);
        await _prices.AddAsync(price, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (request.PriceType == PriceType.SalePrice
            && request.EffectiveFrom <= now
            && (request.EffectiveTo == null || request.EffectiveTo > now))
        {
            await using var cmd = _dataSource.CreateCommand(
                """
                UPDATE catalog.products
                SET current_price = @current_price
                WHERE product_id = @product_id;
                """);
            cmd.Parameters.AddWithValue("current_price", request.Price);
            cmd.Parameters.AddWithValue("product_id", request.ProductId);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        return ToDto(price);
    }

    public async Task<ProductPriceV1?> GetEffectivePriceAsync(
        Guid productId,
        PriceType priceType,
        string currencyCode,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        EnsureIdentifier(productId, nameof(productId));
        EnsureDefined(priceType, nameof(priceType));
        var price = await _prices.GetEffectivePriceAsync(
            productId,
            priceType,
            NormalizeCurrency(currencyCode),
            at,
            cancellationToken);
        return price is null ? null : ToDto(price);
    }

    public async Task<CatalogPageV1<CategoryV1>> ListCategoriesAsync(
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var decoded = DecodeCodeCursor(cursor, "categories");
        await using var command = _dataSource.CreateCommand(
            """
            SELECT category_id, code, name, parent_category_id, sort_order, active
            FROM catalog.categories
            WHERE NOT @has_cursor
               OR (code COLLATE "C", category_id) > (@cursor_code COLLATE "C", @cursor_id)
            ORDER BY code COLLATE "C", category_id
            LIMIT @fetch_limit;
            """);
        BindCodeCursor(command, decoded, limit);
        return await ReadPageAsync(
            command,
            limit,
            reader => new CategoryV1(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.GetInt32(4), reader.GetBoolean(5)),
            item => EncodeCursor(new CursorPayload("categories", item.Code, null, item.Id)),
            cancellationToken);
    }

    public async Task<CatalogPageV1<TaxProfileV1>> ListTaxProfilesAsync(
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var decoded = DecodeCodeCursor(cursor, "tax-profiles");
        await using var command = _dataSource.CreateCommand(
            """
            SELECT tax_profile_id, code, name, vat_rate, active
            FROM catalog.tax_profiles
            WHERE NOT @has_cursor
               OR (code COLLATE "C", tax_profile_id) > (@cursor_code COLLATE "C", @cursor_id)
            ORDER BY code COLLATE "C", tax_profile_id
            LIMIT @fetch_limit;
            """);
        BindCodeCursor(command, decoded, limit);
        return await ReadPageAsync(
            command,
            limit,
            reader => new TaxProfileV1(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetBoolean(4)),
            item => EncodeCursor(new CursorPayload("tax-profiles", item.Code, null, item.Id)),
            cancellationToken);
    }

    public async Task<CatalogPageV1<ProductV1>> ListProductsAsync(
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var decoded = DecodeCodeCursor(cursor, "products");
        await using var command = _dataSource.CreateCommand(
            """
            SELECT product_id, sku, name, product_type, stock_mode, category_id, tax_profile_id,
                   description, printer_route_policy, display_order, current_price, active, is_available
            FROM catalog.products
            WHERE NOT @has_cursor
               OR (sku COLLATE "C", product_id) > (@cursor_code COLLATE "C", @cursor_id)
            ORDER BY sku COLLATE "C", product_id
            LIMIT @fetch_limit;
            """);
        BindCodeCursor(command, decoded, limit);
        return await ReadPageAsync(
            command,
            limit,
            reader => new ProductV1(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                (ProductType)reader.GetInt16(3), (StockMode)reader.GetInt16(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                reader.IsDBNull(6) ? null : reader.GetGuid(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetInt32(9), reader.IsDBNull(10) ? null : reader.GetDecimal(10), reader.GetBoolean(11),
                reader.GetBoolean(12)),
            item => EncodeCursor(new CursorPayload("products", item.Sku, null, item.Id)),
            cancellationToken);
    }

    public async Task<CatalogPageV1<ModifierGroupV1>> ListModifierGroupsAsync(
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var decoded = DecodeCodeCursor(cursor, "modifier-groups");
        await using var command = _dataSource.CreateCommand(
            """
            SELECT modifier_group_id, code, name, selection_type, min_selections, max_selections, active
            FROM catalog.modifier_groups
            WHERE NOT @has_cursor
               OR (code COLLATE "C", modifier_group_id) > (@cursor_code COLLATE "C", @cursor_id)
            ORDER BY code COLLATE "C", modifier_group_id
            LIMIT @fetch_limit;
            """);
        BindCodeCursor(command, decoded, limit);
        return await ReadPageAsync(
            command,
            limit,
            reader => new ModifierGroupV1(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                (SelectionType)reader.GetInt16(3), reader.GetInt16(4), reader.GetInt16(5), reader.GetBoolean(6)),
            item => EncodeCursor(new CursorPayload("modifier-groups", item.Code, null, item.Id)),
            cancellationToken);
    }

    public async Task<CatalogPageV1<ModifierV1>> ListModifiersAsync(
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var decoded = DecodeCodeCursor(cursor, "modifiers");
        await using var command = _dataSource.CreateCommand(
            """
            SELECT modifier_id, modifier_group_id, code, name, price_delta, product_id, active
            FROM catalog.modifiers
            WHERE NOT @has_cursor
               OR (code COLLATE "C", modifier_id) > (@cursor_code COLLATE "C", @cursor_id)
            ORDER BY code COLLATE "C", modifier_id
            LIMIT @fetch_limit;
            """);
        BindCodeCursor(command, decoded, limit);
        return await ReadPageAsync(
            command,
            limit,
            reader => new ModifierV1(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.GetBoolean(6)),
            item => EncodeCursor(new CursorPayload("modifiers", item.Code, null, item.Id)),
            cancellationToken);
    }

    public async Task<CatalogPageV1<ProductModifierAssignmentV1>> ListAssignmentsAsync(
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var decoded = DecodeCursor(cursor, "assignments");
        var productId = decoded is null ? Guid.Empty : ParseCursorGuid(decoded.Key1);
        var modifierGroupId = decoded is null ? Guid.Empty : ParseCursorGuid(decoded.Key2);
        await using var command = _dataSource.CreateCommand(
            """
            SELECT product_modifier_group_id, product_id, modifier_group_id
            FROM catalog.product_modifier_groups
            WHERE NOT @has_cursor
               OR (product_id, modifier_group_id, product_modifier_group_id) >
                  (@cursor_product_id, @cursor_modifier_group_id, @cursor_id)
            ORDER BY product_id, modifier_group_id, product_modifier_group_id
            LIMIT @fetch_limit;
            """);
        command.Parameters.AddWithValue("has_cursor", decoded is not null);
        command.Parameters.AddWithValue("cursor_product_id", productId);
        command.Parameters.AddWithValue("cursor_modifier_group_id", modifierGroupId);
        command.Parameters.AddWithValue("cursor_id", decoded?.Id ?? Guid.Empty);
        command.Parameters.AddWithValue("fetch_limit", limit + 1);
        return await ReadPageAsync(
            command,
            limit,
            reader => new ProductModifierAssignmentV1(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2)),
            item => EncodeCursor(new CursorPayload(
                "assignments", item.ProductId.ToString("D"), item.ModifierGroupId.ToString("D"), item.Id)),
            cancellationToken);
    }

    public async Task<CatalogPageV1<ProductPriceV1>> ListPricesAsync(
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var decoded = DecodeCursor(cursor, "prices");
        var productId = decoded is null ? Guid.Empty : ParseCursorGuid(decoded.Key1);
        var effectiveFrom = decoded is null
            ? DateTimeOffset.MinValue
            : ParseCursorTimestamp(decoded.Key2);
        await using var command = _dataSource.CreateCommand(
            """
            SELECT product_price_id, product_id, price_type, price, currency_code, effective_from, effective_to
            FROM catalog.product_prices
            WHERE NOT @has_cursor
               OR (product_id, effective_from, product_price_id) >
                  (@cursor_product_id, @cursor_effective_from, @cursor_id)
            ORDER BY product_id, effective_from, product_price_id
            LIMIT @fetch_limit;
            """);
        command.Parameters.AddWithValue("has_cursor", decoded is not null);
        command.Parameters.AddWithValue("cursor_product_id", productId);
        command.Parameters.AddWithValue("cursor_effective_from", effectiveFrom);
        command.Parameters.AddWithValue("cursor_id", decoded?.Id ?? Guid.Empty);
        command.Parameters.AddWithValue("fetch_limit", limit + 1);
        return await ReadPageAsync(
            command,
            limit,
            reader => new ProductPriceV1(
                reader.GetGuid(0), reader.GetGuid(1), (PriceType)reader.GetInt16(2), reader.GetDecimal(3),
                reader.GetString(4).Trim(), reader.GetFieldValue<DateTimeOffset>(5),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6)),
            item => EncodeCursor(new CursorPayload(
                "prices", item.ProductId.ToString("D"), item.EffectiveFrom.ToString("O"), item.Id)),
            cancellationToken);
    }

    private static async Task<CatalogPageV1<T>> ReadPageAsync<T>(
        NpgsqlCommand command,
        int limit,
        Func<NpgsqlDataReader, T> read,
        Func<T, string> encodeCursor,
        CancellationToken cancellationToken)
    {
        EnsurePageLimit(limit);
        var items = new List<T>(limit + 1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(read(reader));
        var hasMore = items.Count > limit;
        if (hasMore)
            items.RemoveAt(items.Count - 1);
        return new CatalogPageV1<T>(items, hasMore ? encodeCursor(items[^1]) : null);
    }

    private static void BindCodeCursor(NpgsqlCommand command, CursorPayload? cursor, int limit)
    {
        EnsurePageLimit(limit);
        command.Parameters.AddWithValue("has_cursor", cursor is not null);
        command.Parameters.AddWithValue("cursor_code", cursor?.Key1 ?? string.Empty);
        command.Parameters.AddWithValue("cursor_id", cursor?.Id ?? Guid.Empty);
        command.Parameters.AddWithValue("fetch_limit", limit + 1);
    }

    private static CursorPayload? DecodeCodeCursor(string? value, string kind)
    {
        var cursor = DecodeCursor(value, kind);
        if (cursor is not null && (cursor.Key1.Length is < 1 or > 300 || cursor.Key2 is not null))
            throw new ArgumentException("The catalog cursor is invalid.", nameof(value));
        return cursor;
    }

    private static CursorPayload? DecodeCursor(string? value, string kind)
    {
        if (value is null)
            return null;
        if (value.Length is < 1 or > MaximumCursorLength)
            throw new ArgumentException("The catalog cursor is invalid.", nameof(value));

        try
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            base64 += (base64.Length % 4) switch
            {
                0 => string.Empty,
                2 => "==",
                3 => "=",
                _ => throw new FormatException("Cursor padding is invalid."),
            };
            var payload = JsonSerializer.Deserialize<CursorPayload>(Convert.FromBase64String(base64))
                ?? throw new JsonException("Cursor payload is empty.");
            if (!string.Equals(payload.Kind, kind, StringComparison.Ordinal)
                || string.IsNullOrEmpty(payload.Key1)
                || payload.Id == Guid.Empty)
            {
                throw new JsonException("Cursor payload is invalid.");
            }
            return payload;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The catalog cursor is invalid.", nameof(value), exception);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The catalog cursor is invalid.", nameof(value), exception);
        }
    }

    private static string EncodeCursor(CursorPayload payload)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload);
        return Convert.ToBase64String(json).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static Guid ParseCursorGuid(string? value)
        => Guid.TryParseExact(value, "D", out var parsed) && parsed != Guid.Empty
            ? parsed
            : throw new ArgumentException("The catalog cursor is invalid.", nameof(value));

    private static DateTimeOffset ParseCursorTimestamp(string? value)
        => DateTimeOffset.TryParseExact(
            value,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : throw new ArgumentException("The catalog cursor is invalid.", nameof(value));

    private static void EnsurePageLimit(int limit)
    {
        if (limit is < 1 or > CatalogManagementEndpoints.MaximumPageSize)
            throw new ArgumentOutOfRangeException(nameof(limit));
    }

    private static void EnsureIdentifier(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
    }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(parameterName, value, "Enumeration value is not supported.");
    }

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int? maximumLength, string parameterName)
    {
        if (value is null)
            return null;
        var normalized = value.Trim();
        if (normalized.Length == 0)
            return null;
        if (maximumLength is not null && normalized.Length > maximumLength.Value)
            throw new ArgumentException($"Value cannot exceed {maximumLength.Value} characters.", parameterName);
        return normalized;
    }

    private static string NormalizeCurrency(string value)
    {
        var normalized = NormalizeRequired(value, 3, nameof(value)).ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
            throw new ArgumentException("Currency code must contain three ASCII letters.", nameof(value));
        return normalized;
    }

    private static CategoryV1 ToDto(Category value)
        => new(value.Id, value.Code, value.Name, value.ParentId, value.SortOrder, value.Active);

    private static TaxProfileV1 ToDto(TaxProfile value)
        => new(value.Id, value.Code, value.Name, value.VatRate, value.Active);

    private static ProductV1 ToDto(Product value)
        => new(
            value.Id,
            value.Sku,
            value.Name,
            value.ProductType,
            value.StockMode,
            value.CategoryId,
            value.TaxProfileId,
            value.Description,
            value.PrinterRoutePolicy,
            value.DisplayOrder,
            value.CurrentPrice,
            value.Active,
            value.IsAvailable);

    private static ModifierGroupV1 ToDto(ModifierGroup value)
        => new(
            value.Id,
            value.Code,
            value.Name,
            value.SelectionType,
            value.MinSelections,
            value.MaxSelections,
            value.Active);

    private static ModifierV1 ToDto(Modifier value)
        => new(
            value.Id,
            value.ModifierGroupId,
            value.Code,
            value.Name,
            value.PriceDelta,
            value.ProductId,
            value.Active);

    private static ProductModifierAssignmentV1 ToDto(ProductModifierGroup value)
        => new(value.Id, value.ProductId, value.ModifierGroupId);

    private static ProductPriceV1 ToDto(ProductPrice value)
        => new(
            value.Id,
            value.ProductId,
            value.PriceType,
            value.Price,
            value.CurrencyCode,
            value.EffectiveFrom,
            value.EffectiveTo);

    private sealed record CursorPayload(string Kind, string Key1, string? Key2, Guid Id);
}
