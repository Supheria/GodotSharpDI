using System;

namespace GodotSharpDI.Abstractions;

/// <summary>
/// 标记一个非 Node 类型为服务提供者
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ProviderAttribute : Attribute
{
}
