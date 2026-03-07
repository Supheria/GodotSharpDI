using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Coding.Shared;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Host 代码生成器（重构版本）
/// 支持每个 Provide 成员独立的 WaitFor remaining 计数
/// </summary>
internal static class HostGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        // 生成基础 DI 文件
        NodeLifeCycleGenerator.Generate(context, node.ValidatedTypeInfo);

        // 生成依赖注入部分的代码
        InjectionGenerator.Generate(context, node);

        // 生成 Host 特定代码
        GenerateHostSpecific(context, node);
    }

    /// <summary>
    /// 生成 Host 特定代码（ProvideServices）
    /// </summary>
    public static void GenerateHostSpecific(SourceProductionContext context, TypeNode node)
    {
        var validatedType = node.ValidatedTypeInfo;

        // 分离注入成员和提供成员
        var injectMembers = validatedType.Members.Where(m => m.IsInjectMember).ToImmutableArray();
        var provideMembers = validatedType.Members.Where(m => m.IsProvideMember).ToImmutableArray();

        var f = new CodeFormatter();

        f.BeginClassDeclaration(validatedType, out var fileName);
        {
            GenerateProvideServices(f, validatedType, injectMembers, provideMembers);
            f.AppendLine();

            // 生成异步提供方法
            var asyncMembers = provideMembers.Where(m => m.IsAsync).ToImmutableArray();
            if (!asyncMembers.IsEmpty)
            {
                ServiceProvisionPhase.GenerateAsyncProviderMethods(
                    f,
                    asyncMembers,
                    validatedType.Symbol.Name
                );
            }
        }
        f.EndClassDeclaration();

        context.AddSource($"{fileName}.DI.Provide.g.cs", f.ToString());
    }

    /// <summary>
    /// 生成 ProvideServices 方法
    /// 核心逻辑:
    /// 1. 如果有 Inject 成员且实现了 IDependenciesResolved,先注入依赖(不等待完成)
    /// 2. 对每个 Provide 成员独立处理:
    ///    - 如果有 WaitFor,等待 WaitFor 依赖
    ///    - 否则直接提供服务
    /// </summary>
    private static void GenerateProvideServices(
        CodeFormatter f,
        ValidatedTypeInfo validatedType,
        ImmutableArray<MemberInfo> injectMembers,
        ImmutableArray<MemberInfo> provideMembers
    )
    {
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void ProvideServices()");
        f.BeginBlock();
        {
            f.AppendLine($"var {GlobalNames.LocalScope} = GetParentScope();");
            f.AppendLine($"if ({GlobalNames.LocalScope} is null)");
            f.BeginBlock();
            {
                f.PrintError(
                    $"\"[GodotSharpDI] {validatedType.Symbol.Name}: Cannot find parent Scope in scene tree.\""
                );
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            // 核心逻辑:根据是否有 Inject 成员和是否实现 IDependenciesResolved 来决定处理方式
            if (!injectMembers.IsEmpty && validatedType.ImplementsIDependenciesResolved)
            {
                // 有 Inject 成员且实现了 IDependenciesResolved - 使用依赖跟踪
                GenerateWithDependencyTracking(f, validatedType, injectMembers, provideMembers);
            }
            else
            {
                // 没有 Inject 成员 - 直接提供服务
                GenerateDirectProvision(
                    f,
                    validatedType.Members,
                    provideMembers,
                    validatedType.Symbol.Name
                );
            }
        }
        f.EndBlock();
    }

    /// <summary>
    /// 有依赖跟踪的情况 (实现了 IDependenciesResolved)
    /// WaitFor 通过 TCS（实例字段）机制等待 Inject 成员就绪，无需在此重复注册 ResolveDependency。
    /// Phase 1 已移除：Inject 成员由 ResolveDependencies() 统一处理，避免双重回调。
    /// </summary>
    private static void GenerateWithDependencyTracking(
        CodeFormatter f,
        ValidatedTypeInfo validatedType,
        ImmutableArray<MemberInfo> injectMembers,
        ImmutableArray<MemberInfo> provideMembers
    )
    {
        // WaitFor 依赖通过 TCS 实例字段与 ResolveDependencies() 通信，
        // 此处只需生成 Provide 阶段代码即可
        f.AppendLine(GeneratedStrings.Phase23Comment);
        GenerateDirectProvision(
            f,
            validatedType.Members,
            provideMembers,
            validatedType.Symbol.Name
        );
    }

    /// <summary>
    /// 直接提供服务（每个 Provide 成员独立处理,可能有 WaitFor）
    /// </summary>
    private static void GenerateDirectProvision(
        CodeFormatter f,
        ImmutableArray<MemberInfo> allMembers,
        ImmutableArray<MemberInfo> provideMembers,
        string providerTypeName
    )
    {
        var waitForMembers = new List<(MemberInfo member, Action callback)>();

        foreach (var member in provideMembers)
        {
            f.AppendLine(string.Format(GeneratedStrings.MemberSeparatorFmt, member.Symbol.Name));

            if (member.HasWaitFor)
            {
                // 收集需要生成 local function 的成员
                Action callback = () =>
                {
                    ServiceProvisionPhase.GenerateMemberProvide(
                        f,
                        member,
                        GlobalNames.LocalScope,
                        providerTypeName,
                        instancePrefix: "",
                        inAsyncContext: true
                    );
                };

                waitForMembers.Add((member, callback));

                // 只生成监听代码，不生成 local function
                WaitForPhase.GenerateForMember(
                    f,
                    member,
                    allMembers,
                    GlobalNames.LocalScope,
                    onAllResolved: callback
                );
            }
            else
            {
                // 直接提供
                ServiceProvisionPhase.GenerateMemberProvide(
                    f,
                    member,
                    GlobalNames.LocalScope,
                    providerTypeName,
                    instancePrefix: "",
                    inAsyncContext: false
                );
            }

            f.AppendLine();
        }

        // 统一在方法末尾添加一个 return（可选）
        if (waitForMembers.Any())
        {
            f.AppendLine("return;");
            f.AppendLine();

            // 生成所有 local function 定义
            foreach (var (member, callback) in waitForMembers)
            {
                WaitForPhase.GenerateLocalFunction(f, member, callback);
            }
        }
    }
}
