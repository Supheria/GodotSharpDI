using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// 服务提供者映射表类型
/// Key: 暴露的服务类型, Value: 提供该服务的类型信息
/// </summary>
internal sealed class ServiceProviderMap : Dictionary<ITypeSymbol, TypeNode>
{
    public ServiceProviderMap()
        : base(SymbolEqualityComparer.Default) { }
}
