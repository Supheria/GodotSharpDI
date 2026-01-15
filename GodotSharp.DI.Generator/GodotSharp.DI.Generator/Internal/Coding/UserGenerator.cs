using System.Text;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace GodotSharp.DI.Generator.Internal.Coding;

internal static class UserGenerator
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
        foreach (var user in graph.Users)
        {
            var source = GenerateUserSource(user);
            var hintName = $"{user.Symbol.Name}.DI.User.g.cs";
            context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
        }
    }

    private static string GenerateUserSource(TypeInfo info)
    {
        var f = new CodeFormatter();

        f.AppendLine($"namespace {info.Namespace};");
        f.AppendLine();

        f.AppendLine($"partial class {FormatClassName(info.Symbol)}");
        f.BeginBlock();
        {
            // 如果实现了 IServicesReady，则生成依赖跟踪
            if (info.IsServicesReady && info.InjectedMembers.Length > 0)
            {
                f.AppendLine("private readonly global::System.Object _dependencyLock = new();");
                f.AppendLine(
                    "private readonly global::System.Collections.Generic.HashSet<global::System.Object> _unresolvedDependencies = new()"
                );
                f.BeginBlock();
                {
                    foreach (var dep in info.InjectedMembers)
                    {
                        var depType = FormatType(dep.ParameterType);
                        f.AppendLine($"typeof({depType}),");
                    }
                }
                f.EndBlock(";");
                f.AppendLine();

                f.AppendLine("private void OnDependencyResolved<T>()");
                f.BeginBlock();
                {
                    f.AppendLine("lock (_dependencyLock)");
                    f.BeginBlock();
                    {
                        f.AppendLine("_unresolvedDependencies.Remove(typeof(T));");
                        f.AppendLine("if (_unresolvedDependencies.Count == 0)");
                        f.BeginBlock();
                        {
                            f.AppendLine(
                                $"(({TypeNamesGlobal.ServicesReadyInterface})this).OnServicesReady();"
                            );
                        }
                        f.EndBlock();
                    }
                    f.EndBlock();
                }
                f.EndBlock();
                f.AppendLine();
            }

            // ResolveUserDependencies
            f.AppendLine(
                $"private void ResolveUserDependencies({TypeNamesGlobal.ScopeInterface} scope)"
            );
            f.BeginBlock();
            {
                foreach (var (parameterType, memberName) in info.InjectedMembers)
                {
                    var depType = FormatType(parameterType);

                    f.AppendLine($"scope.ResolveDependency<{depType}>(dependency =>");
                    f.BeginBlock();
                    {
                        f.AppendLine($"{memberName} = dependency;");
                        if (info.IsServicesReady)
                        {
                            f.AppendLine($"OnDependencyResolved<{depType}>();");
                        }
                    }
                    f.EndBlock(");");
                }
            }
            f.EndBlock();
        }
        f.EndBlock();

        return f.ToString();
    }
}
