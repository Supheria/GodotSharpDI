namespace GodotSharp.DI.Abstractions;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false
)]
public sealed class SingletonService : Attribute
{
    public Type[] ServiceTypes { get; }

    public SingletonService(params Type[] serviceTypes)
    {
        ServiceTypes = serviceTypes;
    }
}
