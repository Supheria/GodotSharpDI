using System.Linq;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed class CachedSymbol
{
    public Compilation Compilation { get; }

    // === Attribute Symbols ===
    public INamedTypeSymbol SingletonServiceAttribute { get; }
    public INamedTypeSymbol TransientServiceAttribute { get; }
    public INamedTypeSymbol DependencyAttribute { get; }
    public INamedTypeSymbol ServiceModuleAttribute { get; }

    // === Interface Symbols ===
    public INamedTypeSymbol ServiceHostInterface { get; }
    public INamedTypeSymbol ServiceUserInterface { get; }
    public INamedTypeSymbol ServiceScopeInterface { get; }
    public INamedTypeSymbol ServiceAwareInterface { get; }

    // === Godot Node Symbol ===
    public INamedTypeSymbol? GodotNode { get; }

    // === Constructor ===
    public CachedSymbol(Compilation compilation)
    {
        Compilation = compilation;

        SingletonServiceAttribute = compilation.GetTypeByMetadataName(
            TypeNamesFull.SingletonServiceAttribute
        )!;
        TransientServiceAttribute = compilation.GetTypeByMetadataName(
            TypeNamesFull.TransientServiceAttribute
        )!;
        DependencyAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.DependencyAttribute)!;
        ServiceModuleAttribute = compilation.GetTypeByMetadataName(
            TypeNamesFull.ServiceModuleAttribute
        )!;

        ServiceHostInterface = compilation.GetTypeByMetadataName(
            TypeNamesFull.ServiceHostInterface
        )!;
        ServiceUserInterface = compilation.GetTypeByMetadataName(
            TypeNamesFull.ServiceUserInterface
        )!;
        ServiceScopeInterface = compilation.GetTypeByMetadataName(
            TypeNamesFull.ServiceScopeInterface
        )!;
        ServiceAwareInterface = compilation.GetTypeByMetadataName(
            TypeNamesFull.ServiceAwareInterface
        )!;

        GodotNode = compilation.GetTypeByMetadataName(TypeNamesFull.GodotNode);
    }
}
