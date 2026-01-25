using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Coding;

/// <summary>
/// Scope 接口实现代码生成器
/// </summary>
internal static class ScopeInterfaceGenerator
{
    public static void Generate(CodeFormatter f, ScopeNode node, DiGraph graph)
    {
        var (singletonServiceTypes, transientFactories) = CollectServiceTypes(node, graph);

        GenerateStaticCollections(f, singletonServiceTypes, transientFactories);
        f.AppendLine();

        GenerateInstanceFields(f);
        f.AppendLine();

        GenerateResolveDependency(f);
        f.AppendLine();

        GenerateRegisterService(f);
        f.AppendLine();

        GenerateUnregisterService(f);
    }

    private static (
        System.Collections.Generic.HashSet<ITypeSymbol>,
        System.Collections.Generic.Dictionary<ITypeSymbol, string>
    ) CollectServiceTypes(ScopeNode node, DiGraph graph)
    {
        var singletonServiceTypes = new System.Collections.Generic.HashSet<ITypeSymbol>(
            SymbolEqualityComparer.Default
        );

        // 从 Instantiate 的服务中收集
        foreach (var serviceType in node.InstantiateServices)
        {
            var serviceNode = graph.ServiceNodes.FirstOrDefault(n =>
                SymbolEqualityComparer.Default.Equals(n.TypeInfo.Symbol, serviceType)
            );

            if (serviceNode != null && serviceNode.TypeInfo.Lifetime == ServiceLifetime.Singleton)
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
            // 在 HostNodes 中查找匹配的 Host
            var hostNode = graph.HostNodes.FirstOrDefault(n =>
                SymbolEqualityComparer.Default.Equals(n.TypeInfo.Symbol, hostType)
            );

            if (hostNode != null)
            {
                // 添加 Host 提供的所有服务类型
                foreach (var exposedType in hostNode.ProvidedServices)
                {
                    singletonServiceTypes.Add(exposedType);
                }
            }
        }

        var transientFactories = new System.Collections.Generic.Dictionary<ITypeSymbol, string>(
            SymbolEqualityComparer.Default
        );

        foreach (var serviceType in node.InstantiateServices)
        {
            var serviceNode = graph.ServiceNodes.FirstOrDefault(n =>
                SymbolEqualityComparer.Default.Equals(n.TypeInfo.Symbol, serviceType)
            );

            if (serviceNode != null && serviceNode.TypeInfo.Lifetime == ServiceLifetime.Transient)
            {
                foreach (var exposedType in serviceNode.ProvidedServices)
                {
                    transientFactories[exposedType] = serviceType.Name;
                }
            }
        }

        return (singletonServiceTypes, transientFactories);
    }

    private static void GenerateStaticCollections(
        CodeFormatter f,
        System.Collections.Generic.HashSet<ITypeSymbol> singletonServiceTypes,
        System.Collections.Generic.Dictionary<ITypeSymbol, string> transientFactories
    )
    {
        // SingletonServiceTypes
        f.AppendLine(
            $"private static readonly {GlobalNames.HashSet}<{GlobalNames.Type}> SingletonServiceTypes = new()"
        );
        f.BeginBlock();
        {
            foreach (var serviceType in singletonServiceTypes)
            {
                f.AppendLine($"typeof({serviceType.ToDisplayString()}),");
            }
        }
        f.EndBlock(";");
        f.AppendLine();

        // TransientFactories
        f.AppendLine(
            $"private static readonly {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.Action}<{GlobalNames.IScope}, {GlobalNames.Action}<{GlobalNames.Object}>>> TransientFactories = new()"
        );
        f.BeginBlock();
        {
            foreach (var kvp in transientFactories)
            {
                f.AppendLine($"[typeof({kvp.Key.ToDisplayString()})] = {kvp.Value}.CreateService,");
            }
        }
        f.EndBlock(";");
    }

    private static void GenerateInstanceFields(CodeFormatter f)
    {
        f.AppendLine(
            $"private readonly {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.Object}> _singletonServices = new();"
        );
        f.AppendLine(
            $"private readonly {GlobalNames.HashSet}<{GlobalNames.Object}> _scopeSingletonInstances = new();"
        );
        f.AppendLine(
            $"private readonly {GlobalNames.Dictionary}<{GlobalNames.Type}, {GlobalNames.List}<{GlobalNames.Action}<{GlobalNames.Object}>>> _waiters = new();"
        );
    }

    private static void GenerateResolveDependency(CodeFormatter f)
    {
        f.AppendLine(
            $"void {GlobalNames.IScope}.ResolveDependency<T>({GlobalNames.Action}<T> onResolved)"
        );
        f.BeginBlock();
        {
            f.AppendLine("var type = typeof(T);");
            f.AppendLine();

            f.AppendLine(
                "if (TransientFactories.TryGetValue(type, out var factory))",
                "尝试从瞬态工厂创建"
            );
            f.BeginBlock();
            {
                f.AppendLine("factory.Invoke(this, instance => onResolved.Invoke((T)instance));");
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("if (!SingletonServiceTypes.Contains(type))", "检查是否是单例服务类型");
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
                "if (_singletonServices.TryGetValue(type, out var singleton))",
                "尝试从已注册的单例获取"
            );
            f.BeginBlock();
            {
                f.AppendLine("onResolved.Invoke((T)singleton);");
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
        f.AppendLine($"void {GlobalNames.IScope}.RegisterService<T>(T instance)");
        f.BeginBlock();
        {
            f.AppendLine("var type = typeof(T);");
            f.AppendLine();

            f.AppendLine("if (!SingletonServiceTypes.Contains(type))", "检查是否是单例服务类型");
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

            f.AppendLine("if (!_singletonServices.TryAdd(type, instance))", "注册服务");
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
                    f.AppendLine("callback.Invoke(instance);");
                }
                f.EndBlock();
            }
            f.EndBlock();
        }
        f.EndBlock();
    }

    private static void GenerateUnregisterService(CodeFormatter f)
    {
        f.AppendLine($"void {GlobalNames.IScope}.UnregisterService<T>()");
        f.BeginBlock();
        {
            f.AppendLine("var type = typeof(T);");
            f.AppendLine();

            f.AppendLine("if (!SingletonServiceTypes.Contains(type))", "检查是否是单例服务类型");
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

            f.AppendLine("_singletonServices.Remove(type);");
        }
        f.EndBlock();
    }
}
