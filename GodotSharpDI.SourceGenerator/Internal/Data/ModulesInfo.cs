using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// Module information
/// </summary>
internal sealed record ModulesInfo(ImmutableArray<INamedTypeSymbol> Hosts);
