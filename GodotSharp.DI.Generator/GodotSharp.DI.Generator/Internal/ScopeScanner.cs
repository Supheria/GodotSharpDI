using System.Linq;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class ScopeScanner
{
    public static ScopeDescriptor Analyze(INamedTypeSymbol type, SymbolCache symbolCache)
    {
        var attr = type.GetAttributes()
            .FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(
                    a.AttributeClass,
                    symbolCache.ModulesAttribute
                )
            );
        var instantiate = GetTypeArrayFromNamedArgument(attr, "Instantiate");
        var expect = GetTypeArrayFromNamedArgument(attr, "Expect");

        var isNode = SymbolHelper.IsNode(type, symbolCache);
        return new ScopeDescriptor(isNode, type, instantiate, expect);
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
