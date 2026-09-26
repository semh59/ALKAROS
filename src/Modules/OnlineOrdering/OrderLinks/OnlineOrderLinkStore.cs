using Npgsql;

namespace ALKAROS.OnlineOrdering.OrderLinks;

/// <summary>The platforms an online order can come from, as stored in <c>online_ordering.online_orders.provider</c>.</summary>
public static class OnlineOrderProviders
{
    public const string Yemeksepeti = "yemeksepeti";
}

/// <summary>
/// V12-ONL-006: which platform a local online order came from and under which of that platform's order numbers.
/// A platform number identifies at most one local order (unique per platform); the same number on two platforms is
/// two different orders. Writes join the caller's transaction, so an order and its link commit together.
/// </summary>
public static class OnlineOrderLinkStore
{
    /// <summary>
    /// Links a freshly created local order to its platform order. A second order for the same platform number is
    /// refused by the database (unique violation), so a concurrent path can never create a duplicate.
    /// </summary>
    public static async Task LinkAsync(
        Guid orderId,
        string provider,
        string externalOrderId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("An online order link needs an order id.", nameof(orderId));
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalOrderId);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        await using var command = new NpgsqlCommand(
            "INSERT INTO online_ordering.online_orders (order_id, provider, external_order_id) VALUES ($1, $2, $3);",
            connection, transaction);
        command.Parameters.AddWithValue(orderId);
        command.Parameters.AddWithValue(provider);
        command.Parameters.AddWithValue(externalOrderId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The local order for a platform order number, or null when there is none.</summary>
    public static async Task<Guid?> FindOrderIdAsync(
        string provider,
        string externalOrderId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalOrderId);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        await using var command = new NpgsqlCommand(
            "SELECT order_id FROM online_ordering.online_orders WHERE provider = $1 AND external_order_id = $2;",
            connection, transaction);
        command.Parameters.AddWithValue(provider);
        command.Parameters.AddWithValue(externalOrderId);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as Guid?;
    }
}
