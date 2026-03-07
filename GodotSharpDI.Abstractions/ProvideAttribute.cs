using System;

namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ProvideAttribute : Attribute
{
    public Type[] ExposedTypes { get; set; } = [];

    public string[] WaitFor { get; set; } = [];
}
