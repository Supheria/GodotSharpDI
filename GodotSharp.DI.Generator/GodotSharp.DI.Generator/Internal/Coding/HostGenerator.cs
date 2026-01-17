using System.Text;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace GodotSharp.DI.Generator.Internal.Coding;

internal static class HostGenerator
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
        foreach (var host in graph.HostOrUsers)
        {
            if (host.IsUser)
            {
                continue;
            }
            var hintName = $"{host.Symbol.Name}.DI.Host.g.cs";
            var source = GenerateHostSource(host);
            context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
        }
    }

    private static string GenerateHostSource(ClassTypeInfo info)
    {
        var f = new CodeFormatter();

        f.AppendLine();
        f.AppendLine($"namespace {info.Namespace};");
        f.AppendLine();

        f.AppendLine($"partial class {FormatClassName(info.Symbol)}");
        f.BeginBlock();
        {
            // AttachHostServices
            f.AppendLine(
                $"private void AttachHostServices({TypeNamesGlobal.ScopeInterface} scope)"
            );
            f.BeginBlock();
            {
                foreach (var provided in info.ProvidedServices)
                {
                    foreach (var serviceType in provided.ServiceTypes)
                    {
                        var serviceTypeName = FormatType(serviceType);
                        f.AppendLine(
                            $"scope.RegisterService<{serviceTypeName}>({provided.MemberName});"
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
                foreach (var provided in info.ProvidedServices)
                {
                    foreach (var serviceType in provided.ServiceTypes)
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
}
