using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// Class validation result
/// </summary>
internal sealed record ClassValidationResult(
    ValidatedTypeInfo? TypeInfo,
    ImmutableArray<Diagnostic> Diagnostics
);
