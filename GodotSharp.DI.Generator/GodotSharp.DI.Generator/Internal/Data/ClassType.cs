using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GodotSharp.DI.Generator.Internal.Data;

public sealed record ClassType(INamedTypeSymbol Symbol, ClassDeclarationSyntax DeclarationSyntax)
{
    public string Name => Symbol.Name;
}
