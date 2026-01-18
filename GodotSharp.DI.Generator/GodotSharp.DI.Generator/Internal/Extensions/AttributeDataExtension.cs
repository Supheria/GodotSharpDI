using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Extensions;

internal static class AttributeDataExtension
{
    public static ImmutableArray<TypedConstant> GetNamedArgument(
        this AttributeData data,
        string name
    )
    {
        foreach (var arg in data.NamedArguments)
        {
            if (arg.Key == name)
            {
                return arg.Value.Values;
            }
        }
        return ImmutableArray<TypedConstant>.Empty;
    }
}
