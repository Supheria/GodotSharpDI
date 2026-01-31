using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.Generator.Internal.Helpers;

/// <summary>
/// 诊断构建器 - 统一诊断创建接口
/// </summary>
internal static class DiagnosticBuilder
{
    /// <summary>
    /// 创建诊断
    /// </summary>
    public static Diagnostic Create(
        DiagnosticDescriptor descriptor,
        Location location,
        params object[] messageArgs
    )
    {
        return Diagnostic.Create(descriptor, location, messageArgs);
    }

    /// <summary>
    /// 创建诊断（使用默认位置）
    /// </summary>
    public static Diagnostic CreateAtNone(
        DiagnosticDescriptor descriptor,
        params object[] messageArgs
    )
    {
        return Diagnostic.Create(descriptor, Location.None, messageArgs);
    }

    /// <summary>
    /// 为符号创建诊断
    /// </summary>
    public static Diagnostic CreateForSymbol(
        DiagnosticDescriptor descriptor,
        ISymbol symbol,
        params object[] messageArgs
    )
    {
        var location = symbol.Locations.FirstOrDefault() ?? Location.None;
        return Diagnostic.Create(descriptor, location, messageArgs);
    }

    /// <summary>
    /// 批量创建诊断
    /// </summary>
    public static IEnumerable<Diagnostic> CreateMultiple(
        DiagnosticDescriptor descriptor,
        IEnumerable<(Location Location, object[] Args)> items
    )
    {
        foreach (var (location, args) in items)
        {
            yield return Diagnostic.Create(descriptor, location, args);
        }
    }
}
