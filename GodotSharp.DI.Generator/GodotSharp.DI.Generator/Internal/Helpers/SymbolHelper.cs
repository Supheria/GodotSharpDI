using System.Linq;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Helpers;

public static class SymbolHelper
{
    public static Location GetLocation(ISymbol symbol, Location fallbackLocation)
    {
        var syntax = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        return syntax?.GetLocation() ?? fallbackLocation;
    }
}
