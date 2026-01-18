using System.Text;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace GodotSharp.DI.Generator.Internal.Coding;

internal static class HostOrUserGenerator
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
        foreach (var info in graph.HostOrUsers)
        {
            if (info.IsHost)
            {
                var hintName = $"{info.Symbol.Name}.DI.Host.g.cs";
                var source = GenerateHostSource(info);
                context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
            }
            if (info.IsUser)
            {
                var source = GenerateUserSource(info);
                var hintName = $"{info.Symbol.Name}.DI.User.g.cs";
                context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
            }
        }
    }

    private static string GenerateHostSource(HostUserInfo hostInfo)
    {
        var f = new CodeFormatter();

        f.AppendLine();
        f.AppendLine($"namespace {hostInfo.Namespace};");
        f.AppendLine();

        f.AppendLine($"partial class {FormatClassName(hostInfo.Symbol)}");
        f.BeginBlock();
        {
            // AttachHostServices
            f.AppendLine(
                $"private void AttachHostServices({TypeNamesGlobal.ScopeInterface} scope)"
            );
            f.BeginBlock();
            {
                foreach (var provided in hostInfo.ProvidedServices)
                {
                    foreach (var serviceType in provided.ExposedServiceTypes)
                    {
                        var serviceTypeName = FormatType(serviceType);
                        f.AppendLine(
                            $"scope.RegisterService<{serviceTypeName}>({provided.Name});"
                        );
                    }
                }
            }
            f.EndBlock();
            f.AppendLine();

            // UnattachHostServices
            f.AppendLine(
                $"private void UnattachHostServices({TypeNamesGlobal.ScopeInterface} scope)"
            );
            f.BeginBlock();
            {
                foreach (var provided in hostInfo.ProvidedServices)
                {
                    foreach (var serviceType in provided.ExposedServiceTypes)
                    {
                        var serviceTypeName = FormatType(serviceType);
                        f.AppendLine($"scope.UnregisterService<{serviceTypeName}>();");
                    }
                }
            }
            f.EndBlock();
        }
        f.EndBlock();

        return f.ToString();
    }

    private static string GenerateUserSource(HostUserInfo userInfo)
    {
        var f = new CodeFormatter();

        f.AppendLine($"namespace {userInfo.Namespace};");
        f.AppendLine();

        f.AppendLine($"partial class {FormatClassName(userInfo.Symbol)}");
        f.BeginBlock();
        {
            // 如果实现了 IServicesReady，则生成依赖跟踪
            if (userInfo.IsServicesReady && userInfo.InjectedMembers.Length > 0)
            {
                f.AppendLine("private readonly global::System.Object _dependencyLock = new();");
                f.AppendLine(
                    "private readonly global::System.Collections.Generic.HashSet<global::System.Object> _unresolvedDependencies = new()"
                );
                f.BeginBlock();
                {
                    foreach (var dep in userInfo.InjectedMembers)
                    {
                        var depType = FormatType(dep.Symbol);
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
                foreach (var (parameterType, memberName) in userInfo.InjectedMembers)
                {
                    var depType = FormatType(parameterType);

                    f.AppendLine($"scope.ResolveDependency<{depType}>(dependency =>");
                    f.BeginBlock();
                    {
                        f.AppendLine($"{memberName} = dependency;");
                        if (userInfo.IsServicesReady)
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
