using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed class ServiceGraph
{
    // 所有服务描述
    public List<ServiceDescriptor> Services { get; } = new();

    // 按实现类型分组：一个实现类型 → 多个暴露服务类型
    public Dictionary<INamedTypeSymbol, List<ServiceDescriptor>> ServicesByImplementation { get; } =
        new(SymbolEqualityComparer.Default);

    // Host / User / Scope 信息，复用你已有的 Info 类型
    public List<HostServiceInfo> Hosts { get; } = new();
    public List<UserDependencyInfo> Users { get; } = new();
    public List<ScopeServiceInfo> Scopes { get; } = new();

    // 依赖图：服务类型 → 使用该服务的类型列表（User / Host / Scope）
    public Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> Edges { get; } =
        new(SymbolEqualityComparer.Default);

    // 未来可用：诊断信息
    public List<Diagnostic> Diagnostics { get; } = new();
}
