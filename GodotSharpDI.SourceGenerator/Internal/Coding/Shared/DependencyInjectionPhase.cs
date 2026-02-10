using System;
using System.Collections.Immutable;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

/// <summary>
/// 生成依赖注入代码（Provider、Host 共享）
/// 阶段 1: 注入 [Inject] 标记的字段
/// </summary>
internal static class DependencyInjectionPhase
{
    /// <summary>
    /// 生成字段注入代码
    /// </summary>
    /// <param name="f">代码格式化器</param>
    /// <param name="injectMembers">需要注入的成员（[Inject] 字段）</param>
    /// <param name="scopeField">Scope 字段名称（例如 "_scope" 或 "scope"）</param>
    /// <param name="typeName">类型名称（用于错误信息）</param>
    /// <param name="onAllResolved">所有依赖解析完成后的回调</param>
    public static void Generate(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers,
        string scopeField,
        string typeName,
        Action onAllResolved
    )
    {
        if (injectMembers.IsEmpty)
        {
            // 没有依赖，直接调用回调
            onAllResolved();
            return;
        }

        f.AppendLine("// ━━━ 阶段 1: 依赖注入 ━━━");
        f.AppendLine($"var _remainingDeps = {injectMembers.Length};");
        f.AppendLine("var _depsFailed = false;");
        f.AppendLine();

        foreach (var member in injectMembers)
        {
            GenerateFieldResolution(f, member, scopeField, typeName);
        }

        f.AppendLine("return;");
        f.AppendLine();

        // 生成回调方法
        f.AppendLine("void OnDependenciesResolved()");
        f.BeginBlock();
        {
            f.AppendLine("if (_depsFailed) return;");
            f.AppendLine();
            onAllResolved();
        }
        f.EndBlock();
    }

    /// <summary>
    /// 为单个字段生成依赖解析代码
    /// </summary>
    private static void GenerateFieldResolution(
        CodeFormatter f,
        MemberInfo member,
        string scopeField,
        string typeName
    )
    {
        var memberName = member.Symbol.Name;
        var memberType = member.MemberType.ToFullyQualifiedName();

        f.AppendLine($"// 解析依赖: {memberName}");
        f.AppendLine($"{scopeField}.ResolveDependency<{memberType}>(");
        f.BeginLevel();
        {
            // onResolved 回调
            f.AppendLine("(dependency) =>");
            f.BeginBlock();
            {
                f.AppendLine($"{memberName} = dependency;");
                f.AppendLine("if (--_remainingDeps == 0)");
                f.BeginBlock();
                {
                    f.AppendLine("OnDependenciesResolved();");
                }
                f.EndBlock();
            }
            f.EndBlock(",");

            // onFailed 回调
            f.AppendLine("(error) =>");
            f.BeginBlock();
            {
                f.AppendLine("_depsFailed = true;");
                f.AppendLine(
                    $"{GlobalNames.GodotGD}.PrintErr($\"[{typeName}] 依赖注入失败 ({memberName}): {{error}}\");"
                );
            }
            f.EndBlock(",");

            // requestorType
            f.AppendLine($"requestorType: \"{typeName}\"");
        }
        f.EndLevel();
        f.AppendLine(");");
        f.AppendLine();
    }
}
