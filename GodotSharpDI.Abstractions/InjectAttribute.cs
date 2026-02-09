using System;

namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class InjectAttribute : Attribute
{
    /// <summary>
    /// 是否在注入失败时生成回调方法
    /// </summary>
    public bool FailureCallback { get; set; }
}
