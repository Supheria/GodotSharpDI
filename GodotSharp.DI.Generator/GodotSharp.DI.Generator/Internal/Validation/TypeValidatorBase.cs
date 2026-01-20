using System.Collections.Generic;
using System.Collections.Immutable;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Validation;

internal abstract class TypeValidatorBase
{
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics;
    protected readonly ClassType Type;
    protected readonly CachedSymbols Symbols;
    protected readonly ClassTypeRoles Roles;

    protected TypeValidatorBase(ClassType type, CachedSymbols symbols, ClassTypeRoles roles)
    {
        _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        Type = type;
        Symbols = symbols;
        Roles = roles;
    }

    public ImmutableArray<Diagnostic> GetDiagnostics()
    {
        return _diagnostics.ToImmutable();
    }

    protected void Report(
        DiagnosticDescriptor descriptor,
        Location[]? additionalLocations,
        params object?[]? messageArgs
    )
    {
        Report(descriptor, Type.IdentifierLocation, additionalLocations, messageArgs);
    }

    protected void Report(
        DiagnosticDescriptor descriptor,
        Location location,
        Location[]? additionalLocations,
        params object?[]? messageArgs
    )
    {
        _diagnostics.Add(Diagnostic.Create(descriptor, location, additionalLocations, messageArgs));
    }

    protected Location[] GetAttributesLocation(params INamedTypeSymbol?[] attributeSymbols)
    {
        var locations = new List<Location>(attributeSymbols.Length);
        foreach (var attr in attributeSymbols)
        {
            if (attr is not null)
            {
                var attributeData = AttributeHelper.GetAttribute(Type.Symbol, attr);
                locations.Add(
                    AttributeHelper.GetAttributeLocation(attributeData, Type.IdentifierLocation)
                );
            }
        }
        return locations.ToArray();
    }

    protected Location GetMemberLocation(ISymbol member)
    {
        return SymbolHelper.GetLocation(member, Type.IdentifierLocation);
    }
}
