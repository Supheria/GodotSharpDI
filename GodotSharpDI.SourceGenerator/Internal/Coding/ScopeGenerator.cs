using System.Collections.Generic;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Scope 代码生成器
/// </summary>
internal static class ScopeGenerator
{
    public static void Generate(SourceProductionContext context, ScopeNode node, DiGraph graph)
    {
        NodeLifeCycleGenerator.Generate(context, node.ValidatedTypeInfo);

        ScopeInterfaceGenerator.GenerateInterface(context, node, graph);

        // 生成 Scope 特定代码
        GenerateScopeSpecific(context, node, graph);
    }

    public static void GenerateScopeSpecific(
        SourceProductionContext context,
        ScopeNode node,
        DiGraph graph
    )
    {
        var f = new CodeFormatter();

        f.BeginClassDeclaration(node.ValidatedTypeInfo, out var className);
        {
            GenerateStaticCollections(f, node, graph);
            f.AppendLine();

            GenerateInstanceFields(f);
            f.AppendLine();

            GenerateInstantiateScopeSingletons(f, node, graph);
            f.AppendLine();

            GenerateDisposeScopeSingletons(f);
            f.AppendLine();

            GenerateCheckWaitList(f);
            f.AppendLine();
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.Scope.g.cs", f.ToString());
    }

    private static HashSet<ITypeSymbol> CollectServiceTypes(ScopeNode node, DiGraph graph)
    {
        var singletonServiceTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // 从 Instantiate 的服务中收集
        foreach (var serviceType in node.InstantiateServices)
        {
            if (graph.ServiceNodeMap.TryGetValue(serviceType, out var serviceNode))
            {
                foreach (var exposedType in serviceNode.ProvidedServices)
                {
                    singletonServiceTypes.Add(exposedType);
                }
            }
        }

        // 从 Expect 的 Host 中收集
        foreach (var hostType in node.ExpectHosts)
        {
            if (graph.HostNodeMap.TryGetValue(hostType, out var hostNode))
            {
                // 添加 Host 提供的所有服务类型
                foreach (var exposedType in hostNode.ProvidedServices)
                {
                    singletonServiceTypes.Add(exposedType);
                }
            }
            else if (graph.HostAndUserNodeMap.TryGetValue(hostType, out var hostAndUserNode))
            {
                // 添加 HostAndUser 提供的所有服务类型
                foreach (var exposedType in hostAndUserNode.ProvidedServices)
                {
                    singletonServiceTypes.Add(exposedType);
                }
            }
        }

        return singletonServiceTypes;
    }

    private static void GenerateStaticCollections(CodeFormatter f, ScopeNode node, DiGraph graph)
    {
        // ServiceTypes
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private static readonly {GlobalNames.HashSet}<{GlobalNames.Type}> ServiceTypes = new()"
        );
        f.BeginBlock();
        {
            var singletonServiceTypes = CollectServiceTypes(node, graph);
            foreach (var serviceType in singletonServiceTypes)
            {
                f.AppendLine($"typeof({serviceType.ToFullyQualifiedName()}),");
            }
        }
        f.EndBlock(";");
    }

    private static void GenerateInstanceFields(CodeFormatter f)
    {
        // _services
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private readonly {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.Object}> _services = new();"
        );
        f.AppendLine();

        // _waiters
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private readonly {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.List}<{GlobalNames.Action}<{GlobalNames.Object}>>> _waiters = new();"
        );
        f.AppendLine();

        // _disposableSingletons
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private readonly {GlobalNames.HashSet}<{GlobalNames.IDisposable}> _disposableSingletons = new();"
        );
    }

    private static void GenerateInstantiateScopeSingletons(
        CodeFormatter f,
        ScopeNode node,
        DiGraph graph
    )
    {
        // InstantiateScopeSingletons
        f.AppendHiddenMethodCommentAndAttribute("实例化所有 Scope 约束的单例服务");
        f.AppendLine("private void InstantiateScopeSingletons()");
        f.BeginBlock();
        {
            foreach (var serviceType in node.InstantiateServices)
            {
                if (!graph.ServiceNodeMap.TryGetValue(serviceType, out var serviceNode))
                {
                    continue;
                }

                var simpleServiceName = serviceType.ToFullyQualifiedName();

                f.AppendLine($"{simpleServiceName}.CreateService(");
                f.BeginLevel();
                {
                    f.AppendLine("this,");
                    f.AppendLine("(instance, scope) =>");
                    f.BeginBlock();
                    {
                        f.AppendLine($"if (instance is {GlobalNames.IDisposable} disposable)");
                        f.BeginBlock();
                        {
                            f.AppendLine("_disposableSingletons.Add(disposable);");
                        }
                        f.EndBlock();

                        foreach (var exposedType in serviceNode.ProvidedServices)
                        {
                            f.AppendLine(
                                $"scope.ProvideService(({exposedType.ToFullyQualifiedName()})instance);"
                            );
                        }
                    }
                    f.EndBlock();
                }
                f.EndLevel();
                f.AppendLine(");");
            }
        }
        f.EndBlock();
    }

    private static void GenerateDisposeScopeSingletons(CodeFormatter f)
    {
        // DisposeScopeSingletons
        f.AppendHiddenMethodCommentAndAttribute("释放所有 Scope 约束的单例服务实例");
        f.AppendLine("private void DisposeScopeSingletons()");
        f.BeginBlock();
        {
            f.AppendLine("foreach (var disposable in _disposableSingletons)");
            f.BeginBlock();
            {
                f.BeginTryCatch();
                {
                    f.AppendLine("disposable.Dispose();");
                }
                f.EndTryCatch();
            }
            f.EndBlock();

            f.AppendLine("_disposableSingletons.Clear();");
            f.AppendLine("_services.Clear();");
        }
        f.EndBlock();
    }

    private static void GenerateCheckWaitList(CodeFormatter f)
    {
        // CheckWaitList
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void CheckWaitList()");
        f.BeginBlock();
        {
            f.AppendLine("if (_waiters.Count == 0)");
            f.BeginBlock();
            {
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine($"var sb = new {GlobalNames.StringBuilder}();");
            f.AppendLine("var first = true;");
            f.AppendLine("foreach (var type in _waiters.Keys)");
            f.BeginBlock();
            {
                f.AppendLine("if (!first)");
                f.BeginBlock();
                {
                    f.AppendLine("sb.Append(',');");
                }
                f.EndBlock();
                f.AppendLine("sb.Append(type.Name);");
                f.AppendLine("first = false;");
            }
            f.EndBlock();

            f.AppendLine(
                $"{GlobalNames.GodotGD}.PushError($\"存在未完成注入的服务类型：{{sb}}\");"
            );
            f.AppendLine("_waiters.Clear();");
        }
        f.EndBlock();
    }
}
