using System;

namespace GodotSharpDI.Abstractions;

/// <summary>
/// 标记一个成员提供服务
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
public class ProvidesAttribute : Attribute
{
    public Type[] ExposedTypes { get; set; } = [];

    /// <summary>
    /// 等待的依赖字段名称数组
    /// 例如: new[] { "_database" } 或 new[] { "_database", "_config" }
    /// </summary>
    public string[] WaitFor { get; set; } = [];

    public ProvidesAttribute(params Type[] exposedTypes)
    {
        ExposedTypes = exposedTypes;
    }

    public ProvidesAttribute() { }
}
