namespace GodotSharp.DI;

[AttributeUsage(AttributeTargets.Constructor)]
public sealed class InjectConstructorAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class InjectAttribute : Attribute { }

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false,
    AllowMultiple = false
)]
public sealed class SingletonAttribute : Attribute
{
    public Type[] ServiceTypes { get; }

    public SingletonAttribute(params Type[] serviceTypes)
    {
        ServiceTypes = serviceTypes;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class TransientAttribute : Attribute
{
    public Type[] ServiceTypes { get; }

    public TransientAttribute(params Type[] serviceTypes)
    {
        ServiceTypes = serviceTypes;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class HostAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class UserAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ModulesAttribute : Attribute
{
    public Type[] Instantiate { get; set; } = [];
    public Type[] Expect { get; set; } = [];
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AutoModulesAttribute : Attribute { }

public interface IScope
{
    void RegisterService<T>(T instance)
        where T : notnull { } // no default block in real project
    void UnregisterService<T>()
        where T : notnull { } // no default block in real project
    void ResolveDependency<T>(Action<T> onResolved)
        where T : notnull { } // no default block in real project
}

public interface IServicesReady
{
    void OnServicesReady() { } // no default block in real project
}
