namespace GodotSharp.DI.Abstractions;

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
