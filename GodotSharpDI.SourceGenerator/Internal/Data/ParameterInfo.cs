using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// 参数信息
/// </summary>
internal sealed record ParameterInfo(IParameterSymbol Symbol, Location Location, ITypeSymbol Type);
