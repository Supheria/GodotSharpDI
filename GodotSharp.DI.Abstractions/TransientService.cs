namespace GodotSharp.DI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TransientService : Attribute
{
    public Type[] ServiceTypes { get; }

    public TransientService(params Type[] serviceTypes)
    {
        ServiceTypes = serviceTypes;
    }
}
