using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Coding.Shared;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Host 代码生成器（重构优化版）
///
/// 主要改进：
/// 1. ProvideService 调用使用实现类型而非暴露类型
/// 2. 提取公共的 WaitFor 辅助方法，大幅减少重复代码
/// 3. 优化代码结构，提高生成代码的可读性和可维护性
/// 4. 按类型分组依赖，减少重复的 ResolveDependency 调用
/// </summary>
internal static class HostGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        NodeLifeCycleGenerator.Generate(context, node.ValidatedTypeInfo);
        GenerateHostSpecific(context, node);
    }

    public static void GenerateHostSpecific(SourceProductionContext context, TypeNode node)
    {
        var validatedType = node.ValidatedTypeInfo;
        var injectMembers = validatedType.Members.Where(m => m.IsInjectMember).ToImmutableArray();
        var provideMembers = validatedType.Members.Where(m => m.IsProvideMember).ToImmutableArray();

        var f = new CodeFormatter();

        f.BeginClassDeclaration(validatedType, out var fileName);
        {
            if (validatedType.ImplementsIDependenciesResolved && !injectMembers.IsEmpty)
            {
                IDependenciesResolvedGenerator.GenerateAll(f, injectMembers);
            }

            GenerateProvideHostServices(f, validatedType, injectMembers, provideMembers);
            f.AppendLine();

            // 生成辅助方法
            var hasAsyncMembers = provideMembers.Any(m => m.IsAsync);
            var hasWaitForMembers = provideMembers.Any(m => m.HasWaitFor);

            if (hasAsyncMembers)
            {
                GenerateAsyncProviderHelper(f);
                f.AppendLine();
            }

            if (hasWaitForMembers)
            {
                GenerateWaitForDependenciesHelper(f);
                f.AppendLine();
            }
        }
        f.EndClassDeclaration();

        context.AddSource($"{fileName}.DI.Host.g.cs", f.ToString());
    }

    #region 辅助方法生成

    /// <summary>
    /// 生成异步服务提供辅助方法
    /// </summary>
    private static void GenerateAsyncProviderHelper(CodeFormatter f)
    {
        f.AppendHiddenMethodCommentAndAttribute(
            "异步服务提供辅助方法，使用实现类型调用 ProvideService"
        );
        f.AppendLine(
            "private static async global::System.Threading.Tasks.Task ProvideServiceAsync<TImpl>("
        );
        f.BeginLevel();
        {
            f.AppendLine("global::System.Threading.Tasks.Task<TImpl> task,");
            f.AppendLine("global::GodotSharpDI.Abstractions.IScope scope)");
        }
        f.EndLevel();
        f.AppendTypeConstraints("where TImpl : class");
        f.BeginBlock();
        {
            f.BeginTryCatch();
            {
                f.AppendLine("var result = await task;");
                f.AppendLine(
                    "global::Godot.Callable.From(() => scope.ProvideService<TImpl>(result)).CallDeferred();"
                );
            }
            f.CatchBlock("ex");
            {
                f.AppendLine(
                    "var errorMessage = $\"异步服务提供失败: {ex.Message} (类型: {typeof(TImpl).Name})\";"
                );
                f.AppendLine(
                    "global::Godot.Callable.From(() => scope.ProvideService<TImpl>(null, errorMessage)).CallDeferred();"
                );
            }
            f.EndTryCatch();
        }
        f.EndBlock();
    }

    /// <summary>
    /// 生成 WaitFor 依赖等待辅助方法
    /// 这个方法大幅简化了 WaitFor 逻辑，避免为每个成员生成重复的计数器代码
    /// </summary>
    private static void GenerateWaitForDependenciesHelper(CodeFormatter f)
    {
        f.AppendHiddenMethodCommentAndAttribute(
            "等待指定数量的依赖解析完成后执行回调（用于 WaitFor）"
        );
        f.AppendLine("private void WaitForDependenciesAndThen<T>(");
        f.BeginLevel();
        {
            f.AppendLine("global::GodotSharpDI.Abstractions.IScope scope,");
            f.AppendLine("int count,");
            f.AppendLine("global::System.Action onComplete,");
            f.AppendLine("string memberName)");
        }
        f.EndLevel();
        f.AppendTypeConstraints("where T : class");
        f.BeginBlock();
        {
            // 边界检查
            f.AppendLine("if (count <= 0)");
            f.BeginBlock();
            {
                f.AppendLine("onComplete();");
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            // 计数器
            f.AppendLine("var remaining = count;");
            f.AppendLine();

            // 解析所有依赖
            f.AppendLine("for (int i = 0; i < count; i++)");
            f.BeginBlock();
            {
                f.AppendLine("scope.ResolveDependency<T>(");
                f.BeginLevel();
                {
                    // 成功回调 - 递减计数器，如果为0则执行完成回调
                    f.AppendLine("_ =>");
                    f.BeginBlock();
                    {
                        f.AppendLine("if (--remaining == 0)");
                        f.BeginBlock();
                        {
                            f.AppendLine("onComplete();");
                        }
                        f.EndBlock();
                    }
                    f.EndBlock(",");

                    // 失败回调 - 打印错误并递减计数器
                    f.AppendLine("error =>");
                    f.BeginBlock();
                    {
                        f.PushError("$\"[{memberName}] WaitFor 依赖失败: {error}\"");
                        f.AppendLine("if (--remaining == 0)");
                        f.BeginBlock();
                        {
                            f.AppendLine("onComplete();");
                        }
                        f.EndBlock();
                    }
                    f.EndBlock(",");

                    f.AppendLine($"requestorType: $\"{{memberName}} (WaitFor)\"");
                }
                f.EndLevel();
                f.AppendLine(");");
            }
            f.EndBlock();
        }
        f.EndBlock();
    }

    #endregion

    #region 主要生成逻辑

    private static void GenerateProvideHostServices(
        CodeFormatter f,
        ValidatedTypeInfo validatedType,
        ImmutableArray<MemberInfo> injectMembers,
        ImmutableArray<MemberInfo> provideMembers
    )
    {
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void ProvideHostServices()");
        f.BeginBlock();
        {
            f.AppendLine("var scope = GetParentScope();");
            f.AppendLine("if (scope is null)");
            f.BeginBlock();
            {
                f.PushError($"\"[GodotSharpDI] {validatedType.Symbol.Name} 找不到父 Scope\"");
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            if (!injectMembers.IsEmpty && validatedType.ImplementsIDependenciesResolved)
            {
                GenerateWithDependencyTracking(f, validatedType, injectMembers, provideMembers);
            }
            else if (!injectMembers.IsEmpty)
            {
                GenerateThreePhaseLifecycle(
                    f,
                    validatedType.Members,
                    injectMembers,
                    provideMembers,
                    validatedType.Symbol.Name
                );
            }
            else
            {
                GenerateDirectProvision(f, validatedType.Members, provideMembers);
            }
        }
        f.EndBlock();
    }

    private static void GenerateWithDependencyTracking(
        CodeFormatter f,
        ValidatedTypeInfo validatedType,
        ImmutableArray<MemberInfo> injectMembers,
        ImmutableArray<MemberInfo> provideMembers
    )
    {
        f.AppendLine("// ━━━ 阶段 1: 注入依赖 (不阻塞服务提供) ━━━");
        foreach (var member in injectMembers)
        {
            GenerateFieldInjectionWithTracking(f, member, validatedType.Symbol.Name);
        }
        f.AppendLine();

        f.AppendLine("// ━━━ 阶段 2 & 3: 提供服务 (独立于依赖注入) ━━━");
        GenerateDirectProvision(f, validatedType.Members, provideMembers);
    }

    private static void GenerateFieldInjectionWithTracking(
        CodeFormatter f,
        MemberInfo member,
        string typeName
    )
    {
        var memberName = member.Symbol.Name;
        var memberType = member.MemberType.ToFullyQualifiedName();

        f.AppendLine($"// 注入: {memberName}");
        f.AppendLine($"scope.ResolveDependency<{memberType}>(");
        f.BeginLevel();
        {
            f.AppendLine("(dependency) =>");
            f.BeginBlock();
            {
                f.AppendLine($"{memberName} = dependency;");
                IDependenciesResolvedGenerator.GenerateSetInjectionReady(f, memberName);
                IDependenciesResolvedGenerator.GenerateResolvedCallback(f, memberType);
            }
            f.EndBlock(",");

            f.AppendLine("(error) =>");
            f.BeginBlock();
            {
                f.PushError($"$\"[{typeName}] 依赖注入失败 ({memberName}): {{error}}\"");
                IDependenciesResolvedGenerator.GenerateResolvedCallback(f, memberType);
            }
            f.EndBlock(",");

            f.AppendLine($"requestorType: \"{typeName}\"");
        }
        f.EndLevel();
        f.AppendLine(");");
        f.AppendLine();
    }

    private static void GenerateThreePhaseLifecycle(
        CodeFormatter f,
        ImmutableArray<MemberInfo> allMembers,
        ImmutableArray<MemberInfo> injectMembers,
        ImmutableArray<MemberInfo> provideMembers,
        string typeName
    )
    {
        DependencyInjectionPhase.Generate(
            f,
            injectMembers,
            "scope",
            typeName,
            implementsIDependenciesResolved: false,
            onAllResolved: () =>
            {
                f.AppendLine("// ━━━ 阶段 2 & 3: 提供服务 ━━━");
                GenerateDirectProvision(f, allMembers, provideMembers);
            }
        );
    }

    private static void GenerateDirectProvision(
        CodeFormatter f,
        ImmutableArray<MemberInfo> allMembers,
        ImmutableArray<MemberInfo> provideMembers
    )
    {
        var membersWithWaitFor = provideMembers.Where(m => m.HasWaitFor).ToList();
        var membersWithoutWaitFor = provideMembers.Where(m => !m.HasWaitFor).ToList();

        // 先处理没有 WaitFor 的成员
        foreach (var member in membersWithoutWaitFor)
        {
            GenerateServiceProvisionForMember(f, member);
            f.AppendLine();
        }

        // 处理有 WaitFor 的成员
        foreach (var member in membersWithWaitFor)
        {
            GenerateWaitForHandling(f, member, allMembers);
            f.AppendLine();
        }
    }

    #endregion

    #region WaitFor 处理

    /// <summary>
    /// 生成 WaitFor 处理逻辑
    /// 重构后使用辅助方法，大幅减少重复代码
    /// </summary>
    private static void GenerateWaitForHandling(
        CodeFormatter f,
        MemberInfo member,
        ImmutableArray<MemberInfo> allMembers
    )
    {
        var memberName = member.Symbol.Name;
        var waitForDeps = member
            .WaitFor.Select(name => allMembers.First(m => m.Symbol.Name == name))
            .ToList();

        f.AppendLine($"// ━━━ {memberName} (等待 {waitForDeps.Count} 个依赖) ━━━");

        // 按类型分组依赖，减少重复代码
        var depsByType = waitForDeps.GroupBy(dep => dep.MemberType.ToFullyQualifiedName()).ToList();

        if (depsByType.Count == 1 && depsByType[0].Count() == 1)
        {
            // 优化：单个依赖直接等待，无需使用辅助方法
            GenerateSingleDependencyWait(f, member, waitForDeps[0]);
        }
        else
        {
            // 多个依赖：使用辅助方法处理
            GenerateMultipleDependenciesWait(f, member, depsByType);
        }
    }

    /// <summary>
    /// 单个依赖的简化处理
    /// </summary>
    private static void GenerateSingleDependencyWait(
        CodeFormatter f,
        MemberInfo member,
        MemberInfo dependency
    )
    {
        var memberName = member.Symbol.Name;
        var depType = dependency.MemberType.ToFullyQualifiedName();

        f.AppendLine($"scope.ResolveDependency<{depType}>(");
        f.BeginLevel();
        {
            f.AppendLine("_ =>");
            f.BeginBlock();
            {
                GenerateServiceProvision(f, member);
            }
            f.EndBlock(",");

            f.AppendLine("error =>");
            f.BeginBlock();
            {
                f.PushError($"$\"[{memberName}] WaitFor 依赖失败: {{error}}\"");
            }
            f.EndBlock(",");

            f.AppendLine($"requestorType: \"{memberName} (WaitFor)\"");
        }
        f.EndLevel();
        f.AppendLine(");");
    }

    /// <summary>
    /// 多个依赖使用辅助方法处理
    /// 按类型分组，减少重复调用
    /// </summary>
    private static void GenerateMultipleDependenciesWait(
        CodeFormatter f,
        MemberInfo member,
        System.Collections.Generic.List<System.Linq.IGrouping<string, MemberInfo>> depsByType
    )
    {
        var memberName = member.Symbol.Name;
        var totalDeps = depsByType.Sum(g => g.Count());

        // 如果所有依赖都是同一类型，使用单次调用
        if (depsByType.Count == 1)
        {
            var depType = depsByType[0].Key;
            var count = depsByType[0].Count();

            f.AppendLine($"WaitForDependenciesAndThen<{depType}>(");
            f.BeginLevel();
            {
                f.AppendLine("scope,");
                f.AppendLine($"count: {count},");
                f.AppendLine("onComplete: () =>");
                f.BeginBlock();
                {
                    GenerateServiceProvision(f, member);
                }
                f.EndBlock(",");
                f.AppendLine($"memberName: \"{memberName}\"");
            }
            f.EndLevel();
            f.AppendLine(");");
        }
        else
        {
            // 多种类型：需要嵌套等待
            GenerateNestedDependenciesWait(f, member, depsByType, 0);
        }
    }

    /// <summary>
    /// 生成嵌套的依赖等待（处理多种类型的依赖）
    /// </summary>
    private static void GenerateNestedDependenciesWait(
        CodeFormatter f,
        MemberInfo member,
        System.Collections.Generic.List<System.Linq.IGrouping<string, MemberInfo>> depsByType,
        int currentIndex
    )
    {
        if (currentIndex >= depsByType.Count)
        {
            return;
        }

        var memberName = member.Symbol.Name;
        var group = depsByType[currentIndex];
        var depType = group.Key;
        var count = group.Count();
        var isLast = currentIndex == depsByType.Count - 1;

        f.AppendLine($"WaitForDependenciesAndThen<{depType}>(");
        f.BeginLevel();
        {
            f.AppendLine("scope,");
            f.AppendLine($"count: {count},");
            f.AppendLine("onComplete: () =>");
            f.BeginBlock();
            {
                if (isLast)
                {
                    // 最后一组：提供服务
                    GenerateServiceProvision(f, member);
                }
                else
                {
                    // 还有下一组：递归生成嵌套等待
                    GenerateNestedDependenciesWait(f, member, depsByType, currentIndex + 1);
                }
            }
            f.EndBlock(",");
            f.AppendLine($"memberName: \"{memberName}\"");
        }
        f.EndLevel();
        f.AppendLine(");");
    }

    #endregion

    #region 服务提供

    /// <summary>
    /// 为成员生成服务提供代码（带注释）
    /// </summary>
    private static void GenerateServiceProvisionForMember(CodeFormatter f, MemberInfo member)
    {
        var memberName = member.Symbol.Name;
        f.AppendLine($"// ━━━ {memberName} ━━━");
        GenerateServiceProvision(f, member);
    }

    /// <summary>
    /// 生成服务提供代码（使用实现类型）
    /// </summary>
    private static void GenerateServiceProvision(CodeFormatter f, MemberInfo member)
    {
        var memberName = member.Symbol.Name;
        var implTypeName = member.MemberType.ToFullyQualifiedName();

        if (member.IsAsync)
        {
            // 异步：使用辅助方法
            f.AppendLine($"_ = ProvideServiceAsync({memberName}(), scope);");
        }
        else
        {
            // 同步：直接提供
            f.AppendLine($"scope.ProvideService<{implTypeName}>({memberName}());");
        }
    }

    #endregion
}
