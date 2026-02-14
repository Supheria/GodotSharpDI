using System.Collections.Immutable;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

/// <summary>
/// 生成 IDependenciesResolved 接口实现所需的代码
/// Host 和 User 都可以使用此模块
/// </summary>
internal static class IDependenciesResolvedGenerator
{
    /// <summary>
    /// 生成注入准备标识符字段 (IsXxxInjectionReady)
    /// </summary>
    public static void GenerateInjectionReadyProperties(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        foreach (var member in injectMembers)
        {
            var fieldName = NamingHelper.GetInjectionReadyFieldName(member.Symbol.Name);
            f.AppendLine(
                $"/// <summary>成员 {member.Symbol.Name} 是否成功注入依赖的标识符</summary>"
            );
            f.AppendLine($"[{GlobalNames.MemberNotNullWhen}(true, nameof({member.Symbol.Name}))]");
            f.AppendLine($"private {GlobalNames.Bool} {fieldName} {{ get; set; }} = false;");
            f.AppendLine();
        }
    }

    /// <summary>
    /// 生成 IsAllDependenciesReady 属性
    /// </summary>
    public static void GenerateIsAllDependenciesReadyProperty(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        if (injectMembers.IsEmpty)
        {
            f.AppendLine($"private {GlobalNames.Bool} IsAllDependenciesReady => true;");
            return;
        }

        var fAttribute = f.CreateFromCurrentLevel();
        var fValue = f.CreateFromCurrentLevel();
        fValue.BeginLevel();
        {
            for (int i = 0; i < injectMembers.Length; i++)
            {
                var member = injectMembers[i];
                fAttribute.AppendLine(
                    $"[{GlobalNames.MemberNotNullWhen}(true, nameof({member.Symbol.Name}))]"
                );
                var fieldName = NamingHelper.GetInjectionReadyFieldName(member.Symbol.Name);
                if (i > 0)
                {
                    fValue.AppendLine();
                    fValue.AppendRaw($"&& {fieldName} == true", true);
                }
                else
                {
                    fValue.AppendRaw($"{fieldName} == true", true);
                }
            }
            fValue.AppendRaw(";");
        }
        fValue.EndLevel();
        f.AppendLine("/// <summary>所有 Inject 成员是否都成功注入依赖的标识符</summary>");
        f.AppendRaw(fAttribute.ToString());
        f.AppendLine($"private {GlobalNames.Bool} IsAllDependenciesReady =>");
        f.AppendRaw(fValue.ToString());
        f.AppendLine();
    }

    /// <summary>
    /// 生成未解析依赖集合字段 (_unresolvedDependencies)
    /// </summary>
    public static void GenerateUnresolvedDependenciesField(
        CodeFormatter f,
        ImmutableArray<MemberInfo> injectMembers
    )
    {
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private readonly {GlobalNames.HashSet}<{GlobalNames.Type}> _unresolvedDependencies = new()"
        );
        f.BeginBlock();
        {
            foreach (var member in injectMembers)
            {
                f.AppendLine($"typeof({member.MemberType.ToFullyQualifiedName()}),");
            }
        }
        f.EndBlock(";");
        f.AppendLine();
    }

    /// <summary>
    /// 生成依赖解析跟踪方法 (OnDependencyResolved)
    /// </summary>
    public static void GenerateTrackingMethod(CodeFormatter f)
    {
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void OnDependencyResolved<T>()");
        f.BeginBlock();
        {
            f.AppendLine("_unresolvedDependencies.Remove(typeof(T));");
            f.AppendLine("if (_unresolvedDependencies.Count == 0)");
            f.BeginBlock();
            {
                f.AppendLine(
                    $"(({GlobalNames.IDependenciesResolved})this).OnDependenciesResolved(IsAllDependenciesReady);"
                );
            }
            f.EndBlock();
        }
        f.EndBlock();
        f.AppendLine();
    }

    /// <summary>
    /// 生成所有 IDependenciesResolved 相关的字段和方法
    /// (便捷方法,一次性生成所有内容)
    /// </summary>
    public static void GenerateAll(CodeFormatter f, ImmutableArray<MemberInfo> injectMembers)
    {
        if (injectMembers.IsEmpty)
        {
            return;
        }

        GenerateInjectionReadyProperties(f, injectMembers);
        GenerateIsAllDependenciesReadyProperty(f, injectMembers);
        GenerateUnresolvedDependenciesField(f, injectMembers);
        GenerateTrackingMethod(f);
    }

    /// <summary>
    /// 生成调用 OnDependencyResolved 的代码片段
    /// 用于嵌入到依赖注入回调中
    /// </summary>
    public static void GenerateResolvedCallback(CodeFormatter f, string memberTypeName)
    {
        f.AppendLine($"OnDependencyResolved<{memberTypeName}>();");
    }

    /// <summary>
    /// 生成设置注入准备标识的代码片段
    /// 用于嵌入到依赖注入成功回调中
    /// </summary>
    public static void GenerateSetInjectionReady(CodeFormatter f, string memberName)
    {
        var fieldName = NamingHelper.GetInjectionReadyFieldName(memberName);
        f.AppendLine($"{fieldName} = true;");
    }
}
