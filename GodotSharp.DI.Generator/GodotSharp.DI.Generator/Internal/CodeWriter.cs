using System;
using System.Collections.Generic;
using System.Linq;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class CodeWriter
{
    private static readonly SymbolDisplayFormat ClassNameFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );
    private static readonly SymbolDisplayFormat FullyQualifiedNoAliasFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    private static bool GetNamespace(INamedTypeSymbol type, out string ns)
    {
        if (type.ContainingNamespace.IsGlobalNamespace)
        {
            ns = string.Empty;
            return false;
        }
        ns = type.ContainingNamespace.ToDisplayString();
        return true;
    }

    private static string GetClassName(INamedTypeSymbol type)
    {
        return type.ToDisplayString(ClassNameFormat);
    }

    private static string GetFullQualifiedName(ITypeSymbol type)
    {
        return type.ToDisplayString(FullyQualifiedNoAliasFormat);
    }

    // ============================================================
    // 生成 Host 代码
    // ============================================================
    public static string GenerateHostCode(INamedTypeSymbol type, CachedSymbol cached)
    {
        // 找出属性级 SingletonService
        var singletonProps = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => SymbolHelper.HasAttribute(p, cached.SingletonServiceAttribute))
            .ToArray();

        var f = new Formatter();

        if (GetNamespace(type, out var ns))
        {
            f.AppendLine($"namespace {ns};");
            f.AppendLine();
        }
        f.AppendLine($"partial class {GetClassName(type)}");
        f.BeginBlock();
        {
            f.AppendLine(
                $"private void RegisterServices({TypeNamesGlobal.ServiceScopeInterface} scope)"
            );
            f.BeginBlock();
            {
                foreach (var prop in singletonProps)
                {
                    var serviceTypes = SymbolHelper.GetServiceTypesFromAttribute(
                        prop,
                        cached.SingletonServiceAttribute
                    );
                    foreach (var serviceType in serviceTypes)
                    {
                        f.AppendLine(
                            $"scope.RegisterService<{GetFullQualifiedName(serviceType)}>({prop.Name});"
                        );
                    }
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
    public static string GenerateUserCode(INamedTypeSymbol type, CachedSymbol cached)
    {
        // 找出 Dependency 字段
        var deps = type.GetMembers()
            .Where(m => SymbolHelper.HasAttribute(m, cached.DependencyAttribute))
            .OfType<IFieldSymbol>()
            .ToArray();

        var isAware = SymbolHelper.ImplementsInterface(type, cached.ServiceAwareInterface);

        var f = new Formatter();

        if (GetNamespace(type, out var ns))
        {
            f.AppendLine($"namespace {ns};");
            f.AppendLine();
        }

        f.AppendLine($"partial class {GetClassName(type)}");
        f.BeginBlock();
        {
            if (isAware)
            {
                f.AppendLine(
                    "private readonly global::System.Collections.Generic.HashSet<global::System.Type> _unresolvedDependencies = new()"
                );
                f.BeginBlock();
                {
                    foreach (var dep in deps)
                    {
                        f.AppendLine($"typeof({GetFullQualifiedName(dep.Type)}),");
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
                foreach (var dep in deps)
                {
                    var depType = GetFullQualifiedName(dep.Type);
                    f.AppendLine($"scope.ResolveService<{depType}>(service =>");
                    f.BeginBlock();
                    {
                        f.AppendLine($"{dep.Name} = service;");
                        if (isAware)
                        {
                            f.AppendLine($"OnDependencyResolved(typeof({depType}));");
                        }
                    }
                    f.EndBlock(");");
                    f.AppendLine();
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

    private static INamedTypeSymbol[] ReadTypeArray(AttributeData attr, string name)
    {
        var arg = attr.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value;

        if (arg.Kind == TypedConstantKind.Array)
        {
            return arg
                .Values.Select(v => v.Value as INamedTypeSymbol)
                .Where(t => t is not null)
                .Cast<INamedTypeSymbol>()
                .ToArray();
        }

        return Array.Empty<INamedTypeSymbol>();
    }

    public static string GenerateScopeCode(INamedTypeSymbol scope, CachedSymbol cached)
    {
        // 必须有 ServiceModuleAttribute
        var moduleAttr = scope
            .GetAttributes()
            .FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(
                    a.AttributeClass,
                    cached.ServiceModuleAttribute
                )
            );
        if (moduleAttr is null)
            return "";

        var instantiateTypes = ReadTypeArray(moduleAttr, "Instantiate");
        var expectTypes = ReadTypeArray(moduleAttr, "Expect");

        var f = new Formatter();
        // namespace
        if (GetNamespace(scope, out var ns))
        {
            f.AppendLine($"namespace {ns};");
            f.AppendLine();
        }
        // class header
        f.AppendLine($"partial class {GetClassName(scope)}");
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
                foreach (var t in instantiateTypes)
                {
                    var serviceTypes = SymbolHelper.GetServiceTypesFromAttribute(
                        t,
                        cached.SingletonServiceAttribute
                    );
                    foreach (var serviceType in serviceTypes)
                    {
                        f.AppendLine($"typeof({GetFullQualifiedName(serviceType)}),");
                    }
                }
                foreach (var t in expectTypes)
                {
                    var serviceTypes = SymbolHelper.GetServiceTypesFromAttribute(
                        t,
                        cached.SingletonServiceAttribute
                    );
                    foreach (var serviceType in serviceTypes)
                    {
                        f.AppendLine($"typeof({GetFullQualifiedName(serviceType)}),");
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
                foreach (var type in instantiateTypes)
                {
                    // 仅生成瞬态服务（TransientServiceAttribute）
                    if (!SymbolHelper.HasAttribute(type, cached.TransientServiceAttribute))
                        continue;
                    var serviceTypes = SymbolHelper.GetServiceTypesFromAttribute(
                        type,
                        cached.TransientServiceAttribute
                    );
                    foreach (var t in serviceTypes)
                    {
                        f.AppendLine(
                            $"[typeof({GetFullQualifiedName(t)})] = () => new {GetFullQualifiedName(t)}(),"
                        );
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

    public static string GenerateHostAndUserUtilsCode(
        INamedTypeSymbol type,
        CachedSymbol cached,
        bool isHost,
        bool isUser
    )
    {
        var f = new Formatter();

        if (GetNamespace(type, out var ns))
        {
            f.AppendLine($"namespace {ns};");
            f.AppendLine();
        }
        f.AppendLine($"partial class {GetClassName(type)}");
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
                    $"{TypeNamesGlobal.Gd}.PushError(\"{GetClassName(type)} 没有最近的 Service Scope\");"
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
                    if (isHost)
                    {
                        f.AppendLine("RegisterToServiceScopeAsHost();");
                    }
                    if (isUser)
                    {
                        f.AppendLine("RegisterToServiceScopeAsUser();");
                    }
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

    public static string GenerateScopeUtilsCode(INamedTypeSymbol scope, CachedSymbol cached)
    {
        // 必须有 ServiceModuleAttribute
        var moduleAttr = scope
            .GetAttributes()
            .FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(
                    a.AttributeClass,
                    cached.ServiceModuleAttribute
                )
            );
        if (moduleAttr is null)
            return "";
        // 读取 Instantiate 列表
        var instantiateTypes = ReadTypeArray(moduleAttr, "Instantiate");
        var f = new Formatter();
        // namespace
        if (GetNamespace(scope, out var ns))
        {
            f.AppendLine($"namespace {ns};");
            f.AppendLine();
        }
        f.AppendLine($"partial class {GetClassName(scope)}");
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
                foreach (var t in instantiateTypes)
                {
                    var typeName = GetFullQualifiedName(t);
                    var varName = "_" + t.Name.Substring(0, 1).ToLower() + t.Name.Substring(1);
                    // 实例化
                    f.AppendLine($"var {varName} = new {typeName}();");
                    f.AppendLine();

                    var serviceTypes = SymbolHelper.GetServiceTypesFromAttribute(
                        t,
                        cached.SingletonServiceAttribute
                    );
                    foreach (var serviceType in serviceTypes)
                    {
                        f.AppendLine(
                            $"RegisterServiceInstance<{GetFullQualifiedName(serviceType)}>({varName});"
                        );
                    }
                    f.AppendLine();
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
