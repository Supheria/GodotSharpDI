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
/// Service 工厂代码生成器
/// </summary>
internal static class ServiceGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        var validatedType = node.ValidatedTypeInfo;

        // 分离注入成员和提供成员
        var injectMembers = validatedType.Members.Where(m => m.IsInjectMember).ToImmutableArray();
        var provideMembers = validatedType
            .Members.Where(m => m.IsProvideMember)
            .ToImmutableArray();

        var f = new CodeFormatter();

        f.BeginClassDeclaration(validatedType, out var fileName);
        {
            GenerateCreateProviderMethod(f, validatedType, injectMembers, provideMembers);
            f.AppendLine();

            // 生成异步提供方法
            var asyncMembers = provideMembers.Where(m => m.IsAsync).ToImmutableArray();
            if (!asyncMembers.IsEmpty)
            {
                ServiceProvisionPhase.GenerateAsyncProviderMethods(f, asyncMembers);
            }
        }
        f.EndClassDeclaration();

        context.AddSource($"{fileName}.DI.Provider.g.cs", f.ToString());
    }

    /// <summary>
    /// 生成 CreateProvider 静态工厂方法
    /// </summary>
    private static void GenerateCreateProviderMethod(
        CodeFormatter f,
        ValidatedTypeInfo validatedType,
        ImmutableArray<MemberInfo> injectMembers,
        ImmutableArray<MemberInfo> provideMembers
    )
    {
        var typeName = validatedType.Symbol.ToFullyQualifiedName();

        f.AppendHiddenMethodCommentAndAttribute(
            $"创建 {validatedType.Symbol.Name} 的实例并提供服务"
        );
        f.AppendLine(
            $"public static void CreateProvider("
                + $"{GlobalNames.IScope} scope, "
                + $"{GlobalNames.Action}<{GlobalNames.Object}> onCreated)"
        );
        f.BeginBlock();
        {
            f.BeginTryCatch();
            {
                // 创建实例
                f.AppendLine($"var instance = new {typeName}();");
                f.AppendLine();

                if (injectMembers.IsEmpty)
                {
                    // 没有依赖注入，直接提供服务（可能有 WaitFor）
                    GenerateServiceProvision(
                        f,
                        validatedType.Members,
                        provideMembers,
                        validatedType.Symbol.Name
                    );
                    f.AppendLine("onCreated.Invoke(instance);");
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
            f.CatchBlock("ex");
            {
                f.AppendLine(
                    $"var errorMessage = $\"Provider '{validatedType.Symbol.Name}' 创建失败: {{ex.Message}}\";"
                );

                // 为所有提供的服务报告失败
                foreach (var member in provideMembers)
                {
                    foreach (var exposedType in member.ExposedTypes)
                    {
                        var exposedTypeName = exposedType.ToFullyQualifiedName();
                        f.AppendLine(
                            $"scope.ProvideService<{exposedTypeName}>(null, errorMessage);"
                        );
                    }
                }
            }
            f.EndTryCatch();
        }
        f.EndBlock();
    }

    /// <summary>
    /// 生成三阶段生命周期
    /// 阶段 1: 依赖注入 ([Inject] 字段)
    /// 阶段 2: 每个 Provides 成员独立的 WaitFor 等待
    /// 阶段 3: 服务提供
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
                                    "instance"
                                );
                            }
                        );
                    }
                    else
                    {
                        // 没有 WaitFor，直接提供服务
                        ServiceProvisionPhase.GenerateMemberProvide(f, member, "scope", "instance");
                    }

                    f.AppendLine();
                }

                f.AppendLine("onCreated.Invoke(instance);");
            }
        );
    }

    /// <summary>
    /// 生成服务提供代码（无依赖注入的情况）
    /// </summary>
    private static void GenerateServiceProvision(
        CodeFormatter f,
        ImmutableArray<MemberInfo> allMembers,
        ImmutableArray<MemberInfo> provideMembers,
        string typeName
    )
    {
        foreach (var member in provideMembers)
        {
            f.AppendLine($"// ━━━ 成员: {member.Symbol.Name} ━━━");

            if (member.HasWaitFor)
            {
                // 有 WaitFor 但没有 Inject - 仍然使用独立的 WaitFor 机制
                WaitForPhase.GenerateForMember(
                    f,
                    member,
                    allMembers,
                    "scope",
                    onAllResolved: () =>
                    {
                        ServiceProvisionPhase.GenerateMemberProvide(f, member, "scope", "instance");
                    }
                );
            }
            else
            {
                // 没有 WaitFor，直接提供
                ServiceProvisionPhase.GenerateMemberProvide(f, member, "scope", "instance");
            }

            f.AppendLine();
        }
    }
}
