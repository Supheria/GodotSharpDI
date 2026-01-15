using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Descriptors;

public sealed record ProvidedServiceDescriptor(
    ImmutableArray<ITypeSymbol> ServiceTypes, // 暴露的接口类型们
    ITypeSymbol MemberType, // 成员的实际类型
    string MemberName // 成员名
);
