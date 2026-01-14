using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Descriptors;

public sealed record InjectedMemberDescriptor(string MemberName, ITypeSymbol MemberType);
