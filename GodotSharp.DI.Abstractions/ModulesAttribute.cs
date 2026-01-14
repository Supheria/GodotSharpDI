namespace GodotSharp.DI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ModulesAttribute : Attribute
{
    public Type[] Instantiate { get; set; } = [];
    public Type[] Expect { get; set; } = [];
}
