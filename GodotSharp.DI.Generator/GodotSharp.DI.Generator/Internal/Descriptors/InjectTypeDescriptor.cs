using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Descriptors;

public sealed record InjectTypeDescriptor(ITypeSymbol Symbol, string Name);
