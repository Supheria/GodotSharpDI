using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;
using TypeInfo = GodotSharp.DI.Generator.Internal.Data.TypeInfo;

namespace GodotSharp.DI.Generator.Internal.Coding;

/// <summary>
/// HostAndUser 代码生成器
/// 生成同时具有 Host 和 User 特性的类型代码
/// </summary>
internal static class HostAndUserGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        var type = node.TypeInfo;
        var namespaceName = type.Symbol.ContainingNamespace.ToDisplayString();
        var className = type.Symbol.Name;

        // 生成基础 DI 文件（包含 Node DI 代码）
        GenerateBaseDI(context, type, namespaceName, className);

        // 生成 Host 特定代码
        GenerateHostCode(context, type, namespaceName, className);

        // 生成 User 特定代码
        GenerateUserCode(context, type, namespaceName, className);
    }

    /// <summary>
    /// 生成基础 DI 文件（Node 生命周期管理）
    /// </summary>
    private static void GenerateBaseDI(
        SourceProductionContext context,
        TypeInfo type,
        string namespaceName,
        string className
    )
    {
        var f = new CodeFormatter();
        var isNode = type.IsNode;

        f.BeginClassDeclaration(namespaceName, className);
        {
            if (isNode)
            {
                // HostAndUser 类型必须是 Node，生成 Node DI 代码
                NodeDIGenerator.GenerateNodeDICode(f, type);
            }
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.g.cs", f.ToString());
    }

    /// <summary>
    /// 生成 Host 特定代码（AttachHostServices/UnattachHostServices）
    /// </summary>
    private static void GenerateHostCode(
        SourceProductionContext context,
        TypeInfo type,
        string namespaceName,
        string className
    )
    {
        // 收集 Singleton 成员
        var singletonMembers = type
            .Members.Where(m =>
                m.Kind == MemberKind.SingletonField || m.Kind == MemberKind.SingletonProperty
            )
            .ToArray();

        // 如果没有 Singleton 成员，不生成 Host 代码
        if (singletonMembers.Length == 0)
            return;

        var f = new CodeFormatter();

        f.BeginClassDeclaration(namespaceName, className);
        {
            // AttachHostServices
            GenerateAttachHostServices(f, singletonMembers);
            f.AppendLine();

            // UnattachHostServices
            GenerateUnattachHostServices(f, singletonMembers);
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.Host.g.cs", f.ToString());
    }

    /// <summary>
    /// 生成 User 特定代码（ResolveUserDependencies）
    /// </summary>
    private static void GenerateUserCode(
        SourceProductionContext context,
        TypeInfo type,
        string namespaceName,
        string className
    )
    {
        // 收集 Inject 成员和 UserMember
        var injectMembers = type
            .Members.Where(m =>
                m.Kind == MemberKind.InjectField || m.Kind == MemberKind.InjectProperty
            )
            .ToArray();

        var userMembers = type
            .Members.Where(m =>
                m.Kind == MemberKind.UserMemberField || m.Kind == MemberKind.UserMemberProperty
            )
            .ToArray();

        // 如果既没有 Inject 成员也没有 UserMember，不生成 User 代码
        if (injectMembers.Length == 0 && userMembers.Length == 0)
            return;

        var f = new CodeFormatter();

        f.BeginClassDeclaration(namespaceName, className);
        {
            // 如果实现了 IServicesReady，生成依赖跟踪代码
            if (type.ImplementsIServicesReady && injectMembers.Length > 0)
            {
                GenerateDependencyTracking(f, injectMembers);
                f.AppendLine();
            }

            // 生成 ResolveUserDependencies
            GenerateResolveUserDependencies(f, type, injectMembers, userMembers);
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.User.g.cs", f.ToString());
    }

    /// <summary>
    /// 生成 AttachHostServices 方法
    /// </summary>
    private static void GenerateAttachHostServices(CodeFormatter f, MemberInfo[] singletonMembers)
    {
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine($"private void AttachHostServices({GlobalNames.IScope} scope)");
        f.BeginBlock();
        {
            foreach (var member in singletonMembers)
            {
                foreach (var exposedType in member.ExposedTypes)
                {
                    f.AppendLine(
                        $"scope.RegisterService<{exposedType.ToDisplayString()}>({member.Symbol.Name});"
                    );
                }
            }
        }
        f.EndBlock();
    }

    /// <summary>
    /// 生成 UnattachHostServices 方法
    /// </summary>
    private static void GenerateUnattachHostServices(CodeFormatter f, MemberInfo[] singletonMembers)
    {
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine($"private void UnattachHostServices({GlobalNames.IScope} scope)");
        f.BeginBlock();
        {
            foreach (var member in singletonMembers)
            {
                foreach (var exposedType in member.ExposedTypes)
                {
                    f.AppendLine($"scope.UnregisterService<{exposedType.ToDisplayString()}>();");
                }
            }
        }
        f.EndBlock();
    }

    /// <summary>
    /// 生成依赖跟踪代码（用于 IServicesReady）
    /// </summary>
    private static void GenerateDependencyTracking(CodeFormatter f, MemberInfo[] injectMembers)
    {
        f.AppendLine(
            $"private readonly {GlobalNames.HashSet}<{GlobalNames.Type}> _unresolvedDependencies = new()"
        );
        f.BeginBlock();
        {
            foreach (var member in injectMembers)
            {
                f.AppendLine($"typeof({member.MemberType.ToDisplayString()}),");
            }
        }
        f.EndBlock(";");
        f.AppendLine();

        f.AppendLine("private void OnDependencyResolved<T>()");
        f.BeginBlock();
        {
            f.AppendLine("_unresolvedDependencies.Remove(typeof(T));");
            f.AppendLine("if (_unresolvedDependencies.Count == 0)");
            f.BeginBlock();
            {
                f.AppendLine($"(({GlobalNames.IServicesReady})this).OnServicesReady();");
            }
            f.EndBlock();
        }
        f.EndBlock();
    }

    /// <summary>
    /// 生成 ResolveUserDependencies 方法
    /// </summary>
    private static void GenerateResolveUserDependencies(
        CodeFormatter f,
        TypeInfo type,
        MemberInfo[] injectMembers,
        MemberInfo[] userMembers
    )
    {
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine($"private void ResolveUserDependencies({GlobalNames.IScope} scope)");
        f.BeginBlock();
        {
            // 注入 [Inject] 成员
            foreach (var member in injectMembers)
            {
                f.AppendLine(
                    $"scope.ResolveDependency<{member.MemberType.ToDisplayString()}>(dependency =>"
                );
                f.BeginBlock();
                {
                    f.AppendLine($"{member.Symbol.Name} = dependency;");

                    if (type.ImplementsIServicesReady)
                    {
                        f.AppendLine(
                            $"OnDependencyResolved<{member.MemberType.ToDisplayString()}>();"
                        );
                    }
                }
                f.EndBlock(");");
            }

            // 递归注入 UserMember
            foreach (var member in userMembers)
            {
                f.AppendLine($"// 注入 User 成员: {member.Symbol.Name}");
                f.AppendLine($"if ({member.Symbol.Name} != null)");
                f.BeginBlock();
                {
                    f.AppendLine($"{member.Symbol.Name}.ResolveDependencies(scope);");
                }
                f.EndBlock();
            }
        }
        f.EndBlock();
    }
}
