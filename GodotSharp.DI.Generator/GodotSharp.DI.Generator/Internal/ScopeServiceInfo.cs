using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

public sealed class ScopeServiceInfo
{
    public bool IsNode { get; }
    public INamedTypeSymbol ScopeType { get; }
    public HashSet<INamedTypeSymbol> Instantiate { get; }
    public HashSet<INamedTypeSymbol> Expect { get; }

    public ScopeServiceInfo(
        bool isNode,
        INamedTypeSymbol scopeType,
        IEnumerable<INamedTypeSymbol> instantiate,
        IEnumerable<INamedTypeSymbol> expect
    )
    {
        IsNode = isNode;
        ScopeType = scopeType;
        Instantiate = new HashSet<INamedTypeSymbol>(instantiate, SymbolEqualityComparer.Default);
        Expect = new HashSet<INamedTypeSymbol>(expect, SymbolEqualityComparer.Default);
    }
}
