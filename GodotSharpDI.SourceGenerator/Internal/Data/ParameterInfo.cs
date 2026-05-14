using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// Parameter information
/// </summary>
internal sealed record ParameterInfo(IParameterSymbol Symbol, Location Location, ITypeSymbol Type);
