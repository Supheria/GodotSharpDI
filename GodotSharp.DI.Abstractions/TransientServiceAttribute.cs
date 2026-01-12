namespace GodotSharp.DI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TransientServiceAttribute : Attribute
{
    public Type[] ServiceTypes { get; }

    public TransientServiceAttribute(params Type[] serviceTypes)
    {
        ServiceTypes = serviceTypes;
    }
}
