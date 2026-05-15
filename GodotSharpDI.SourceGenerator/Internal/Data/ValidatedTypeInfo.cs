using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// Type information (after validation)
/// </summary>
internal sealed record ValidatedTypeInfo(
    INamedTypeSymbol Symbol,
    Location Location,
    TypeRole Role,
    bool ImplementsIDependenciesResolved,
    bool IsNode,
    ImmutableArray<MemberInfo> Members,
    ModulesInfo? ModulesInfo
);
