using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

public sealed class ScopeServiceInfo
{
    public bool IsNode { get; }
    public INamedTypeSymbol ScopeType { get; }
    public INamedTypeSymbol[] Instantiate { get; }
    public INamedTypeSymbol[] Expect { get; }

    public ScopeServiceInfo(
        bool isNode,
        INamedTypeSymbol scopeType,
        INamedTypeSymbol[] instantiate,
        INamedTypeSymbol[] expect
    )
    {
        IsNode = isNode;
        ScopeType = scopeType;
        Instantiate = instantiate;
        Expect = expect;
    }
}
