using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.Generator.Internal.Data;

/// <summary>
/// 构造函数信息
/// </summary>
internal sealed record ConstructorInfo(
    IMethodSymbol Symbol,
    Location Location,
    ImmutableArray<ParameterInfo> Parameters
);
