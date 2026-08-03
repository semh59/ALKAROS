using ALKAROS.Host.Composition;
using ALKAROS.ModuleComposition;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Tests.Composition;

/// <summary>
/// Verifies that the host applies the concrete service registrations
/// produced by module composition to its dependency container (kusur 1):
/// singleton instances, singleton types and transient types are resolvable
/// through the built provider, and a failing module registration fails the
/// composition fail-closed.
/// </summary>
public sealed class HostServiceRegistrationTests
{
    [Fact]
    public void ComposeModulesAppliesModuleRegistrationsToTheContainer()
    {
        using var provider = HostComposition.ComposeModules(
            TextWriter.Null,
            moduleTypes: [typeof(ServiceModule)]);

        Assert.NotNull(provider);
        Assert.Contains(
            provider.GetServices<ISingletonService>(),
            service => ReferenceEquals(service, ServiceModule.SingletonInstance));
        Assert.IsType<SingletonService>(provider.GetRequiredService<ISingletonService>());

        var firstTransient = provider.GetRequiredService<ITransientService>();
        var secondTransient = provider.GetRequiredService<ITransientService>();
        Assert.NotSame(firstTransient, secondTransient);
    }

    [Fact]
    public void ComposeModulesFailsClosedWhenModuleRegistrationThrows()
    {
        using var output = new StringWriter();
        var provider = HostComposition.ComposeModules(
            output,
            moduleTypes: [typeof(ThrowingModule)]);

        Assert.Null(provider);
        Assert.Contains("Module composition failed: registration failed", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeModulesReportsZeroServicesWhenNoModuleRegisters()
    {
        using var output = new StringWriter();
        using var provider = HostComposition.ComposeModules(
            output,
            moduleTypes: [typeof(EmptyModule)]);

        Assert.NotNull(provider);
        Assert.Contains("Modules composed: none registered.", output.ToString(), StringComparison.Ordinal);
    }

    private interface ISingletonService
    {
    }

    private sealed class SingletonService : ISingletonService
    {
    }

    private interface ITransientService
    {
    }

    private sealed class TransientService : ITransientService
    {
    }

    private sealed class ServiceModule : IModule
    {
        public static readonly SingletonService SingletonInstance = new();

        public string Id => "ServiceModule";
        public string DisplayName => "ServiceModule";
        public IReadOnlyCollection<string> DependsOn => Array.Empty<string>();

        public void Register(ModuleContext context) => context
            .RegisterSingleton<ISingletonService>(SingletonInstance)
            .RegisterSingleton<ISingletonService, SingletonService>()
            .RegisterTransient<ITransientService, TransientService>();
    }

    private sealed class ThrowingModule : IModule
    {
        public string Id => "ThrowingModule";
        public string DisplayName => "ThrowingModule";
        public IReadOnlyCollection<string> DependsOn => Array.Empty<string>();

        public void Register(ModuleContext context)
            => throw new InvalidOperationException("registration failed");
    }

    private sealed class EmptyModule : IModule
    {
        public string Id => "EmptyModule";
        public string DisplayName => "EmptyModule";
        public IReadOnlyCollection<string> DependsOn => Array.Empty<string>();

        public void Register(ModuleContext context)
        {
        }
    }
}
