using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Data;

/// <summary>
/// 依赖边
/// </summary>
internal sealed record DependencyEdge(
    ITypeSymbol TargetType,
    Location Location,
    DependencySource Source
);
