using System.Linq;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed class SymbolCache
{
    // === Attribute Symbols ===
    public INamedTypeSymbol InjectAttribute { get; }
    public INamedTypeSymbol ModulesAttribute { get; }
    public INamedTypeSymbol SingletonAttribute { get; }
    public INamedTypeSymbol TransientAttribute { get; }
    public INamedTypeSymbol HostAttribute { get; }
    public INamedTypeSymbol UserAttribute { get; }

    // === Interface Symbols ===
    public INamedTypeSymbol ScopeInterface { get; }
    public INamedTypeSymbol ServicesReadyInterface { get; }

    // === Godot Node Symbol ===
    public INamedTypeSymbol? GodotNode { get; }

    // === Constructor ===
    public SymbolCache(Compilation compilation)
    {
        InjectAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.InjectAttribute)!;
        ModulesAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.ModulesAttribute)!;
        SingletonAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.SingletonAttribute)!;
        TransientAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.TransientAttribute)!;
        HostAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.HostAttribute)!;
        UserAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.UserAttribute)!;

        ScopeInterface = compilation.GetTypeByMetadataName(TypeNamesFull.ScopeInterface)!;
        ServicesReadyInterface = compilation.GetTypeByMetadataName(
            TypeNamesFull.ServicesReadyInterface
        )!;

        GodotNode = compilation.GetTypeByMetadataName(TypeNamesFull.GodotNode);
    }
}
