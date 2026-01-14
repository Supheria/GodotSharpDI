using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Descriptors;

public sealed record ProvidedServiceDescriptor(
    ITypeSymbol ServiceType,
    ITypeSymbol MemberType,
    string MemberName
);
