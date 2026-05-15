using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// Service index - Maintains global index information for fast lookups
/// Responsibility: Provide efficient lookup interfaces, does not contain validation logic
/// </summary>
internal sealed class ServiceIndexes
{
    /// <summary>
    /// Host type to node mapping
    /// Key: Host type (HostA, HostB)
    /// Value: Host's TypeNode
    /// </summary>
    public ImmutableDictionary<ITypeSymbol, TypeNode> HostTypeToNode { get; }

    /// <summary>
    /// Service type to providers mapping (multi-value)
    /// Key: Service type (IServiceA, IServiceB)
    /// Value: List of all TypeNodes providing this service
    /// </summary>
    public ImmutableDictionary<
        ITypeSymbol,
        ImmutableArray<TypeNode>
    > ServiceTypeToProviders { get; }

    /// <summary>
    /// User type to node mapping
    /// Key: User type
    /// Value: User's TypeNode
    /// </summary>
    public ImmutableDictionary<ITypeSymbol, TypeNode> UserTypeToNode { get; }

    /// <summary>Service types with multiple providers in global scope</summary>
    public ImmutableDictionary<ITypeSymbol, ImmutableArray<TypeNode>>
        DuplicateServiceProviders { get; }

    /// <summary>
    /// Service type S → Set of all injection types that the Host providing S waits for in WaitFor
    /// Used to build cross-Host service dependency graph for deadlock detection
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
    /// Build service index
    /// </summary>
    public static ServiceIndexes Build(
        ImmutableArray<TypeNode> hostNodes,
        ImmutableArray<TypeNode> userNodes
    )
    {
        // 1. Build Host type to node mapping
        var hostTypeToNode = ImmutableDictionary.CreateBuilder<ITypeSymbol, TypeNode>(
            SymbolEqualityComparer.Default
        );
        foreach (var node in hostNodes)
        {
            hostTypeToNode[node.ValidatedTypeInfo.Symbol] = node;
        }

        // 2. Build service type to providers multi-value mapping
        var serviceTypeToProviders = ImmutableDictionary.CreateBuilder<
            ITypeSymbol,
            ImmutableArray<TypeNode>
        >(SymbolEqualityComparer.Default);

        // First collect all providers for each service type
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

        // Convert to immutable structure
        foreach (var kvp in tempProviders)
        {
            serviceTypeToProviders[kvp.Key] = kvp.Value.ToImmutableArray();
        }

        // 3. Build User type to node mapping
        var userTypeToNode = ImmutableDictionary.CreateBuilder<ITypeSymbol, TypeNode>(
            SymbolEqualityComparer.Default
        );
        foreach (var node in userNodes)
        {
            userTypeToNode[node.ValidatedTypeInfo.Symbol] = node;
        }

        // 4. P2: Collect duplicate service providers
        var dupBuilder = ImmutableDictionary.CreateBuilder<
            ITypeSymbol, ImmutableArray<TypeNode>>(SymbolEqualityComparer.Default);
        foreach (var kvp in tempProviders)
            if (kvp.Value.Count > 1)
                dupBuilder[kvp.Key] = kvp.Value.ToImmutableArray();

        // 5. P1: Build service type → WaitFor dependency type mapping (for cross-Host deadlock detection)
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
    /// Find all Hosts providing the specified service
    /// </summary>
    public ImmutableArray<TypeNode> FindProviders(ITypeSymbol serviceType)
    {
        return ServiceTypeToProviders.TryGetValue(serviceType, out var providers)
            ? providers
            : ImmutableArray<TypeNode>.Empty;
    }

    /// <summary>
    /// Check if a service has providers
    /// </summary>
    public bool HasProvider(ITypeSymbol serviceType)
    {
        return ServiceTypeToProviders.ContainsKey(serviceType);
    }
}
