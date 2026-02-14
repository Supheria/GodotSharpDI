using System;

namespace GodotSharpDI.Abstractions;

/// <summary>
/// 标记一个成员提供服务
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
public class ProvideAttribute : Attribute
{
    public Type[] ExposedTypes { get; set; } = [];

    public string[] WaitFor { get; set; } = [];
}
