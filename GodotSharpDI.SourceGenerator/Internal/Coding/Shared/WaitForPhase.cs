using System;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Internal.Semantic;
using GodotSharpDI.SourceGenerator.Shared;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

/// <summary>
/// 生成 WaitFor 依赖等待代码
/// </summary>
internal static class WaitForPhase
{
    /// <summary>
    /// 生成 WaitFor 等待代码
    /// </summary>
    /// <param name="f">代码格式化器</param>
    /// <param name="waitForSpec">WaitFor 规范字符串（逗号分隔的字段名）</param>
    /// <param name="onAllResolved">所有依赖就绪后的回调</param>
    public static void Generate(
        CodeFormatter f,
        string? waitForSpec,
        Action onAllResolved)
    {
        var dependencies = WaitForParser.Parse(waitForSpec);
        
        if (dependencies.IsEmpty)
        {
            // 没有 WaitFor，直接调用回调
            onAllResolved();
            return;
        }
        
        f.AppendLine($"// 等待 WaitFor 依赖: {waitForSpec}");
        f.AppendLine($"{GlobalNames.GodotCallable}.From(() =>");
        f.BeginBlock();
        {
            // 检查所有 WaitFor 字段不为 null
            foreach (var dep in dependencies)
            {
                f.AppendLine($"if ({dep} == null)");
                f.BeginBlock();
                {
                    f.AppendLine($"{GlobalNames.GodotGD}.PrintErr(\"WaitFor 依赖未就绪: {dep}\");");
                    f.AppendLine("return;");
                }
                f.EndBlock();
            }
            
            f.AppendLine("// 所有 WaitFor 依赖已就绪");
            onAllResolved();
        }
        f.EndBlock(").CallDeferred();");
    }
}
