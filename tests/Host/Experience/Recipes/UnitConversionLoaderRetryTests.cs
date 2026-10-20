using ALKAROS.Host.Experience.Recipes;
using ALKAROS.Measurements;
using ALKAROS.Recipes.Units;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ALKAROS.Host.Experience.Recipes.Tests;

public sealed class UnitConversionLoaderRetryTests
{
    [Fact]
    public async Task AStartupLoadThatFailsIsRetriedUntilTheConversionsArrive()
    {
        var converter = new UnitConverter();
        var repository = new FlakyRepository(failures: 2, new UnitConversion(Guid.NewGuid(), "koli", "adet", 12m));
        var loader = new UnitConversionLoaderHostedService(converter, repository, NullLogger<UnitConversionLoaderHostedService>.Instance)
        {
            RetryDelay = TimeSpan.FromMilliseconds(10),
        };

        await loader.StartAsync(CancellationToken.None);
        Assert.Equal(1m, converter.Convert(1m, "koli", "adet"));

        await WaitUntilAsync(() => converter.Convert(1m, "koli", "adet") == 12m);
        await loader.StopAsync(CancellationToken.None);

        Assert.Equal(3, repository.Calls);
    }

    [Fact]
    public async Task StoppingEndsTheRetryLoop()
    {
        var repository = new FlakyRepository(failures: int.MaxValue);
        var loader = new UnitConversionLoaderHostedService(new UnitConverter(), repository, NullLogger<UnitConversionLoaderHostedService>.Instance)
        {
            RetryDelay = TimeSpan.FromMilliseconds(10),
        };

        await loader.StartAsync(CancellationToken.None);
        await loader.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        var callsAtStop = repository.Calls;
        await Task.Delay(100);

        Assert.Equal(callsAtStop, repository.Calls);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The loader did not retry in time.");
            await Task.Delay(10);
        }
    }

    private sealed class FlakyRepository(int failures, params UnitConversion[] stored) : IUnitConversionRepository
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<IReadOnlyList<UnitConversion>> GetActiveConversionsAsync(CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _calls) <= failures)
                throw new TimeoutException("database unavailable");
            return Task.FromResult<IReadOnlyList<UnitConversion>>(stored);
        }

        public Task AddConversionAsync(UnitConversion conversion, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<UnitConversion?> FindConversionAsync(string fromUnitCode, string toUnitCode, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
