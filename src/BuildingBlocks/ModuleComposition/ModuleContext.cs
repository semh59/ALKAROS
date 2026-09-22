namespace ALKAROS.ModuleComposition;

/// <summary>
/// Minimal registration surface passed to <see cref="IModule.Register"/> so
/// modules can declare their services without taking a hard dependency on any
/// specific DI container. The host supplies the concrete adapter.
/// </summary>
public sealed class ModuleContext
{
    private readonly List<ServiceDescriptor> _services = new();

    public IReadOnlyList<ServiceDescriptor> Services => _services.AsReadOnly();

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

    /// <summary>
    /// Registers a singleton built from a factory that may itself resolve
    /// other DI-registered dependencies — the escape hatch for an
    /// implementation whose constructor takes a primitive (a connection
    /// string, a file-system path) that <see cref="RegisterSingleton{TService,TImplementation}"/>'s
    /// plain type-to-type mapping cannot supply.
    /// </summary>
    public ModuleContext RegisterSingleton<TService>(Func<IServiceProvider, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        _services.Add(ServiceDescriptor.SingletonFactory(factory));
        return this;
    }

    /// <summary>Transient counterpart of <see cref="RegisterSingleton{TService}(Func{IServiceProvider, TService})"/>.</summary>
    public ModuleContext RegisterTransient<TService>(Func<IServiceProvider, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        _services.Add(ServiceDescriptor.TransientFactory(factory));
        return this;
    }

    public sealed record ServiceDescriptor(
        Type ServiceType,
        Type ImplementationType,
        ServiceLifetime Lifetime,
        object? ImplementationInstance,
        Func<IServiceProvider, object>? ImplementationFactory = null)
    {
        public static ServiceDescriptor Singleton<TService>(TService instance)
            where TService : notnull
            => new(typeof(TService), instance.GetType(), ServiceLifetime.Singleton, instance);

        public static ServiceDescriptor Singleton<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
            => new(typeof(TService), typeof(TImplementation), ServiceLifetime.Singleton, null);

        public static ServiceDescriptor Transient<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
            => new(typeof(TService), typeof(TImplementation), ServiceLifetime.Transient, null);

        public static ServiceDescriptor SingletonFactory<TService>(Func<IServiceProvider, TService> factory)
            where TService : class
            => new(typeof(TService), typeof(TService), ServiceLifetime.Singleton, null, sp => factory(sp));

        public static ServiceDescriptor TransientFactory<TService>(Func<IServiceProvider, TService> factory)
            where TService : class
            => new(typeof(TService), typeof(TService), ServiceLifetime.Transient, null, sp => factory(sp));
    }

    public enum ServiceLifetime { Singleton, Transient }
}
