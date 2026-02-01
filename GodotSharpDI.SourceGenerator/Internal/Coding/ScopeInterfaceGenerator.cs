using System.Collections.Generic;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Scope 接口实现代码生成器
/// </summary>
internal static class ScopeInterfaceGenerator
{
    public static void GenerateInterface(
        SourceProductionContext context,
        ScopeNode node,
        DiGraph graph
    )
    {
        var f = new CodeFormatter();

        f.BeginClassDeclaration(node.ValidatedTypeInfo, out var className);
        {
            Generate(f, node, graph);
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.Scope.g.cs", f.ToString());
    }

    private static void Generate(CodeFormatter f, ScopeNode node, DiGraph graph)
    {
        var serviceTypes = CollectServiceTypes(node, graph);

        GenerateStaticCollections(f, serviceTypes);
        f.AppendLine();

        GenerateInstanceFields(f);
        f.AppendLine();

        GenerateResolveDependency(f);
        f.AppendLine();

        GenerateRegisterService(f);
        f.AppendLine();

        GenerateUnregisterService(f);
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

    private static void GenerateStaticCollections(
        CodeFormatter f,
        HashSet<ITypeSymbol> singletonServiceTypes
    )
    {
        // ServiceTypes
        f.AppendHiddenMemberCommentAndAttribute();
        f.AppendLine(
            $"private static readonly {GlobalNames.HashSet}<{GlobalNames.Type}> ServiceTypes = new()"
        );
        f.BeginBlock();
        {
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

    private static void GenerateResolveDependency(CodeFormatter f)
    {
        // ResolveDependency
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine(
            $"void {GlobalNames.IScope}.ResolveDependency<T>({GlobalNames.Action}<T> onResolved)"
        );
        f.BeginBlock();
        {
            f.AppendLine("var type = typeof(T);");
            f.AppendLine();

            f.AppendLine("if (!ServiceTypes.Contains(type))", "检查是否是单例服务类型");
            f.BeginBlock();
            {
                f.AppendLine("var parent = GetParentScope();", "尝试从父 Scope 解析");
                f.AppendLine("if (parent is not null)");
                f.BeginBlock();
                {
                    f.AppendLine("parent.ResolveDependency(onResolved);");
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine();

                f.AppendLine(
                    $"{GlobalNames.GodotGD}.PushError($\"直到根 Service Scope 都无法找到服务类型：{{type.Name}}\");"
                );
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine(
                "if (_services.TryGetValue(type, out var singleton))",
                "尝试从已注册的单例获取"
            );
            f.BeginBlock();
            {
                f.BeginTryCatch();
                {
                    f.AppendLine("onResolved.Invoke((T)singleton);");
                }
                f.EndTryCatch();
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("if (!_waiters.TryGetValue(type, out var waiterList))", "加入等待列表");
            f.BeginBlock();
            {
                f.AppendLine(
                    $"waiterList = new {GlobalNames.List}<{GlobalNames.Action}<{GlobalNames.Object}>>();"
                );
                f.AppendLine("_waiters[type] = waiterList;");
            }
            f.EndBlock();
            f.AppendLine("waiterList.Add(obj => onResolved.Invoke((T)obj));");
        }
        f.EndBlock();
    }

    private static void GenerateRegisterService(CodeFormatter f)
    {
        // RegisterService
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine($"void {GlobalNames.IScope}.RegisterService<T>(T instance)");
        f.BeginBlock();
        {
            f.AppendLine("var type = typeof(T);");
            f.AppendLine();

            f.AppendLine("if (!ServiceTypes.Contains(type))", "检查是否是单例服务类型");
            f.BeginBlock();
            {
                f.AppendLine("var parent = GetParentScope();", "尝试向父 Scope 注册");
                f.AppendLine("if (parent is not null)");
                f.BeginBlock();
                {
                    f.AppendLine("parent.RegisterService(instance);");
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine();

                f.AppendLine(
                    $"{GlobalNames.GodotGD}.PushError($\"直到根 Service Scope 都无法注册服务类型：{{type.Name}}\");"
                );
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("if (!_services.TryAdd(type, instance))", "注册服务");
            f.BeginBlock();
            {
                f.AppendLine(
                    $"{GlobalNames.GodotGD}.PushError($\"重复注册类型: {{type.Name}}。\");"
                );
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("if (_waiters.Remove(type, out var waiterList))", "通知等待者");
            f.BeginBlock();
            {
                f.AppendLine("foreach (var callback in waiterList)");
                f.BeginBlock();
                {
                    f.BeginTryCatch();
                    {
                        f.AppendLine("callback.Invoke(instance);");
                    }
                    f.EndTryCatch();
                }
                f.EndBlock();
            }
            f.EndBlock();
        }
        f.EndBlock();
    }

    private static void GenerateUnregisterService(CodeFormatter f)
    {
        // UnregisterService
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine($"void {GlobalNames.IScope}.UnregisterService<T>()");
        f.BeginBlock();
        {
            f.AppendLine("var type = typeof(T);");
            f.AppendLine();

            f.AppendLine("if (!ServiceTypes.Contains(type))", "检查是否是单例服务类型");
            f.BeginBlock();
            {
                f.AppendLine("var parent = GetParentScope();", "尝试从父 Scope 注销");
                f.AppendLine("if (parent is not null)");
                f.BeginBlock();
                {
                    f.AppendLine("parent.UnregisterService<T>();");
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine();

                f.AppendLine(
                    $"{GlobalNames.GodotGD}.PushError($\"直到根 Service Scope 都无法注销服务类型：{{type.Name}}\");"
                );
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("_services.Remove(type);");
        }
        f.EndBlock();
    }
}
