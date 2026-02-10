using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Coding.Shared;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Host 代码生成器
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
        var provideMembers = validatedType
            .Members.Where(m => m.IsSingletonMember || m.IsProvidesMember)
            .ToImmutableArray();

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
    /// 2. WaitFor 等待 (WaitForPhase)
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
                // 没有依赖注入，直接提供服务
                GenerateDirectServiceProvision(f, provideMembers);
            }
            else
            {
                // 有依赖注入，使用三阶段流程
                GenerateThreePhaseLifecycle(
                    f,
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
                // 依赖注入完成后，处理每个提供的成员
                foreach (var member in provideMembers)
                {
                    if (member.HasWaitFor)
                    {
                        // 阶段 2: WaitFor 等待
                        f.AppendLine();
                        f.AppendLine($"// ━━━ 成员: {member.Symbol.Name} (with WaitFor) ━━━");
                        WaitForPhase.Generate(
                            f,
                            member.WaitFor,
                            onAllResolved: () =>
                            {
                                // 阶段 3: 服务提供
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
                        // 没有 WaitFor，直接提供服务（阶段 3）
                        f.AppendLine();
                        f.AppendLine($"// ━━━ 成员: {member.Symbol.Name} ━━━");
                        ServiceProvisionPhase.GenerateMemberProvide(
                            f,
                            member,
                            "scope",
                            "" // Host 成员直接访问，不需要前缀
                        );
                    }
                }
            }
        );
    }

    /// <summary>
    /// 直接提供服务（无依赖注入的情况）
    /// </summary>
    private static void GenerateDirectServiceProvision(
        CodeFormatter f,
        ImmutableArray<MemberInfo> provideMembers
    )
    {
        foreach (var member in provideMembers)
        {
            f.AppendLine($"// ━━━ 成员: {member.Symbol.Name} ━━━");

            if (member.HasWaitFor)
            {
                // 有 WaitFor 但没有 Inject - 使用 WaitFor 机制
                WaitForPhase.Generate(
                    f,
                    member.WaitFor,
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
        }
    }
}
