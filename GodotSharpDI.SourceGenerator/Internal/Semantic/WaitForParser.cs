using System.Collections.Immutable;
using System.Linq;

namespace GodotSharpDI.SourceGenerator.Internal.Semantic;

/// <summary>
/// 解析 WaitFor 依赖字符串
/// </summary>
internal static class WaitForParser
{
    /// <summary>
    /// 解析 WaitFor 字符串为字段名列表
    /// </summary>
    /// <param name="waitForSpec">例如: "_database" 或 "_database,_config"</param>
    /// <returns>字段名列表</returns>
    public static ImmutableArray<string> Parse(string? waitForSpec)
    {
        if (string.IsNullOrWhiteSpace(waitForSpec))
            return ImmutableArray<string>.Empty;
        
        return waitForSpec
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToImmutableArray();
    }
}
