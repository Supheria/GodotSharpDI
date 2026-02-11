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
/// 支持每个 Provides 成员独立的 WaitFor remaining 计数
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
    /// 使用统一的三阶段流程：
    /// 1. 依赖注入 (DependencyInjectionPhase)
    /// 2. 每个 Provides 成员独立的 WaitFor 等待 (WaitForPhase)
    /// 3. 服务提供 (ServiceProvisionPhase)
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

            if (injectMembers.IsEmpty)
            {
                // 没有依赖注入，直接处理 Provides 成员（可能有 WaitFor）
                GenerateDirectServiceProvision(f, validatedType.Members, provideMembers);
            }
            else
            {
                // 有依赖注入，使用三阶段流程
                GenerateThreePhaseLifecycle(
                    f,
                    validatedType.Members,
                    injectMembers,
                    provideMembers,
                    validatedType.Symbol.Name
                );
            }
        }
        f.EndBlock();
    }

    /// <summary>
    /// 生成三阶段生命周期（有依赖注入的情况）
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
            onAllResolved: () =>
            {
                f.AppendLine(
                    "// ━━━ 阶段 2 & 3: 每个 Provides 成员独立处理 WaitFor 并提供服务 ━━━"
                );
                f.AppendLine();

                // 依赖注入完成后，为每个 Provides 成员独立处理 WaitFor
                foreach (var member in provideMembers)
                {
                    f.AppendLine($"// ━━━ 成员: {member.Symbol.Name} ━━━");

                    if (member.HasWaitFor)
                    {
                        // 使用新的 WaitForPhase.GenerateForMember
                        WaitForPhase.GenerateForMember(
                            f,
                            member,
                            allMembers,
                            "scope",
                            onAllResolved: () =>
                            {
                                // WaitFor 依赖就绪后，提供服务
                                ServiceProvisionPhase.GenerateMemberProvide(
                                    f,
                                    member,
                                    "scope",
                                    "" // Host 成员直接访问，不需要前缀
                                );
                            }
                        );
                    }
                    else
                    {
                        // 没有 WaitFor，直接提供服务
                        ServiceProvisionPhase.GenerateMemberProvide(
                            f,
                            member,
                            "scope",
                            "" // Host 成员直接访问，不需要前缀
                        );
                    }

                    f.AppendLine();
                }
            }
        );
    }

    /// <summary>
    /// 直接提供服务（无依赖注入的情况）
    /// </summary>
    private static void GenerateDirectServiceProvision(
        CodeFormatter f,
        ImmutableArray<MemberInfo> allMembers,
        ImmutableArray<MemberInfo> provideMembers
    )
    {
        foreach (var member in provideMembers)
        {
            f.AppendLine($"// ━━━ 成员: {member.Symbol.Name} ━━━");

            if (member.HasWaitFor)
            {
                // 有 WaitFor 但没有 Inject - 使用独立的 WaitFor 机制
                WaitForPhase.GenerateForMember(
                    f,
                    member,
                    allMembers,
                    "scope",
                    onAllResolved: () =>
                    {
                        ServiceProvisionPhase.GenerateMemberProvide(
                            f,
                            member,
                            "scope",
                            "" // Host 成员直接访问
                        );
                    }
                );
            }
            else
            {
                // 直接提供
                ServiceProvisionPhase.GenerateMemberProvide(
                    f,
                    member,
                    "scope",
                    "" // Host 成员直接访问
                );
            }

            f.AppendLine();
        }
    }
}
