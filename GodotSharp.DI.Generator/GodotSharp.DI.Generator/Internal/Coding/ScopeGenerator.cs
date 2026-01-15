using System.Linq;
using System.Text;
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

    public static void Generate(SourceProductionContext context, ServiceGraph graph)
    {
        foreach (var scope in graph.Scopes)
        {
            var source = GenerateScopeSource(scope, graph);
            var hintName = $"{scope.Symbol.Name}.DI.Scope.g.cs";
            context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
        }
    }

    private static string GenerateScopeSource(TypeInfo info, ServiceGraph graph)
    {
        var f = new CodeFormatter();

        f.AppendLine($"namespace {info.Namespace};");
        f.AppendLine();

        f.AppendLine($"partial class {FormatClassName(info.Symbol)}");
        f.BeginBlock();
        {
            // 字段：单例、等待队列
            f.AppendLine(
                $"private readonly global::System.Collections.Generic.Dictionary<global::System.Object, global::System.Object> _singletons = new();"
            );
            f.AppendLine(
                $"private readonly global::System.Collections.Generic.HashSet<global::System.Object> _singletonInstances = new();"
            );
            f.AppendLine(
                $"private readonly global::System.Collections.Generic.Dictionary<global::System.Object, global::System.Collections.Generic.List<global::System.Action<global::System.Object>>> _waiters = new();"
            );
            f.AppendLine();

            // TransientFactories
            f.AppendLine(
                $"private static readonly global::System.Collections.Generic.Dictionary<global::System.Object, global::System.Action<{TypeNamesGlobal.ScopeInterface}, global::System.Action<global::System.Object>>> TransientFactories = new()"
            );
            f.BeginBlock();
            {
                foreach (
                    var service in graph.Services.Where(s =>
                        s.Lifetime == ServiceLifetime.Transient
                    )
                )
                {
                    var impl = FormatType(service.Symbol);
                    foreach (var st in service.ServiceTypes)
                    {
                        var stName = FormatType(st);
                        f.AppendLine($"[typeof({stName})] = {impl}.CreateService,");
                    }
                }
            }
            f.EndBlock(";");
            f.AppendLine();

            // InstantiateScopeSingletons
            f.AppendLine("private void InstantiateScopeSingletons()");
            f.BeginBlock();
            {
                foreach (
                    var service in graph.Services.Where(s =>
                        s.Lifetime == ServiceLifetime.Singleton
                    )
                )
                {
                    var impl = FormatType(service.Symbol);
                    f.AppendLine($"{impl}.CreateService(this, instance =>");
                    f.BeginBlock();
                    {
                        f.AppendLine("_singletonInstances.Add(instance);");
                        foreach (var st in service.ServiceTypes)
                        {
                            var stName = FormatType(st);
                            f.AppendLine($"RegisterScopeSingleton<{stName}>(({stName})instance);");
                        }
                    }
                    f.EndBlock(");");
                }
            }
            f.EndBlock();
            f.AppendLine();

            // RegisterScopeSingleton<T>
            f.AppendLine($"private void RegisterScopeSingleton<T>(T instance) where T : notnull");
            f.BeginBlock();
            {
                f.AppendLine("var type = typeof(T);");
                f.AppendLine("_singletons[type] = instance;");
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

                f.AppendLine("if (_singletons.TryGetValue(type, out var singleton))");
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
                f.AppendLine("_singletons[type] = instance!;");
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
                f.AppendLine("_singletons.Remove(type);");
            }
            f.EndBlock();
        }
        f.EndBlock();

        return f.ToString();
    }
}
