using System;

namespace GodotSharpDI.Abstractions;

/// <summary>
/// 标记一个成员提供服务
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
public class ProvidesAttribute : Attribute
{
    public Type ServiceType { get; }
    
    /// <summary>
    /// 等待的依赖字段名称（逗号分隔）
    /// 例如: "_database" 或 "_database,_config"
    /// </summary>
    public string? WaitFor { get; set; }
    
    public ProvidesAttribute(Type serviceType)
    {
        ServiceType = serviceType;
    }
}
