using System;
using System.Collections.Generic;
using System.Linq;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class CodeWriter
{
    // ============================================================
    // 生成 Host 代码
    // ============================================================
    public static string GenerateHost(HostServiceInfo host)
    {
        var f = new CodeFormatter();

        if (TypeFormatter.GetNamespace(host.HostType, out var ns))
        {
            f.AppendLine($"namespace {ns};");
            f.AppendLine();
        }
        f.AppendLine($"partial class {TypeFormatter.GetClassName(host.HostType)}");
        f.BeginBlock();
        {
            f.AppendLine(
                $"private void RegisterServices({TypeNamesGlobal.ServiceScopeInterface} scope)"
            );
            f.BeginBlock();
            {
                foreach (var service in host.SingletonServices)
                {
                    f.AppendLine(
                        $"scope.RegisterService<{TypeFormatter.GetFullQualifiedName(service.ServiceType)}>({service.Name});"
                    );
                }
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("private void RegisterToServiceScopeAsHost()");
            f.BeginBlock();
            {
                f.AppendLine("var scope = GetServiceScope();");
                f.AppendLine("if (scope is not null)");
                f.BeginBlock();
                f.AppendLine("RegisterServices(scope);");
                f.EndBlock();
            }
            f.EndBlock();
        }
        f.EndBlock();

        return f.ToString();
    }

    // ============================================================
    // 生成 User 代码
    // ============================================================
    public static string GenerateUser(UserDependencyInfo user)
    {
        var f = new CodeFormatter();

        if (TypeFormatter.GetNamespace(user.UserType, out var ns))
        {
            f.AppendLine($"namespace {ns};");
            f.AppendLine();
        }

        f.AppendLine($"partial class {TypeFormatter.GetClassName(user.UserType)}");
        f.BeginBlock();
        {
            if (user.IsServiceAware)
            {
                f.AppendLine(
                    "private readonly global::System.Collections.Generic.HashSet<global::System.Type> _unresolvedDependencies = new()"
                );
                f.BeginBlock();
                {
                    foreach (var dep in user.Dependencies)
                    {
                        f.AppendLine($"typeof({TypeFormatter.GetFullQualifiedName(dep.Type)}),");
                    }
                }
                f.EndBlock(";");
                f.AppendLine();

                f.AppendLine("private void OnDependencyResolved(global::System.Type type)");
                f.BeginBlock();
                {
                    f.AppendLine("_unresolvedDependencies.Remove(type);");
                    f.AppendLine("if (_unresolvedDependencies.Count == 0)");
                    f.BeginBlock();
                    {
                        f.AppendLine(
                            $"(({TypeNamesGlobal.ServiceAwareInterface})this).OnServicesReady();"
                        );
                    }
                    f.EndBlock();
                }
                f.EndBlock();
                f.AppendLine();
            }

            f.AppendLine(
                $"private void ResolveServices({TypeNamesGlobal.ServiceScopeInterface} scope)"
            );
            f.BeginBlock();
            {
                foreach (var dep in user.Dependencies)
                {
                    var depType = TypeFormatter.GetFullQualifiedName(dep.Type);
                    f.AppendLine($"scope.ResolveService<{depType}>(service =>");
                    f.BeginBlock();
                    {
                        f.AppendLine($"{dep.Name} = service;");
                        if (user.IsServiceAware)
                        {
                            f.AppendLine($"OnDependencyResolved(typeof({depType}));");
                        }
                    }
                    f.EndBlock(");");
                }
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("private void RegisterToServiceScopeAsUser()");
            f.BeginBlock();
            {
                f.AppendLine("var scope = GetServiceScope();");
                f.AppendLine("if (scope is not null)");
                f.BeginBlock();
                {
                    f.AppendLine("ResolveServices(scope);");
                }
                f.EndBlock();
            }
            f.EndBlock();
        }
        f.EndBlock();

        return f.ToString();
    }

    // ============================================================
    // 生成 Scope 代码
    // ============================================================

    private static ScopeServiceKind GetScopeServiceKind(
        INamedTypeSymbol implType,
        List<ServiceDescriptor> descriptors,
        ScopeServiceInfo scope
    )
    {
        if (scope.Instantiate.Contains(implType))
        {
            var lifetime = descriptors[0].Lifetime;
            return lifetime switch
            {
                ServiceLifetime.Singleton => ScopeServiceKind.ScopeSingleton,
                ServiceLifetime.Transient => ScopeServiceKind.ScopeTransient,
                _ => ScopeServiceKind.None,
            };
        }
        if (descriptors[0].ProvidedByHost)
        {
            if (scope.Expect.Contains(implType))
            {
                return ScopeServiceKind.HostSingleton;
            }
        }
        return ScopeServiceKind.None;
    }

    public static string GenerateScope(ScopeServiceInfo scope, ServiceGraph graph)
    {
        var f = new CodeFormatter();
        // namespace
        if (TypeFormatter.GetNamespace(scope.ScopeType, out var ns))
        {
            f.AppendLine($"namespace {ns};");
            f.AppendLine();
        }
        // class header
        f.AppendLine($"partial class {TypeFormatter.GetClassName(scope.ScopeType)}");
        f.BeginBlock();
        {
            //
            // === ExpectTypes ===
            //
            f.AppendLine(
                "private static readonly global::System.Collections.Generic.HashSet<global::System.Type> SingletonTypes = new()"
            );
            f.BeginBlock();
            {
                foreach (var group in graph.ServicesByImplementation)
                {
                    var implType = group.Key;
                    var descriptors = group.Value;

                    var kind = GetScopeServiceKind(implType, descriptors, scope);
                    if (kind is ScopeServiceKind.ScopeSingleton or ScopeServiceKind.HostSingleton)
                    {
                        f.AppendLine($"// {implType.Name}");
                        foreach (var descriptor in descriptors)
                        {
                            var serviceName = TypeFormatter.GetFullQualifiedName(
                                descriptor.ServiceType
                            );
                            f.AppendLine($"typeof({serviceName}),");
                        }
                    }
                }
            }
            f.EndBlock(";");
            f.AppendLine();
            //
            // === TransientFactories ===
            //
            f.AppendLine(
                "private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, global::System.Func<object>> TransientFactories = new()"
            );
            f.BeginBlock();
            {
                foreach (var group in graph.ServicesByImplementation)
                {
                    var implType = group.Key;
                    var descriptors = group.Value;

                    var kind = GetScopeServiceKind(implType, descriptors, scope);
                    if (kind == ScopeServiceKind.ScopeTransient)
                    {
                        var implName = TypeFormatter.GetFullQualifiedName(implType);

                        f.AppendLine($"// {implType.Name}");

                        // TODO:为该实现类型的所有暴露服务类型生成工厂
                        foreach (var descriptor in descriptors)
                        {
                            var serviceName = TypeFormatter.GetFullQualifiedName(
                                descriptor.ServiceType
                            );
                            f.AppendLine($"[typeof({serviceName})] = () => new {implName}(),");
                        }
                    }
                }
            }
            f.EndBlock(";");
            f.AppendLine();
            //
            // === _singletons / _waiters ===
            //
            f.AppendLine(
                "private readonly global::System.Collections.Generic.Dictionary<global::System.Type, object> _singletons = new();"
            );
            f.AppendLine(
                "private readonly global::System.Collections.Generic.Dictionary<global::System.Type, global::System.Collections.Generic.List<global::System.Action<object>>> _waiters = new();"
            );
            f.AppendLine();
            //
            // === ResolveService ===
            //
            f.AppendLine(
                $"void {TypeNamesGlobal.ServiceScopeInterface}.ResolveService<T>(global::System.Action<T> onResolved)"
            );
            f.BeginBlock();
            {
                f.AppendLine("var type = typeof(T);");
                f.AppendLine("if (TransientFactories.TryGetValue(type, out var factory))");
                f.BeginBlock();
                {
                    f.AppendLine("onResolved.Invoke((T)factory.Invoke());");
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine("if (_singletons.TryGetValue(type, out var singleton))");
                f.BeginBlock();
                {
                    f.AppendLine("onResolved.Invoke((T)singleton);");
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine("if (!SingletonTypes.Contains(type))");
                f.BeginBlock();
                {
                    f.AppendLine("var parent = GetParentScope();");
                    f.AppendLine("if (parent is not null)");
                    f.BeginBlock();
                    {
                        f.AppendLine("parent.ResolveService(onResolved);");
                        f.AppendLine("return;");
                    }
                    f.EndBlock();
                    f.AppendLine(
                        $"{TypeNamesGlobal.Gd}.PushError($\"直到根 Service Scope 都无法找到服务类型：{{type.Name}}\");"
                    );
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine("if (!_waiters.TryGetValue(type, out var waiters))");
                f.BeginBlock();
                {
                    f.AppendLine("waiters = [];"); // C# 12 collection expression
                    f.AppendLine("_waiters[type] = waiters;");
                }
                f.EndBlock();
                f.AppendLine("waiters.Add(obj => onResolved.Invoke((T)obj));");
            }
            f.EndBlock();
            f.AppendLine();
            //
            // === RegisterService ===
            //
            f.AppendLine(
                $"void {TypeNamesGlobal.ServiceScopeInterface}.RegisterService<T>(T instance)"
            );
            f.BeginBlock();
            {
                f.AppendLine("var type = typeof(T);");
                f.AppendLine("if (!SingletonTypes.Contains(type))");
                f.BeginBlock();
                {
                    f.AppendLine("var parent = GetParentScope();");
                    f.AppendLine("if (parent is not null)");
                    f.BeginBlock();
                    {
                        f.AppendLine("parent.RegisterService(instance);");
                        f.AppendLine("return;");
                    }
                    f.EndBlock();
                    f.AppendLine(
                        $"{TypeNamesGlobal.Gd}.PushError($\"直到根 Service Scope 都无法注册服务类型：{{type.Name}}\");"
                    );
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine("if (!_singletons.TryAdd(type, instance))");
                f.BeginBlock();
                {
                    f.AppendLine(
                        "throw new global::System.Exception($\"重复注册类型: {type.Name}。\");"
                    );
                }
                f.EndBlock();
                f.AppendLine("if (!_waiters.TryGetValue(type, out var waiterList))");
                f.BeginBlock();
                {
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine("foreach (var callback in waiterList)");
                f.BeginBlock();
                {
                    f.AppendLine("callback.Invoke(instance);");
                }
                f.EndBlock();
                f.AppendLine("_waiters.Remove(type);");
            }
            f.EndBlock();
        }
        f.EndBlock();
        return f.ToString();
    }

    // ============================================================
    // 生成 utils 代码
    // ============================================================

    public static string GenerateHostUserUtils(INamedTypeSymbol type, bool isHost, bool isUser)
    {
        var f = new CodeFormatter();

        if (TypeFormatter.GetNamespace(type, out var ns))
        {
            f.AppendLine($"namespace {ns};");
            f.AppendLine();
        }
        f.AppendLine($"partial class {TypeFormatter.GetClassName(type)}");
        f.BeginBlock();
        {
            f.AppendLine("#nullable enable");
            f.AppendLine();

            f.AppendLine($"private {TypeNamesGlobal.ServiceScopeInterface}? _serviceScope;");
            f.AppendLine();

            f.AppendLine($"private {TypeNamesGlobal.ServiceScopeInterface}? GetServiceScope()");
            f.BeginBlock();
            {
                f.AppendLine("if (_serviceScope is not null)");
                f.BeginBlock();
                {
                    f.AppendLine("return _serviceScope;");
                }
                f.EndBlock();
                f.AppendLine("var parent = GetParent();");
                f.AppendLine("while (parent is not null)");
                f.BeginBlock();
                {
                    f.AppendLine($"if (parent is {TypeNamesGlobal.ServiceScopeInterface} scope)");
                    f.BeginBlock();
                    {
                        f.AppendLine("_serviceScope = scope;");
                        f.AppendLine("return _serviceScope;");
                    }
                    f.EndBlock();
                    f.AppendLine("parent = parent.GetParent();");
                }
                f.EndBlock();
                f.AppendLine(
                    $"{TypeNamesGlobal.Gd}.PushError(\"{TypeFormatter.GetClassName(type)} 没有最近的 Service Scope\");"
                );
                f.AppendLine("return null;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("public override void _Notification(int what)");
            f.BeginBlock();
            {
                f.AppendLine("base._Notification(what);");
                f.AppendLine();

                f.AppendLine("switch (what)");
                f.BeginBlock();
                {
                    f.AppendLine("case 13:", "Godot.Node.NotificationReady");
                    f.BeginBlock();
                    {
                        if (isHost)
                        {
                            f.AppendLine("RegisterToServiceScopeAsHost();");
                        }
                        if (isUser)
                        {
                            f.AppendLine("RegisterToServiceScopeAsUser();");
                        }
                    }
                    f.EndBlock();
                    f.AppendLine("break;");
                    f.AppendLine("case 10:", "Godot.Node.NotificationEnterTree");
                    f.AppendLine("case 11:", "Godot.Node.NotificationExitTree");
                    f.AppendLine("case 18:", "Godot.Node.NotificationParented");
                    f.AppendLine("case 19:", "Godot.Node.NotificationUnparented");
                    f.BeginBlock();
                    {
                        f.AppendLine("_serviceScope = null;");
                        f.AppendLine("break;");
                    }
                    f.EndBlock();
                }
                f.EndBlock();
            }
            f.EndBlock();
        }
        f.EndBlock();

        return f.ToString();
    }

    public static string GenerateScopeUtils(ScopeServiceInfo scope, ServiceGraph graph)
    {
        var f = new CodeFormatter();
        // namespace
        if (TypeFormatter.GetNamespace(scope.ScopeType, out var ns))
        {
            f.AppendLine($"namespace {ns};");
            f.AppendLine();
        }
        f.AppendLine($"partial class {TypeFormatter.GetClassName(scope.ScopeType)}");
        f.BeginBlock();
        {
            f.AppendLine("#nullable enable");
            f.AppendLine();

            //
            // === _parentScope ===
            //
            f.AppendLine($"private {TypeNamesGlobal.ServiceScopeInterface}? _parentScope;");
            f.AppendLine();
            //
            // === GetParentScope ===
            //
            f.AppendLine($"private {TypeNamesGlobal.ServiceScopeInterface}? GetParentScope()");
            f.BeginBlock();
            {
                f.AppendLine("if (_parentScope is not null)");
                f.BeginBlock();
                {
                    f.AppendLine("return _parentScope;");
                }
                f.EndBlock();
                f.AppendLine("var parent = GetParent();");
                f.AppendLine("while (parent is not null)");
                f.BeginBlock();
                {
                    f.AppendLine($"if (parent is {TypeNamesGlobal.ServiceScopeInterface} scope)");
                    f.BeginBlock();
                    {
                        f.AppendLine("_parentScope = scope;");
                        f.AppendLine("return _parentScope;");
                    }
                    f.EndBlock();
                    f.AppendLine("parent = parent.GetParent();");
                }
                f.EndBlock();
                f.AppendLine("return null;");
            }
            f.EndBlock();
            f.AppendLine();
            //
            // === RegisterServiceInstance<T> ===
            //
            f.AppendLine("private void RegisterServiceInstance<T>(T instance) where T : notnull");
            f.BeginBlock();
            {
                f.AppendLine("var type = typeof(T);");
                f.AppendLine("_singletons.Add(type, instance);");
                f.AppendLine("if (!_waiters.TryGetValue(type, out var waiterList))");
                f.BeginBlock();
                {
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine("foreach (var callback in waiterList)");
                f.BeginBlock();
                {
                    f.AppendLine("callback.Invoke(instance);");
                }
                f.EndBlock();
                f.AppendLine("_waiters.Remove(type);");
            }
            f.EndBlock();
            f.AppendLine();
            //
            // === RegisterServiceInstances ===
            //
            f.AppendLine("private void RegisterServiceInstances()");
            f.BeginBlock();
            {
                var index = 0;
                foreach (var group in graph.ServicesByImplementation)
                {
                    var implType = group.Key;
                    var descriptors = group.Value;

                    var kind = GetScopeServiceKind(implType, descriptors, scope);
                    if (kind == ScopeServiceKind.ScopeSingleton)
                    {
                        var implName = TypeFormatter.GetFullQualifiedName(implType);
                        var varName = $"{implType.Name.ToLowerInvariant()}_{index++}";

                        f.AppendLine($"// {implType.Name}");
                        f.AppendLine($"var {varName} = new {implName}();");
                        foreach (var descriptor in descriptors)
                        {
                            var serviceName = TypeFormatter.GetFullQualifiedName(
                                descriptor.ServiceType
                            );
                            f.AppendLine($"RegisterServiceInstance<{serviceName}>({varName});");
                        }
                    }
                }
            }
            f.EndBlock();
            f.AppendLine();

            //
            // === CheckWaitList ===
            //
            f.AppendLine("private void CheckWaitList()");
            f.BeginBlock();
            {
                f.AppendLine("if (_waiters.Count == 0)");
                f.BeginBlock();
                {
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine(
                    "var waitTypes = new global::System.Text.StringBuilder().AppendJoin(',', _waiters.Keys);"
                );
                f.AppendLine(
                    $"{TypeNamesGlobal.Gd}.PushError($\"存在未完成注入的服务类型：{{waitTypes}}\");"
                );
            }
            f.EndBlock();
            f.AppendLine();
            //
            // === _Notification ===
            //
            f.AppendLine("public override void _Notification(int what)");
            f.BeginBlock();
            {
                f.AppendLine("base._Notification(what);");
                f.AppendLine();
                f.AppendLine("switch (what)");
                f.BeginBlock();
                {
                    f.AppendLine("case 13:", "Godot.Node.NotificationReady");
                    f.BeginBlock();
                    {
                        f.AppendLine("RegisterServiceInstances();");
                        f.AppendLine("CheckWaitList();");
                        f.AppendLine("break;");
                    }
                    f.EndBlock();
                    f.AppendLine("case 10:", "Godot.Node.NotificationEnterTree");
                    f.AppendLine("case 11:", "Godot.Node.NotificationExitTree");
                    f.AppendLine("case 18:", "Godot.Node.NotificationParented");
                    f.AppendLine("case 19:", "Godot.Node.NotificationUnparented");
                    f.BeginBlock();
                    {
                        f.AppendLine("_parentScope = null;");
                        f.AppendLine("break;");
                    }
                    f.EndBlock();
                }
                f.EndBlock();
            }
            f.EndBlock();
        }
        f.EndBlock();
        return f.ToString();
    }
}
