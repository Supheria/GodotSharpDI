using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding.Shared;

/// <summary>
/// 生成服务提供代码（Provider 和 Host 共享）
/// 阶段 3: 提供 [Provides] 或 [Singleton] 标记的服务
/// </summary>
internal static class ServiceProvisionPhase
{
    /// <summary>
    /// 为单个成员生成提供代码
    /// </summary>
    /// <param name="f">代码格式化器</param>
    /// <param name="member">要提供的成员</param>
    /// <param name="scopeField">Scope 字段名称</param>
    /// <param name="instancePrefix">实例访问前缀（Provider用""，外部调用可能用"instance."）</param>
    public static void GenerateMemberProvide(
        CodeFormatter f,
        MemberInfo member,
        string scopeField,
        string instancePrefix = ""
    )
    {
        var memberAccess = GetMemberAccess(member, instancePrefix);

        // 为每个暴露类型提供服务
        foreach (var exposedType in member.ExposedTypes)
        {
            var exposedTypeName = exposedType.ToFullyQualifiedName();

            f.AppendLine($"// 提供服务: {exposedTypeName}");

            if (member.IsAsync)
            {
                // 异步成员 - 启动异步任务
                f.AppendLine(
                    $"_ = ProvideAsync_{member.Symbol.Name}_{GetSafeTypeName(exposedType)}({memberAccess}, {scopeField});"
                );
            }
            else
            {
                // 同步成员 - 直接提供
                GenerateSyncProvide(f, memberAccess, exposedTypeName, scopeField);
            }

            f.AppendLine();
        }
    }

    /// <summary>
    /// 生成所有异步提供的辅助方法
    /// </summary>
    public static void GenerateAsyncProviderMethods(
        CodeFormatter f,
        ImmutableArray<MemberInfo> asyncMembers
    )
    {
        foreach (var member in asyncMembers.Where(m => m.IsAsync))
        {
            foreach (var exposedType in member.ExposedTypes)
            {
                GenerateAsyncProviderMethod(f, member, exposedType);
            }
        }
    }

    /// <summary>
    /// 生成单个异步提供方法
    /// </summary>
    private static void GenerateAsyncProviderMethod(
        CodeFormatter f,
        MemberInfo member,
        INamedTypeSymbol exposedType
    )
    {
        var exposedTypeName = exposedType.ToFullyQualifiedName();
        // member.MemberType 是 T（从 Task<T> 中提取的），需要构建完整的 Task<T> 类型
        var innerTypeName = member.MemberType.ToFullyQualifiedName(); // T
        var taskTypeName = $"{GlobalNames.Task}<{innerTypeName}>"; // Task<T>
        var methodName = $"ProvideAsync_{member.Symbol.Name}_{GetSafeTypeName(exposedType)}";

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
                    f.AppendLine($"scope.ProvideService<{exposedTypeName}>(result);");
                }
                f.EndBlock(").CallDeferred();");
            }
            f.CatchBlock("ex");
            {
                f.AppendLine($"{GlobalNames.GodotCallable}.From(() =>");
                f.BeginBlock();
                {
                    f.AppendLine($"var errorMessage = $\"异步服务提供失败: {{ex.Message}}\";");
                    f.AppendLine($"scope.ProvideService<{exposedTypeName}>(null, errorMessage);");
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
            f.AppendLine($"{scopeField}.ProvideService<{exposedTypeName}>(instance);");
        }
        f.CatchBlock("ex");
        {
            f.AppendLine($"{scopeField}.ProvideService<{exposedTypeName}>(null, ex.Message);");
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

    /// <summary>
    /// 获取类型的安全名称（用于方法命名）
    /// </summary>
    private static string GetSafeTypeName(INamedTypeSymbol type)
    {
        return type.Name.Replace("<", "_").Replace(">", "_").Replace(",", "_");
    }
}
