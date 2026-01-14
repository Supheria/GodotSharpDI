namespace GodotSharp.DI;

[AttributeUsage(AttributeTargets.Constructor)]
public sealed class InjectionConstructorAttribute : Attribute;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class DependencyAttribute : Attribute;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false
)]
public sealed class SingletonServiceAttribute : Attribute
{
    public Type[] ServiceTypes { get; }

    public SingletonServiceAttribute(params Type[] serviceTypes)
    {
        ServiceTypes = serviceTypes;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TransientServiceAttribute : Attribute
{
    public Type[] ServiceTypes { get; }

    public TransientServiceAttribute(params Type[] serviceTypes)
    {
        ServiceTypes = serviceTypes;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ServiceHostAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ServiceUserAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ServiceModulesAttribute : Attribute
{
    public Type[] Instantiate { get; set; } = [];
    public Type[] Expect { get; set; } = [];
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class AutoScanServiceModulesAttribute : Attribute;

public interface IServiceHost;

public interface IServiceUser;

public interface IServiceScope
{
    void RegisterService<T>(T instance)
        where T : notnull { } // no default block in real project
    void UnregisterService<T>()
        where T : notnull { } // no default block in real project
    void ResolveDependency<T>(Action<T> onResolved)
        where T : notnull { } // no default block in real project
}

public interface IServiceAware
{
    void OnServicesReady();
}
