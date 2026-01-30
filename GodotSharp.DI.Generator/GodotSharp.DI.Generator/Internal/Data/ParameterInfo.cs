using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Data;

/// <summary>
/// 参数信息
/// </summary>
internal sealed record ParameterInfo(IParameterSymbol Symbol, Location Location, ITypeSymbol Type);
