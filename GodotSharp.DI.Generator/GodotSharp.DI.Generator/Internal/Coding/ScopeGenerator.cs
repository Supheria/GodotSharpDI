using System;
using System.Linq;
using System.Text;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace GodotSharp.DI.Generator.Internal.Coding;

internal static class ScopeGenerator
{
    private static string FormatType(ITypeSymbol type)
    {
        return type.ToDisplayString(DisplayFormats.TypeFullQualified);
    }

    private static string FormatClassName(ITypeSymbol type)
    {
        return type.ToDisplayString(DisplayFormats.ClassName);
    }

    public static void Generate(SourceProductionContext context, DiGraph graph)
    {
        foreach (var scope in graph.Scopes)
        {
            var source = GenerateDiSource(scope);
            var hintName = $"{scope.Symbol.Name}.DI.g.cs";
            context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));

            source = GenerateScopeSource(scope);
            hintName = $"{scope.Symbol.Name}.DI.Scope.g.cs";
            context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
        }
    }

    private static string GenerateDiSource(ScopeInfo scopeInfo)
    {
        var f = new CodeFormatter();

        SourceGenHelper.AppendFileHeader(f);

        f.AppendLine($"namespace {scopeInfo.Namespace};");
        f.AppendLine();
        f.AppendLine($"partial class {FormatClassName(scopeInfo.Symbol)}");
        f.BeginBlock();
        {
            // ============================================================
            // Parent Scope
            // ============================================================
            f.AppendLine($"private {TypeNamesGlobal.ScopeInterface}? _parentScope;");
            f.AppendLine();
            f.AppendLine($"private {TypeNamesGlobal.ScopeInterface}? GetParentScope()");
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
                    f.AppendLine($"if (parent is {TypeNamesGlobal.ScopeInterface} scope)");
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

            // ============================================================
            // InstantiateScopeSingletons
            // ============================================================
            f.AppendXmlSummery("实例化所有 Scope 约束的单例服务");
            f.AppendLine("private void InstantiateScopeSingletons()");
            f.BeginBlock();
            {
                foreach (
                    var svc in scopeInfo.Services.Where(s =>
                        s.Lifetime == ServiceLifetime.Singleton && !s.IsHostProvided
                    )
                )
                {
                    var implName = FormatType(svc.ImplementType);

                    f.AppendLine($"{implName}.CreateService(");
                    f.BeginLevel();
                    {
                        f.AppendLine("this,");
                        f.AppendLine("(instance, scope) =>");
                        f.BeginBlock();
                        {
                            f.AppendLine("_scopeSingletonInstances.Add(instance);");
                            foreach (var st in svc.ExposedServiceTypes)
                            {
                                var stName = FormatType(st);
                                f.AppendLine($"scope.RegisterService(({stName})instance);");
                            }
                        }
                        f.EndBlock();
                    }
                    f.EndLevel();
                    f.AppendLine(");");
                }
            }
            f.EndBlock();
            f.AppendLine();

            // ============================================================
            // DisposeScopeSingletons
            // ============================================================
            f.AppendXmlSummery("释放所有 Scope 约束的单例服务实例");
            f.AppendLine("private void DisposeScopeSingletons()");
            f.BeginBlock();
            {
                f.AppendLine("foreach (var instance in _scopeSingletonInstances)");
                f.BeginBlock();
                {
                    f.AppendLine("if (instance is global::System.IDisposable disposable)");
                    f.BeginBlock();
                    {
                        f.AppendLine("try");
                        f.BeginBlock();
                        {
                            f.AppendLine("disposable.Dispose();");
                        }
                        f.EndBlock();
                        f.AppendLine("catch (global::System.Exception ex)");
                        f.BeginBlock();
                        {
                            f.AppendLine("global::Godot.GD.PushError(ex);");
                        }
                        f.EndBlock();
                    }
                    f.EndBlock();
                }
                f.EndBlock();

                f.AppendLine("_scopeSingletonInstances.Clear();");
                f.AppendLine("_singletonServices.Clear();");
                f.AppendLine("_waiters.Clear();");
            }
            f.EndBlock();
            f.AppendLine();

            // ============================================================
            // CheckWaitList
            // ============================================================
            f.AppendLine("private void CheckWaitList()");
            f.BeginBlock();
            {
                f.AppendLine("if (_waiters.Count == 0)");
                f.BeginBlock();
                {
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine("var sb = new global::System.Text.StringBuilder();");
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
                f.AppendLine("global::Godot.GD.PushError($\"存在未完成注入的服务类型：{sb}\");");
                f.AppendLine("_waiters.Clear();");
            }
            f.EndBlock();
            f.AppendLine();

            // ============================================================
            // Notification
            // ============================================================
            f.AppendLine("public override void _Notification(int what)");
            f.BeginBlock();
            {
                f.AppendLine("base._Notification(what);");
                f.AppendLine();
                f.AppendLine("switch ((long)what)");
                f.BeginBlock();
                {
                    f.AppendLine("case NotificationEnterTree:");
                    f.AppendLine("case NotificationExitTree:");
                    f.BeginBlock();
                    {
                        f.AppendLine("_parentScope = null;");
                        f.AppendLine("break;");
                    }
                    f.EndBlock();
                    f.AppendLine("case NotificationReady:");
                    f.BeginBlock();
                    {
                        f.AppendLine("InstantiateScopeSingletons();");
                        f.AppendLine("CheckWaitList();");
                        f.AppendLine("break;");
                    }
                    f.EndBlock();
                    f.AppendLine("case NotificationPredelete:");
                    f.BeginBlock();
                    {
                        f.AppendLine("DisposeScopeSingletons();");
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

    private static string GenerateScopeSource(ScopeInfo scopeInfo)
    {
        var f = new CodeFormatter();

        SourceGenHelper.AppendFileHeader(f);

        f.AppendLine($"namespace {scopeInfo.Namespace};");
        f.AppendLine();
        f.AppendLine($"partial class {FormatClassName(scopeInfo.Symbol)}");
        f.BeginBlock();
        {
            // ============================================================
            // 1. SingletonTypes
            // ============================================================
            f.AppendLine(
                "private static readonly global::System.Collections.Generic.HashSet<global::System.Type> SingletonServiceTypes = new()"
            );
            f.BeginBlock();
            {
                foreach (
                    var svc in scopeInfo.Services.Where(s =>
                        s.Lifetime == ServiceLifetime.Singleton
                    )
                )
                {
                    foreach (var st in svc.ExposedServiceTypes)
                    {
                        f.AppendLine($"typeof({FormatType(st)}),");
                    }
                }
            }
            f.EndBlock(";");
            f.AppendLine();

            // ============================================================
            // 2. TransientFactories
            // ============================================================
            f.AppendLine(
                $"private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, global::System.Action<{TypeNamesGlobal.ScopeInterface}, global::System.Action<global::System.Object>>> TransientFactories = new()"
            );
            f.BeginBlock();
            {
                foreach (
                    var svc in scopeInfo.Services.Where(s =>
                        s.Lifetime == ServiceLifetime.Transient
                    )
                )
                {
                    var implName = FormatType(svc.ImplementType);
                    foreach (var st in svc.ExposedServiceTypes)
                    {
                        f.AppendLine($"[typeof({FormatType(st)})] = {implName}.CreateService,");
                    }
                }
            }
            f.EndBlock(";");
            f.AppendLine();

            // ============================================================
            // 3. Scope fields
            // ============================================================
            f.AppendLine(
                "private readonly global::System.Collections.Generic.Dictionary<global::System.Type, global::System.Object> _singletonServices = new();"
            );
            f.AppendLine(
                "private readonly global::System.Collections.Generic.HashSet<global::System.Object> _scopeSingletonInstances = new();"
            );
            f.AppendLine(
                "private readonly global::System.Collections.Generic.Dictionary<global::System.Type, global::System.Collections.Generic.List<global::System.Action<global::System.Object>>> _waiters = new();"
            );
            f.AppendLine();

            // ============================================================
            // 4. IScope.ResolveDependency<T>
            // ============================================================
            f.AppendLine(
                $"void {TypeNamesGlobal.ScopeInterface}.ResolveDependency<T>(global::System.Action<T> onResolved)"
            );
            f.BeginBlock();
            {
                f.AppendLine("var type = typeof(T);");
                f.AppendLine("if (TransientFactories.TryGetValue(type, out var factory))");
                f.BeginBlock();
                {
                    f.AppendLine("factory.Invoke(this, obj => onResolved.Invoke((T)obj));");
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine("if (!SingletonServiceTypes.Contains(type))");
                f.BeginBlock();
                {
                    f.AppendLine("var parent = GetParentScope();");
                    f.AppendLine("if (parent is not null)");
                    f.BeginBlock();
                    {
                        f.AppendLine("parent.ResolveDependency(onResolved);");
                        f.AppendLine("return;");
                    }
                    f.EndBlock();
                    f.AppendLine(
                        "global::Godot.GD.PushError($\"直到根 Service Scope 都无法找到服务类型：{type.Name}\");"
                    );
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine("if (_singletonServices.TryGetValue(type, out var singleton))");
                f.BeginBlock();
                {
                    f.AppendLine("onResolved.Invoke((T)singleton);");
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine("if (!_waiters.TryGetValue(type, out var waiterList))");
                f.BeginBlock();
                {
                    f.AppendLine(
                        "waiterList = new global::System.Collections.Generic.List<global::System.Action<global::System.Object>>();"
                    );
                    f.AppendLine("_waiters[type] = waiterList;");
                }
                f.EndBlock();

                f.AppendLine("waiterList.Add(obj => onResolved.Invoke((T)obj));");
            }
            f.EndBlock();
            f.AppendLine();

            // ============================================================
            // 5. IScope.RegisterService<T>
            // ============================================================
            f.AppendLine($"void {TypeNamesGlobal.ScopeInterface}.RegisterService<T>(T instance)");
            f.BeginBlock();
            {
                f.AppendLine("var type = typeof(T);");
                f.AppendLine("if (!SingletonServiceTypes.Contains(type))");
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
                        "global::Godot.GD.PushError($\"直到根 Service Scope 都无法注册服务类型：{type.Name}\");"
                    );
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine("if (!_singletonServices.TryAdd(type, instance))");
                f.BeginBlock();
                {
                    f.AppendLine("global::Godot.GD.PushError($\"重复注册类型: {type.Name}。\");");
                }
                f.EndBlock();
                f.AppendLine("if (_waiters.Remove(type, out var waiterList))");
                f.BeginBlock();
                {
                    f.AppendLine("foreach (var callback in waiterList)");
                    f.BeginBlock();
                    {
                        f.AppendLine("callback.Invoke(instance!);");
                    }
                    f.EndBlock();
                }
                f.EndBlock();
            }
            f.EndBlock();
            f.AppendLine();

            // ============================================================
            // 6. IScope.UnregisterService<T>
            // ============================================================
            f.AppendLine($"void {TypeNamesGlobal.ScopeInterface}.UnregisterService<T>()");
            f.BeginBlock();
            {
                f.AppendLine("var type = typeof(T);");

                // Not in this scope → try parent
                f.AppendLine("if (!SingletonServiceTypes.Contains(type))");
                f.BeginBlock();
                {
                    f.AppendLine("var parent = GetParentScope();");
                    f.AppendLine("if (parent is not null)");
                    f.BeginBlock();
                    {
                        f.AppendLine("parent.UnregisterService<T>();");
                        f.AppendLine("return;");
                    }
                    f.EndBlock();

                    f.AppendLine(
                        "global::Godot.GD.PushError($\"直到根 Service Scope 都无法注册服务类型：{type.Name}\");"
                    );
                    f.AppendLine("return;");
                }
                f.EndBlock();

                // Remove from this scope
                f.AppendLine("_singletonServices.Remove(type);");
            }
            f.EndBlock();
        }
        f.EndBlock();

        return f.ToString();
    }
}
