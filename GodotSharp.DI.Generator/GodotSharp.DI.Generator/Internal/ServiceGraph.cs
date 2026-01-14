using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed class ServiceGraph
{
    public List<ServiceDescriptor> Services { get; }
    public List<HostDescriptor> Hosts { get; }
    public List<UserDescriptor> Users { get; }
    public List<ScopeDescriptor> Scopes { get; }

    // 按实现类型分组：一个实现类型 → 多个暴露服务类型
    public Dictionary<INamedTypeSymbol, List<ServiceDescriptor>> ServicesByImplementation { get; } =
        new(SymbolEqualityComparer.Default);

    // 依赖图：服务类型 → 使用该服务的类型列表（User / Host / Scope）
    public Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> Edges { get; } =
        new(SymbolEqualityComparer.Default);

    // 未来可用：诊断信息
    public List<Diagnostic> Diagnostics { get; } = new();
}
