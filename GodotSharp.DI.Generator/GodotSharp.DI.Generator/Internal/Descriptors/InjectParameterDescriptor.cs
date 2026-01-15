using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Descriptors;

public sealed record InjectParameterDescriptor(ITypeSymbol ParameterType, string ParameterName);
