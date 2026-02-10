using System;

namespace GodotSharpDI.Abstractions;

/// <summary>
/// [已过时] 对于成员，请使用 ProvidesAttribute 替代
/// 对于类，请使用 ProviderAttribute 替代
/// </summary>
[Obsolete("对于成员，请使用 ProvidesAttribute 替代；对于类，请使用 ProviderAttribute 替代", false)]
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false,
    AllowMultiple = false
)]
public sealed class SingletonAttribute : Attribute
{
    public Type[] ExposedTypes { get; }

    public SingletonAttribute(params Type[] exposedTypes)
    {
        ExposedTypes = exposedTypes;
    }
}
