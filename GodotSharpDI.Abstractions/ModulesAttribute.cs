using System;

namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ModulesAttribute : Attribute
{
    public Type[] Hosts { get; set; } = [];
}
