namespace GodotSharp.DI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class TransientAttribute : Attribute
{
    public Type[] ServiceTypes { get; }

    public TransientAttribute(params Type[] serviceTypes)
    {
        ServiceTypes = serviceTypes;
    }
}
