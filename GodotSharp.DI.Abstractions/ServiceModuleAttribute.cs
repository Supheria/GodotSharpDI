namespace GodotSharp.DI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ServiceModuleAttribute : Attribute
{
    public Type[] Instantiate { get; set; } = [];
    public Type[] Expect { get; set; } = [];
}
