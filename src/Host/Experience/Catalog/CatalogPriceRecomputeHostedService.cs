using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.Catalog;

/// <summary>
/// Periodically recomputes <c>catalog.products.current_price</c> from the
/// authoritative <c>catalog.product_prices</c> timeline.
///
/// Found by an independent audit (2026-09-07): <c>current_price</c> was only
/// ever refreshed as an inline side effect of <c>POST /prices</c>
/// (<see cref="CatalogManagementStore.CreatePriceAsync"/>), and — before that
/// method's own fix in the same wave — only when the just-inserted row
/// happened to already be effective at that exact instant. Either way, a
/// price scheduled to start in the future never activated, and an expired
/// promotional price kept charging forever, until some UNRELATED later price
/// write happened to touch the same product. That method's own recompute now
/// closes the "a price was just written" case instantly; this worker is what
/// closes the remaining case a write-time fix cannot: a
/// scheduled/expiry boundary crossed with nothing written at all. Assumes a
/// single-currency (TRY) catalog, matching every other read path in this
/// module (<c>catalog.product_prices.currency_code</c> defaults to TRY and no
/// client ever picks another) and the DB's own <c>price_type IN (1)</c> CHECK
/// constraint (only <see cref="PriceType.SalePrice"/> exists today).
/// </summary>
public sealed class CatalogPriceRecomputeHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private static readonly Action<ILogger, Exception?> LogLoopFault =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5400, nameof(LogLoopFault)),
            "Catalog current_price recompute loop iteration failed; retrying after the interval.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CatalogPriceRecomputeHostedService> _logger;

    public CatalogPriceRecomputeHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<CatalogPriceRecomputeHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
                await RecomputeAllAsync(dataSource, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A recompute-loop fault (transient DB) must not kill the
                // worker; current_price simply stays as it was until the
                // next successful tick.
                LogLoopFault(_logger, ex);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Runs one full recompute pass. Public and static so tests can invoke it
    /// deterministically instead of waiting on <see cref="Interval"/>.
    /// </summary>
    public static async Task RecomputeAllAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        await using (var activate = dataSource.CreateCommand(
            """
            UPDATE catalog.products p
            SET current_price = latest.price
            FROM (
                SELECT DISTINCT ON (pp.product_id) pp.product_id, pp.price
                FROM catalog.product_prices pp
                WHERE pp.price_type = 1
                  AND pp.currency_code = 'TRY'
                  AND pp.effective_from <= now()
                  AND (pp.effective_to IS NULL OR pp.effective_to > now())
                ORDER BY pp.product_id, pp.effective_from DESC
            ) latest
            WHERE p.product_id = latest.product_id
              AND p.current_price IS DISTINCT FROM latest.price;
            """))
        {
            await activate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var expire = dataSource.CreateCommand(
            """
            UPDATE catalog.products p
            SET current_price = NULL
            WHERE p.current_price IS NOT NULL
              -- A product that has NEVER had a product_prices row is not
              -- using the dated pricing system at all — its current_price is
              -- a plain seed value set directly on the product record
              -- (CreateProductV1.CurrentPrice) and must be left alone. Only a
              -- product that has actually entered the dated system (>=1 row)
              -- but has none effective right now should be cleared.
              AND EXISTS (
                  SELECT 1 FROM catalog.product_prices pp
                  WHERE pp.product_id = p.product_id
              )
              AND NOT EXISTS (
                  SELECT 1 FROM catalog.product_prices pp
                  WHERE pp.product_id = p.product_id
                    AND pp.price_type = 1
                    AND pp.currency_code = 'TRY'
                    AND pp.effective_from <= now()
                    AND (pp.effective_to IS NULL OR pp.effective_to > now())
              );
            """))
        {
            await expire.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
