using System.Linq;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class ScopeServiceCollector
{
    public static ScopeServiceInfo Analyze(INamedTypeSymbol type, CachedSymbol cachedSymbol)
    {
        var attr = type.GetAttributes()
            .FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(
                    a.AttributeClass,
                    cachedSymbol.ServiceModuleAttribute
                )
            );
        var instantiate = GetTypeArrayFromNamedArgument(attr, "Instantiate");
        var expect = GetTypeArrayFromNamedArgument(attr, "Expect");

        var isNode = SymbolHelper.IsNode(type, cachedSymbol);
        return new ScopeServiceInfo(isNode, type, instantiate, expect);
    }

    private static INamedTypeSymbol[] GetTypeArrayFromNamedArgument(
        AttributeData? attr,
        string name
    )
    {
        if (attr is null)
        {
            return [];
        }
        foreach (var pair in attr.NamedArguments)
        {
            if (pair.Key == name)
            {
                var arg = pair.Value;
                if (arg.Kind == TypedConstantKind.Array)
                {
                    return arg
                        .Values.Where(v => v.Value is INamedTypeSymbol)
                        .Select(v => (INamedTypeSymbol)v.Value!)
                        .ToArray();
                }
            }
        }
        return [];
    }
}
