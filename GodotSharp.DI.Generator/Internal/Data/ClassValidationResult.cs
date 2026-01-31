using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Data;

/// <summary>
/// 类验证结果
/// </summary>
internal sealed record ClassValidationResult(
    TypeInfo? TypeInfo,
    ImmutableArray<Diagnostic> Diagnostics
);
