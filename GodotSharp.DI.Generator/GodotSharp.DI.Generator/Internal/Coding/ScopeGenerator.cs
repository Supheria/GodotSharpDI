using System.Linq;
using System.Text;
using GodotSharp.DI.Generator.Internal.Data;
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
            var source = GenerateScopeSource(scope);
            var hintName = $"{scope.Symbol.Name}.DI.Scope.g.cs";
            context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
        }
    }

    private static string GenerateScopeNodeSource(ClassTypeInfo scopeInfo)
    {
        var f = new CodeFormatter();

        f.AppendLine();
        f.AppendLine($"namespace {scopeInfo.Namespace};");
        f.AppendLine();
        f.AppendLine($"partial class {FormatClassName(scopeInfo.Symbol)}");
        f.BeginBlock();
        {
            f.AppendLine("private IScope? _parentScope;");
            f.AppendLine();
            f.AppendLine("private IScope? GetParentScope()");
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
                    f.AppendLine("if (parent is IScope scope)");
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

            f.AppendLine("private void RegisterScopeSingleton<T>(T instance) where T : notnull");
            f.BeginBlock();
            {
                f.AppendLine("var type = typeof(T);");
                f.AppendLine("_scopeSingletons.Add(type, instance);");
                f.AppendLine("if (_waiters.Remove(type, out var waiterList))");
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
            f.AppendLine();

            f.AppendLine("/// <summary>");
            f.AppendLine("/// 实例化所有 Scope 约束的单例服务");
            f.AppendLine("/// </summary>");
            f.AppendLine("private void InstantiateScopeSingletons()");
            f.BeginBlock();
            {
                foreach (
                    var svc in scopeInfo.ScopeServices.Where(s =>
                        s.Lifetime == ServiceLifetime.Singleton && !s.IsHostProvided
                    )
                )
                {
                    var implName = FormatType(svc.Implementation);

                    f.AppendLine($"{implName}.CreateService(");
                    f.BeginLevel();
                    f.AppendLine("this,");
                    f.AppendLine("instance =>");
                    f.BeginBlock();
                    {
                        f.AppendLine("_scopeSingletonInstances.Add(instance);");
                        foreach (var st in svc.ServiceTypes)
                        {
                            var stName = FormatType(st);
                            f.AppendLine($"RegisterScopeSingleton(({stName})instance);");
                        }
                    }
                    f.EndBlock();
                    f.EndLevel();
                    f.AppendLine(");");
                    f.AppendLine();
                }
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("/// <summary>");
            f.AppendLine("/// 释放所有 Scope 约束的单例服务实例");
            f.AppendLine("/// </summary>");
            f.AppendLine("private void DisposeScopeSingletons()");
            f.BeginBlock();
            {
                f.AppendLine("foreach (var instance in _scopeSingletonInstances)");
                f.BeginBlock();
                {
                    f.AppendLine("if (instance is IDisposable disposable)");
                    f.BeginBlock();
                    {
                        f.AppendLine("try");
                        f.BeginBlock();
                        {
                            f.AppendLine("disposable.Dispose();");
                        }
                        f.EndBlock();
                        f.AppendLine("catch (Exception ex)");
                        f.BeginBlock();
                        {
                            f.AppendLine("Godot.GD.PushError(ex);");
                        }
                        f.EndBlock();
                    }
                    f.EndBlock();
                }
                f.EndBlock();
                f.AppendLine("_scopeSingletonInstances.Clear();");
                f.AppendLine("_scopeSingletons.Clear();");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("private void CheckWaitList()");
            f.BeginBlock();
            {
                f.AppendLine("if (_waiters.Count == 0) return;");
                f.AppendLine("var waitTypes = new StringBuilder().AppendJoin(',', _waiters.Keys);");
                f.AppendLine("Godot.GD.PushError($\"存在未完成注入的服务类型：{waitTypes}\");");
            }
            f.EndBlock();
            f.AppendLine();

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
                    f.AppendLine();
                    f.AppendLine("case NotificationReady:");
                    f.BeginBlock();
                    {
                        f.AppendLine("InstantiateScopeSingletons();");
                        f.AppendLine("CheckWaitList();");
                        f.AppendLine("break;");
                    }
                    f.EndBlock();
                    f.AppendLine();
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

    private static string GenerateScopeSource(ClassTypeInfo scopeInfo)
    {
        var f = new CodeFormatter();

        f.AppendLine();
        f.AppendLine($"namespace {scopeInfo.Namespace};");
        f.AppendLine();
        f.AppendLine($"partial class {FormatClassName(scopeInfo.Symbol)}");
        f.BeginBlock();
        {
            // SingletonTypes
            f.AppendLine("private static readonly HashSet<Type> SingletonTypes = new()");
            f.BeginBlock();
            {
                f.AppendLine("{");
                f.BeginLevel();
                foreach (
                    var svc in scopeInfo.ScopeServices.Where(s =>
                        s.Lifetime == ServiceLifetime.Singleton
                    )
                )
                {
                    foreach (var st in svc.ServiceTypes)
                    {
                        var stName = FormatType(st);
                        f.AppendLine($"typeof({stName}),");
                    }
                }
                f.EndLevel();
                f.AppendLine("};");
            }
            f.EndBlock();
            f.AppendLine();

            // TransientFactories
            f.AppendLine(
                "private static readonly Dictionary<Type, Action<IScope, Action<object>>> TransientFactories = new()"
            );
            f.BeginBlock();
            {
                f.AppendLine("{");
                f.BeginLevel();
                foreach (
                    var svc in scopeInfo.ScopeServices.Where(s =>
                        s.Lifetime == ServiceLifetime.Transient
                    )
                )
                {
                    var implName = FormatType(svc.Implementation);
                    foreach (var st in svc.ServiceTypes)
                    {
                        var stName = FormatType(st);
                        f.AppendLine($"[typeof({stName})] = {implName}.CreateService,");
                    }
                }
                f.EndLevel();
                f.AppendLine("};");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("private readonly Dictionary<Type, object> _scopeSingletons = new();");
            f.AppendLine("private readonly HashSet<object> _scopeSingletonInstances = new();");
            f.AppendLine(
                "private readonly Dictionary<Type, List<Action<object>>> _waiters = new();"
            );
            f.AppendLine();

            // RegisterScopeSingleton<T>
            f.AppendLine($"private void RegisterScopeSingleton<T>(T instance) where T : notnull");
            f.BeginBlock();
            {
                f.AppendLine("var type = typeof(T);");
                f.AppendLine("_scopeSingletons[type] = instance;");
                f.AppendLine("if (_waiters.Remove(type, out var waiters))");
                f.BeginBlock();
                {
                    f.AppendLine("foreach (var cb in waiters)");
                    f.BeginBlock();
                    {
                        f.AppendLine("cb.Invoke(instance);");
                    }
                    f.EndBlock();
                }
                f.EndBlock();
            }
            f.EndBlock();
            f.AppendLine();

            // IScope.ResolveDependency<T>
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

                f.AppendLine("if (_scopeSingletons.TryGetValue(type, out var singleton))");
                f.BeginBlock();
                {
                    f.AppendLine("onResolved.Invoke((T)singleton);");
                    f.AppendLine("return;");
                }
                f.EndBlock();

                f.AppendLine("if (!_waiters.TryGetValue(type, out var waiters))");
                f.BeginBlock();
                {
                    f.AppendLine(
                        $"waiters = new global::System.Collections.Generic.List<global::System.Action<global::System.Object>>();"
                    );
                    f.AppendLine("_waiters[type] = waiters;");
                }
                f.EndBlock();

                f.AppendLine("waiters.Add(obj => onResolved.Invoke((T)obj));");
            }
            f.EndBlock();
            f.AppendLine();

            // IScope.RegisterService<T>
            f.AppendLine($"void {TypeNamesGlobal.ScopeInterface}.RegisterService<T>(T instance)");
            f.BeginBlock();
            {
                f.AppendLine("var type = typeof(T);");
                f.AppendLine("_scopeSingletons[type] = instance!;");
                f.AppendLine("if (_waiters.Remove(type, out var waiters))");
                f.BeginBlock();
                {
                    f.AppendLine("foreach (var cb in waiters)");
                    f.BeginBlock();
                    {
                        f.AppendLine("cb.Invoke(instance!);");
                    }
                    f.EndBlock();
                }
                f.EndBlock();
            }
            f.EndBlock();
            f.AppendLine();

            // IScope.UnregisterService<T>
            f.AppendLine($"void {TypeNamesGlobal.ScopeInterface}.UnregisterService<T>()");
            f.BeginBlock();
            {
                f.AppendLine("var type = typeof(T);");
                f.AppendLine("_scopeSingletons.Remove(type);");
            }
            f.EndBlock();
        }
        f.EndBlock();

        return f.ToString();
    }
}
