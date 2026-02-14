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
        // 生成 Node 生命周期
        NodeLifeCycleGenerator.Generate(context, node.ValidatedTypeInfo);

        // 生成 Host 特定代码
        GenerateHostSpecific(context, node);
    }

    /// <summary>
    /// 生成 Host 特定代码（ProvideHostServices）
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
            IDependenciesResolvedGenerator.GenerateInjectionReadyProperties(f, injectMembers);

            // 如果实现了 IDependenciesResolved 且有 Inject 成员,生成相关字段和方法
            if (validatedType.ImplementsIDependenciesResolved && !injectMembers.IsEmpty)
            {
                IDependenciesResolvedGenerator.GenerateAll(f, injectMembers);
            }

            GenerateProvideHostServices(f, validatedType, injectMembers, provideMembers);
            f.AppendLine();

            // 生成异步提供方法
            var asyncMembers = provideMembers.Where(m => m.IsAsync).ToImmutableArray();
            if (!asyncMembers.IsEmpty)
            {
                ServiceProvisionPhase.GenerateAsyncProviderMethods(f, asyncMembers);
            }
        }
        f.EndClassDeclaration();

        context.AddSource($"{fileName}.DI.Host.g.cs", f.ToString());
    }

    /// <summary>
    /// 生成 ProvideHostServices 方法
    /// 核心逻辑:
    /// 1. 如果有 Inject 成员且实现了 IDependenciesResolved,先注入依赖(不等待完成)
    /// 2. 对每个 Provide 成员独立处理:
    ///    - 如果有 WaitFor,等待 WaitFor 依赖
    ///    - 否则直接提供服务
    /// </summary>
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

            // 核心逻辑:根据是否有 Inject 成员和是否实现 IDependenciesResolved 来决定处理方式
            if (!injectMembers.IsEmpty && validatedType.ImplementsIDependenciesResolved)
            {
                // 有 Inject 成员且实现了 IDependenciesResolved - 使用依赖跟踪
                GenerateWithDependencyTracking(f, validatedType, injectMembers, provideMembers);
            }
            else if (!injectMembers.IsEmpty)
            {
                // 有 Inject 成员但未实现 IDependenciesResolved - 使用传统三阶段流程
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
                // 没有 Inject 成员 - 直接提供服务
                GenerateDirectProvision(f, validatedType.Members, provideMembers);
            }
        }
        f.EndBlock();
    }

    /// <summary>
    /// 有依赖跟踪的情况 (实现了 IDependenciesResolved)
    /// Inject 成员注入不阻塞 Provide 成员提供服务
    /// </summary>
    private static void GenerateWithDependencyTracking(
        CodeFormatter f,
        ValidatedTypeInfo validatedType,
        ImmutableArray<MemberInfo> injectMembers,
        ImmutableArray<MemberInfo> provideMembers
    )
    {
        // 阶段 1: 注入依赖 (不等待完成,不阻塞后续流程)
        f.AppendLine("// ━━━ 阶段 1: 注入依赖 (不阻塞服务提供) ━━━");
        foreach (var member in injectMembers)
        {
            GenerateFieldInjectionWithTracking(f, member, "scope", validatedType.Symbol.Name);
        }
        f.AppendLine();

        // 阶段 2 & 3: 每个 Provide 成员独立处理
        f.AppendLine("// ━━━ 阶段 2 & 3: 提供服务 (独立于依赖注入) ━━━");
        GenerateDirectProvision(f, validatedType.Members, provideMembers);
    }

    /// <summary>
    /// 为单个字段生成依赖注入代码 (带依赖跟踪)
    /// </summary>
    private static void GenerateFieldInjectionWithTracking(
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
            // onResult 回调
            f.AppendLine("(result) =>");
            f.BeginBlock();
            {
                f.AppendLine("if (result.IsSuccess)");
                f.BeginBlock();
                {
                    f.AppendLine($"{memberName} = result.Instance;");
                    IDependenciesResolvedGenerator.GenerateSetInjectionReady(f, memberName);
                    IDependenciesResolvedGenerator.GenerateResolvedCallback(f, memberType);
                }
                f.EndBlock();
                f.AppendLine("else");
                f.BeginBlock();
                {
                    f.AppendLine(
                        $"{GlobalNames.GodotGD}.PrintErr($\"[{typeName}] 依赖注入失败 ({memberName}): {{result.ErrorMessage}}\");"
                    );
                    IDependenciesResolvedGenerator.GenerateResolvedCallback(f, memberType);
                }
                f.EndBlock();
            }
            f.EndBlock(",");

            // requestorType
            f.AppendLine($"requestorType: \"{typeName}\"");
        }
        f.EndLevel();
        f.AppendLine(");");
        f.AppendLine();
    }

    /// <summary>
    /// 生成三阶段生命周期（有依赖注入但未实现 IDependenciesResolved 的情况）
    /// 这是传统的三阶段流程:所有 Inject 依赖解决后才提供 Provide 服务
    /// </summary>
    private static void GenerateThreePhaseLifecycle(
        CodeFormatter f,
        ImmutableArray<MemberInfo> allMembers,
        ImmutableArray<MemberInfo> injectMembers,
        ImmutableArray<MemberInfo> provideMembers,
        string typeName
    )
    {
        // 阶段 1: 依赖注入
        DependencyInjectionPhase.Generate(
            f,
            injectMembers,
            "scope",
            typeName,
            implementsIDependenciesResolved: false,
            onAllResolved: () =>
            {
                f.AppendLine("// ━━━ 阶段 2 & 3: 每个 Provide 成员独立处理 WaitFor 并提供服务 ━━━");
                f.AppendLine();

                // 依赖注入完成后，为每个 Provide 成员独立处理 WaitFor
                GenerateDirectProvision(f, allMembers, provideMembers);
            }
        );
    }

    /// <summary>
    /// 直接提供服务（每个 Provide 成员独立处理,可能有 WaitFor）
    /// </summary>
    private static void GenerateDirectProvision(
        CodeFormatter f,
        ImmutableArray<MemberInfo> allMembers,
        ImmutableArray<MemberInfo> provideMembers
    )
    {
        var waitForMembers = new List<(MemberInfo member, Action callback)>();

        foreach (var member in provideMembers)
        {
            f.AppendLine($"// ━━━ 成员: {member.Symbol.Name} ━━━");

            if (member.HasWaitFor)
            {
                // 收集需要生成 local function 的成员
                Action callback = () =>
                {
                    ServiceProvisionPhase.GenerateMemberProvide(
                        f,
                        member,
                        "scope",
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
                    "scope",
                    onAllResolved: callback
                );
            }
            else
            {
                // 直接提供
                ServiceProvisionPhase.GenerateMemberProvide(
                    f,
                    member,
                    "scope",
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
