namespace GodotSharp.DI.Abstractions;

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
