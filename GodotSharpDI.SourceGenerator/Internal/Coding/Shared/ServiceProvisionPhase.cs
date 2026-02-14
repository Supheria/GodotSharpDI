using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

/// <summary>
/// 阶段 3: 提供 [Provide] 标记的服务
/// </summary>
internal static class ServiceProvisionPhase
{
    /// <summary>
    /// 为单个成员生成提供代码（使用成员的实现类型提供服务）
    /// </summary>
    /// <param name="f">代码格式化器</param>
    /// <param name="member">要提供的成员</param>
    /// <param name="scopeField">Scope 字段名称</param>
    /// <param name="instancePrefix">实例访问前缀（Provider用""，外部调用可能用"instance."）</param>
    /// <param name="inAsyncContext">是否在 async 上下文中（影响异步成员的调用方式）</param>
    public static void GenerateMemberProvide(
        CodeFormatter f,
        MemberInfo member,
        string scopeField,
        string instancePrefix = "",
        bool inAsyncContext = false
    )
    {
        var memberAccess = GetMemberAccess(member, instancePrefix);

        // 使用成员的实现类型提供服务
        var implType = member.MemberType.ToFullyQualifiedName();

        f.AppendLine($"// 提供服务: {implType}");

        if (member.IsAsync)
        {
            // 异步成员
            if (inAsyncContext)
            {
                // 在 async 上下文中，使用 await 确保完成
                f.AppendLine(
                    $"await ProvideAsync_{member.Symbol.Name}({memberAccess}, {scopeField});"
                );
            }
            else
            {
                // 不在 async 上下文中，启动异步任务但不等待
                f.AppendLine(
                    $"_ = ProvideAsync_{member.Symbol.Name}({memberAccess}, {scopeField});"
                );
            }
        }
        else
        {
            // 同步成员 - 直接提供
            GenerateSyncProvide(f, memberAccess, implType, scopeField);
        }

        f.AppendLine();
    }

    /// <summary>
    /// 生成所有异步提供的辅助方法（使用成员的实现类型提供服务）
    /// </summary>
    public static void GenerateAsyncProviderMethods(
        CodeFormatter f,
        ImmutableArray<MemberInfo> asyncMembers
    )
    {
        foreach (var member in asyncMembers.Where(m => m.IsAsync))
        {
            GenerateAsyncProviderMethod(f, member);
        }
    }

    /// <summary>
    /// 生成单个异步提供方法
    /// </summary>
    private static void GenerateAsyncProviderMethod(CodeFormatter f, MemberInfo member)
    {
        var implType = member.MemberType.ToFullyQualifiedName();
        // member.MemberType 是 T（从 Task<T> 中提取的），需要构建完整的 Task<T> 类型
        var innerTypeName = member.MemberType.ToFullyQualifiedName(); // T
        var taskTypeName = $"{GlobalNames.Task}<{innerTypeName}>"; // Task<T>
        var methodName = $"ProvideAsync_{member.Symbol.Name}";

        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine(
            $"private static async {GlobalNames.Task} {methodName}("
                + $"{taskTypeName} task, "
                + $"{GlobalNames.IScope} scope)"
        );
        f.BeginBlock();
        {
            f.BeginTryCatch();
            {
                f.AppendLine("var result = await task;");
                f.AppendLine();

                f.AppendLine("// 使用 CallDeferred 回到主线程");
                f.AppendLine($"{GlobalNames.GodotCallable}.From(() =>");
                f.BeginBlock();
                {
                    f.AppendLine(
                        $"scope.ProvideService<{implType}>({GlobalNames.ResolutionResult}.Success(result));"
                    );
                }
                f.EndBlock(").CallDeferred();");
            }
            f.CatchBlock("ex");
            {
                f.AppendLine($"{GlobalNames.GodotCallable}.From(() =>");
                f.BeginBlock();
                {
                    f.AppendLine(
                        $"scope.ProvideService<{implType}>({GlobalNames.ResolutionResult}.Failure(ex.Message));"
                    );
                }
                f.EndBlock(").CallDeferred();");
            }
            f.EndTryCatch();
        }
        f.EndBlock();
        f.AppendLine();
    }

    /// <summary>
    /// 生成同步服务提供代码
    /// </summary>
    private static void GenerateSyncProvide(
        CodeFormatter f,
        string memberAccess,
        string exposedTypeName,
        string scopeField
    )
    {
        f.BeginTryCatch();
        {
            f.AppendLine($"var instance = {memberAccess};");
            f.AppendLine(
                $"{scopeField}.ProvideService<{exposedTypeName}>({GlobalNames.ResolutionResult}.Success(instance));"
            );
        }
        f.CatchBlock("ex");
        {
            f.AppendLine(
                $"{scopeField}.ProvideService<{exposedTypeName}>({GlobalNames.ResolutionResult}.Failure(ex.Message));"
            );
        }
        f.EndTryCatch();
    }

    /// <summary>
    /// 获取成员访问表达式
    /// </summary>
    private static string GetMemberAccess(MemberInfo member, string instancePrefix)
    {
        var prefix = string.IsNullOrEmpty(instancePrefix) ? "" : $"{instancePrefix}.";
        var memberName = member.Symbol.Name;

        return member.Kind switch
        {
            MemberKind.ProvideProperty => $"{prefix}{memberName}",
            MemberKind.ProvideMethod => $"{prefix}{memberName}()",
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}
