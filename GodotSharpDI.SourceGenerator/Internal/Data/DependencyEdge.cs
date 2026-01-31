using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// 依赖边
/// </summary>
internal sealed record DependencyEdge(
    ITypeSymbol TargetType,
    Location Location,
    DependencySource Source
);
