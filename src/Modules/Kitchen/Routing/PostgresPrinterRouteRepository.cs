namespace ALKAROS.Kitchen.Routing;

using Npgsql;

public sealed class PostgresPrinterRouteRepository : IPrinterRouteRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPrinterRouteRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<PrinterRoute?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, route_level, printer_id, item_id, product_id, category_id, special_date, is_active, created_at, updated_at
            FROM kitchen.printer_routes
            WHERE id = @id;
            """;
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return MapPrinterRoute(reader);
    }

    public async Task<IReadOnlyList<PrinterRoute>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, route_level, printer_id, item_id, product_id, category_id, special_date, is_active, created_at, updated_at
            FROM kitchen.printer_routes
            ORDER BY route_level, created_at;
            """;

        var list = new List<PrinterRoute>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(MapPrinterRoute(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<PrinterRoute>> GetActiveRoutesAsync(CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, route_level, printer_id, item_id, product_id, category_id, special_date, is_active, created_at, updated_at
            FROM kitchen.printer_routes
            WHERE is_active = TRUE
            ORDER BY route_level, created_at;
            """;

        var list = new List<PrinterRoute>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(MapPrinterRoute(reader));
        }

        return list;
    }

    public async Task SaveRouteAsync(PrinterRoute route, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(route);

        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO kitchen.printer_routes (
                id, route_level, printer_id, item_id, product_id, category_id, special_date, is_active, created_at, updated_at
            ) VALUES (
                @id, @route_level, @printer_id, @item_id, @product_id, @category_id, @special_date, @is_active, @created_at, @updated_at
            )
            ON CONFLICT (id) DO UPDATE SET
                route_level = EXCLUDED.route_level,
                printer_id = EXCLUDED.printer_id,
                item_id = EXCLUDED.item_id,
                product_id = EXCLUDED.product_id,
                category_id = EXCLUDED.category_id,
                special_date = EXCLUDED.special_date,
                is_active = EXCLUDED.is_active,
                updated_at = EXCLUDED.updated_at;
            """;

        AddRouteParameters(cmd, route);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task SaveRoutesAtomicallyAsync(IReadOnlyList<PrinterRoute> routes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(routes);

        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        foreach (var route in routes)
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO kitchen.printer_routes (
                    id, route_level, printer_id, item_id, product_id, category_id, special_date, is_active, created_at, updated_at
                ) VALUES (
                    @id, @route_level, @printer_id, @item_id, @product_id, @category_id, @special_date, @is_active, @created_at, @updated_at
                )
                ON CONFLICT (id) DO UPDATE SET
                    route_level = EXCLUDED.route_level,
                    printer_id = EXCLUDED.printer_id,
                    item_id = EXCLUDED.item_id,
                    product_id = EXCLUDED.product_id,
                    category_id = EXCLUDED.category_id,
                    special_date = EXCLUDED.special_date,
                    is_active = EXCLUDED.is_active,
                    updated_at = EXCLUDED.updated_at;
                """;

            AddRouteParameters(cmd, route);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteRouteAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM kitchen.printer_routes WHERE id = @id;";
        cmd.Parameters.AddWithValue("id", id);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static void AddRouteParameters(NpgsqlCommand cmd, PrinterRoute route)
    {
        cmd.Parameters.AddWithValue("id", route.Id);
        cmd.Parameters.AddWithValue("route_level", route.RouteLevel.ToString());
        cmd.Parameters.AddWithValue("printer_id", route.PrinterId);
        cmd.Parameters.AddWithValue("item_id", (object?)route.ItemId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("product_id", (object?)route.ProductId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("category_id", (object?)route.CategoryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("special_date", (object?)route.SpecialDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("is_active", route.IsActive);
        cmd.Parameters.AddWithValue("created_at", route.CreatedAt);
        cmd.Parameters.AddWithValue("updated_at", (object?)route.UpdatedAt ?? DBNull.Value);
    }

    private static PrinterRoute MapPrinterRoute(NpgsqlDataReader reader)
    {
        var level = Enum.Parse<RouteLevel>(reader.GetString(1));
        return new PrinterRoute(
            id: reader.GetGuid(0),
            routeLevel: level,
            printerId: reader.GetGuid(2),
            itemId: reader.IsDBNull(3) ? null : reader.GetGuid(3),
            productId: reader.IsDBNull(4) ? null : reader.GetGuid(4),
            categoryId: reader.IsDBNull(5) ? null : reader.GetGuid(5),
            specialDate: reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6),
            isActive: reader.GetBoolean(7),
            createdAt: reader.GetFieldValue<DateTimeOffset>(8),
            updatedAt: reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9));
    }
}
