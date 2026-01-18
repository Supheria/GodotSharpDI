using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Data;

public abstract record TypeInfo(INamedTypeSymbol Symbol, string Namespace);
