using System.Linq;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed class SymbolCache
{
    public INamedTypeSymbol? InjectAttribute { get; }
    public INamedTypeSymbol? InjectConstructorAttribute { get; }
    public INamedTypeSymbol? ModulesAttribute { get; }
    public INamedTypeSymbol? AutoModulesAttribute { get; }
    public INamedTypeSymbol? SingletonAttribute { get; }
    public INamedTypeSymbol? TransientAttribute { get; }
    public INamedTypeSymbol? HostAttribute { get; }
    public INamedTypeSymbol? UserAttribute { get; }

    public INamedTypeSymbol? ScopeInterface { get; }
    public INamedTypeSymbol? ServicesReadyInterface { get; }

    public INamedTypeSymbol? GodotNodeType { get; }

    public SymbolCache(Compilation compilation)
    {
        InjectAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.InjectAttribute);
        InjectConstructorAttribute = compilation.GetTypeByMetadataName(
            TypeNamesFull.InjectConstructorAttribute
        );
        ModulesAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.ModulesAttribute);
        AutoModulesAttribute = compilation.GetTypeByMetadataName(
            TypeNamesFull.AutoModulesAttribute
        );
        SingletonAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.SingletonAttribute);
        TransientAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.TransientAttribute);
        HostAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.HostAttribute);
        UserAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.UserAttribute);

        ScopeInterface = compilation.GetTypeByMetadataName(TypeNamesFull.ScopeInterface);
        ServicesReadyInterface = compilation.GetTypeByMetadataName(
            TypeNamesFull.ServicesReadyInterface
        );

        GodotNodeType = compilation.GetTypeByMetadataName(TypeNamesFull.GodotNode);
    }
}
