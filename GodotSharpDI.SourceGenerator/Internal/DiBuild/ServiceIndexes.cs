using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// 服务索引 - 维护全局的索引信息，用于快速查找
/// 职责：提供高效的查找接口，不包含验证逻辑
/// </summary>
internal sealed class ServiceIndexes
{
    /// <summary>
    /// Host类型到节点的映射
    /// Key: Host类型 (HostA, HostB)
    /// Value: Host的TypeNode
    /// </summary>
    public ImmutableDictionary<ITypeSymbol, TypeNode> HostTypeToNode { get; }

    /// <summary>
    /// 服务类型到提供者的映射（多值）
    /// Key: 服务类型 (IServiceA, IServiceB)
    /// Value: 所有提供该服务的TypeNode列表
    /// </summary>
    public ImmutableDictionary<
        ITypeSymbol,
        ImmutableArray<TypeNode>
    > ServiceTypeToProviders { get; }

    /// <summary>
    /// User类型到节点的映射
    /// Key: User类型
    /// Value: User的TypeNode
    /// </summary>
    public ImmutableDictionary<ITypeSymbol, TypeNode> UserTypeToNode { get; }

    /// <summary>全局范围内有多个提供者的服务类型</summary>
    public ImmutableDictionary<ITypeSymbol, ImmutableArray<TypeNode>>
        DuplicateServiceProviders { get; }

    /// <summary>
    /// 服务类型 S → 提供 S 的 Host 在 WaitFor 中等待的所有注入类型集合
    /// 用于构建跨 Host 服务依赖图以检测死锁
    /// </summary>
    public ImmutableDictionary<ITypeSymbol, ImmutableHashSet<ITypeSymbol>>
        ServiceTypeToWaitForDeps { get; }

    public ServiceIndexes(
        ImmutableDictionary<ITypeSymbol, TypeNode> hostTypeToNode,
        ImmutableDictionary<ITypeSymbol, ImmutableArray<TypeNode>> serviceTypeToProviders,
        ImmutableDictionary<ITypeSymbol, TypeNode> userTypeToNode,
        ImmutableDictionary<ITypeSymbol, ImmutableArray<TypeNode>> duplicateServiceProviders,
        ImmutableDictionary<ITypeSymbol, ImmutableHashSet<ITypeSymbol>> serviceTypeToWaitForDeps
    )
    {
        HostTypeToNode = hostTypeToNode;
        ServiceTypeToProviders = serviceTypeToProviders;
        UserTypeToNode = userTypeToNode;
        DuplicateServiceProviders = duplicateServiceProviders;
        ServiceTypeToWaitForDeps = serviceTypeToWaitForDeps;
    }

    /// <summary>
    /// 构建服务索引
    /// </summary>
    public static ServiceIndexes Build(
        ImmutableArray<TypeNode> hostNodes,
        ImmutableArray<TypeNode> userNodes
    )
    {
        // 1. 构建 Host 类型到节点的映射
        var hostTypeToNode = ImmutableDictionary.CreateBuilder<ITypeSymbol, TypeNode>(
            SymbolEqualityComparer.Default
        );
        foreach (var node in hostNodes)
        {
            hostTypeToNode[node.ValidatedTypeInfo.Symbol] = node;
        }

        // 2. 构建服务类型到提供者的多值映射
        var serviceTypeToProviders = ImmutableDictionary.CreateBuilder<
            ITypeSymbol,
            ImmutableArray<TypeNode>
        >(SymbolEqualityComparer.Default);

        // 先收集所有服务类型的提供者
        var tempProviders = new Dictionary<ITypeSymbol, List<TypeNode>>(
            SymbolEqualityComparer.Default
        );

        foreach (var node in hostNodes)
        {
            foreach (var providedService in node.ProvidedServices)
            {
                if (!tempProviders.ContainsKey(providedService))
                {
                    tempProviders[providedService] = new List<TypeNode>();
                }
                tempProviders[providedService].Add(node);
            }
        }

        // 转换为不可变结构
        foreach (var kvp in tempProviders)
        {
            serviceTypeToProviders[kvp.Key] = kvp.Value.ToImmutableArray();
        }

        // 3. 构建 User 类型到节点的映射
        var userTypeToNode = ImmutableDictionary.CreateBuilder<ITypeSymbol, TypeNode>(
            SymbolEqualityComparer.Default
        );
        foreach (var node in userNodes)
        {
            userTypeToNode[node.ValidatedTypeInfo.Symbol] = node;
        }

        // 4. P2: 收集重复服务提供者
        var dupBuilder = ImmutableDictionary.CreateBuilder<
            ITypeSymbol, ImmutableArray<TypeNode>>(SymbolEqualityComparer.Default);
        foreach (var kvp in tempProviders)
            if (kvp.Value.Count > 1)
                dupBuilder[kvp.Key] = kvp.Value.ToImmutableArray();

        // 5. P1: 构建服务类型 → WaitFor 依赖类型映射（用于跨 Host 死锁检测）
        var waitForDepsBuilder = ImmutableDictionary.CreateBuilder<
            ITypeSymbol, ImmutableHashSet<ITypeSymbol>>(SymbolEqualityComparer.Default);

        foreach (var node in hostNodes)
        {
            foreach (var member in node.ValidatedTypeInfo.Members)
            {
                if (!member.IsProvideMember || !member.HasWaitFor) continue;

                foreach (var exposedType in member.ExposedTypes)
                {
                    var injectDeps = member.WaitFor
                        .Select(fn => node.ValidatedTypeInfo.Members
                            .FirstOrDefault(m => m.Symbol.Name == fn && m.IsInjectMember))
                        .Where(m => m != null)
                        .Select(m => (ITypeSymbol)m!.MemberType)
                        .ToImmutableHashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

                    if (!injectDeps.IsEmpty)
                        waitForDepsBuilder[(ITypeSymbol)exposedType] = injectDeps;
                }
            }
        }

        return new ServiceIndexes(
            hostTypeToNode.ToImmutable(),
            serviceTypeToProviders.ToImmutable(),
            userTypeToNode.ToImmutable(),
            dupBuilder.ToImmutable(),
            waitForDepsBuilder.ToImmutable()
        );
    }

    /// <summary>
    /// 查找提供指定服务的所有Host
    /// </summary>
    public ImmutableArray<TypeNode> FindProviders(ITypeSymbol serviceType)
    {
        return ServiceTypeToProviders.TryGetValue(serviceType, out var providers)
            ? providers
            : ImmutableArray<TypeNode>.Empty;
    }

    /// <summary>
    /// 检查服务是否有提供者
    /// </summary>
    public bool HasProvider(ITypeSymbol serviceType)
    {
        return ServiceTypeToProviders.ContainsKey(serviceType);
    }
}
