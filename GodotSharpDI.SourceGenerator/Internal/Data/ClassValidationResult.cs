using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// 类验证结果
/// </summary>
internal sealed record ClassValidationResult(
    ValidateTypeInfo? TypeInfo,
    ImmutableArray<Diagnostic> Diagnostics
);
