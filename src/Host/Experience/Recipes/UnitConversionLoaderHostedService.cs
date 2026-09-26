using ALKAROS.Measurements;
using ALKAROS.Recipes.Units;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.Recipes;

/// <summary>
/// V1-RMD-319 (independent 2026-09-26 audit, finding K7): <c>recipe.unit_conversions</c> rows a manager
/// added through <see cref="RecipeManagementEndpoints"/>'s own POST were persisted correctly, but nothing
/// in the running host ever called <see cref="IUnitConverter.RegisterConversion"/> with them — a
/// repo-wide grep found zero callers before this fix. Worse, <see cref="IUnitConverter"/> was registered
/// <c>Transient</c> (see <c>InventoryModule</c>/<c>RecipesModule</c>), so even a caller that DID register
/// a conversion on one resolved instance would have left every OTHER resolution (a fresh
/// <see cref="UnitConverter"/> with only <see cref="StandardUnits"/> loaded) none the wiser. Custom
/// conversions (e.g. "1 koli = 12 adet") sat in the database, fully persisted and listable, but were
/// silently never applied anywhere real (goods receipt, stock counts, production consumption) - a same-dimension (Count)
/// mismatch quietly fell back to an incorrect 1:1 conversion instead.
///
/// This service is the one place that loads them into the now-<c>Singleton</c> <see cref="IUnitConverter"/>
/// at startup. A conversion added AFTER startup takes effect immediately too —
/// <see cref="RecipeManagementEndpoints"/>'s own POST handler calls <see cref="IUnitConverter.RegisterConversion"/>
/// on the same shared singleton right after persisting, so no restart is needed going forward.
/// </summary>
public sealed class UnitConversionLoaderHostedService : IHostedService
{
    private static readonly Action<ILogger, string, string, Exception> LogRejectedConversion =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5910, nameof(LogRejectedConversion)),
            "Stored unit conversion '{FromUnitCode}' -> '{ToUnitCode}' was rejected while loading into the " +
            "runtime converter; it stays persisted but unapplied until the contradiction is resolved.");

    private static readonly Action<ILogger, Exception> LogLoadFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5911, nameof(LogLoadFailed)),
            "Failed to load stored unit conversions into the runtime converter at startup; the Host still " +
            "starts (a missing custom conversion is the same broken state this fix corrects, not worse), " +
            "but no custom conversion is usable until the next successful load.");

    private readonly IUnitConverter _converter;
    private readonly IUnitConversionRepository _conversions;
    private readonly ILogger<UnitConversionLoaderHostedService> _logger;

    public UnitConversionLoaderHostedService(
        IUnitConverter converter,
        IUnitConversionRepository conversions,
        ILogger<UnitConversionLoaderHostedService> logger)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _conversions = conversions ?? throw new ArgumentNullException(nameof(conversions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // The Microsoft.Extensions.Hosting host AWAITS every registered IHostedService.StartAsync before
        // the app is considered started - a transient database outage at exactly the wrong moment must
        // never block the whole Host from serving any other, unrelated request just to load a nice-to-have
        // list of custom unit conversions. Caught broadly (not just NpgsqlException) because this is a
        // best-effort warm-up, not a required dependency the rest of the app relies on synchronously.
        IReadOnlyList<UnitConversion> active;
        try
        {
            active = await _conversions.GetActiveConversionsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or OperationCanceledException)
        {
            LogLoadFailed(_logger, ex);
            return;
        }

        foreach (var conversion in active)
        {
            try
            {
                _converter.RegisterConversion(conversion.FromUnitCode, conversion.ToUnitCode, conversion.Factor);
            }
            catch (Exception ex) when (ex is ContradictoryUnitConversionException or InvalidUnitConversionFactorException)
            {
                // Never let one bad/contradictory stored row (e.g. two rows written before this fix
                // existed, with no runtime check to catch the conflict at write time) crash the whole
                // Host on every future startup - log it loudly and keep loading the rest.
                LogRejectedConversion(_logger, conversion.FromUnitCode, conversion.ToUnitCode, ex);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
