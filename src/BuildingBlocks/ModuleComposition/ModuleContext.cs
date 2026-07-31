namespace ALKAROS.ModuleComposition;

/// <summary>
/// Minimal registration surface passed to <see cref="IModule.Register"/> so
/// modules can declare their services without taking a hard dependency on any
/// specific DI container. The host supplies the concrete adapter.
/// </summary>
public sealed class ModuleContext
{
    private readonly List<ServiceDescriptor> _services = new();

    public IReadOnlyList<ServiceDescriptor> Services => _services;

    public ModuleContext RegisterSingleton<TService>(TService instance)
        where TService : notnull
    {
        _services.Add(ServiceDescriptor.Singleton(instance));
        return this;
    }

    public ModuleContext RegisterSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        _services.Add(ServiceDescriptor.Singleton<TService, TImplementation>());
        return this;
    }

    public ModuleContext RegisterTransient<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        _services.Add(ServiceDescriptor.Transient<TService, TImplementation>());
        return this;
    }

    public sealed record ServiceDescriptor(
        string ServiceFullName,
        string ImplementationFullName,
        ServiceLifetime Lifetime)
    {
        public static ServiceDescriptor Singleton<TService>(TService instance)
            where TService : notnull
            => new(typeof(TService).FullName!, instance.GetType().FullName!, ServiceLifetime.Singleton);

        public static ServiceDescriptor Singleton<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
            => new(typeof(TService).FullName!, typeof(TImplementation).FullName!, ServiceLifetime.Singleton);

        public static ServiceDescriptor Transient<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
            => new(typeof(TService).FullName!, typeof(TImplementation).FullName!, ServiceLifetime.Transient);
    }

    public enum ServiceLifetime { Singleton, Transient }
}