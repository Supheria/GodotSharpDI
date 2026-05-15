using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.DiBuild;

/// <summary>
/// Node builder collection
/// Responsibility: Build corresponding nodes for various role types
/// </summary>
internal static class NodeBuilders
{
    public static ImmutableArray<TypeNode> BuildHostNodes(
        ImmutableArray<ValidatedTypeInfo> hosts,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        var nodes = ImmutableArray.CreateBuilder<TypeNode>();

        foreach (var host in hosts)
        {
            try
            {
                var node = BuildHostNode(host);
                nodes.Add(node);
            }
            catch (Exception ex)
            {
                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.NodeBuildFailed,
                        host.Location,
                        "Host",
                        host.Symbol.Name,
                        ex.Message
                    )
                );
            }
        }

        return nodes.ToImmutable();
    }

    public static ImmutableArray<TypeNode> BuildUserNodes(
        ImmutableArray<ValidatedTypeInfo> users,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        var nodes = ImmutableArray.CreateBuilder<TypeNode>();

        foreach (var user in users)
        {
            try
            {
                var node = BuildUserNode(user);
                nodes.Add(node);
            }
            catch (Exception ex)
            {
                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.NodeBuildFailed,
                        user.Location,
                        "User",
                        user.Symbol.Name,
                        ex.Message
                    )
                );
            }
        }

        return nodes.ToImmutable();
    }

    /// <summary>
    /// Build Scope nodes
    /// </summary>
    public static ImmutableArray<ScopeNode> BuildScopeNodes(
        ImmutableArray<ValidatedTypeInfo> scopes,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        ServiceIndexes indexes
    )
    {
        var nodes = ImmutableArray.CreateBuilder<ScopeNode>();

        foreach (var scope in scopes)
        {
            try
            {
                var node = BuildScopeNode(scope, symbols, diagnostics, indexes);
                if (node != null)
                {
                    nodes.Add(node);
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.NodeBuildFailed,
                        scope.Location,
                        "Scope",
                        scope.Symbol.Name,
                        ex.Message
                    )
                );
            }
        }

        return nodes.ToImmutable();
    }

    private static TypeNode BuildHostNode(ValidatedTypeInfo host)
    {
        var dependencies = ImmutableArray.CreateBuilder<DependencyEdge>();
        var providedServices = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        // New: Collect mapping from service exposed type to implementation type
        var serviceImplMap = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, INamedTypeSymbol>(
            SymbolEqualityComparer.Default
        );

        // Collect Inject member dependencies
        foreach (var member in host.Members)
        {
            if (member.IsInjectMember)
            {
                dependencies.Add(
                    new DependencyEdge(
                        TargetType: member.MemberType,
                        Location: member.Location,
                        Source: DependencySource.InjectMember
                    )
                );
            }
        }

        // Collect WaitFor dependencies from Provide members
        foreach (var member in host.Members)
        {
            if (member.IsProvideMember && member.HasWaitFor)
            {
                // Get the first service type provided by this Provide member (used to identify this member)
                var providedType = member.ExposedTypes.FirstOrDefault();

                foreach (var waitForFieldName in member.WaitFor)
                {
                    // Find the field referenced by WaitFor
                    var waitForField = host.Members.FirstOrDefault(m =>
                        m.Symbol.Name == waitForFieldName
                    );

                    if (waitForField != null && waitForField.IsInjectMember)
                    {
                        dependencies.Add(
                            new DependencyEdge(
                                TargetType: waitForField.MemberType,
                                Location: member.Location,
                                Source: DependencySource.WaitForMember,
                                SourceMemberName: member.Symbol.Name, // Record source member name
                                SourceProvidedType: providedType // Record the service type provided by the source member
                            )
                        );
                    }
                }
            }
        }

        // Collect services provided by Provide members, and build mapping from exposed types to implementation types
        foreach (var member in host.Members)
        {
            if (member.IsProvideMember)
            {
                // Add all exposed types
                providedServices.AddRange(member.ExposedTypes);

                // Build mapping: exposed type -> implementation type
                // member.MemberType is the actual implementation type returned by this member
                var implementationType = member.MemberType;

                foreach (var exposedType in member.ExposedTypes)
                {
                    // If exposed type and implementation type are different, build mapping
                    // (If they are the same, mapping can also be added for consistency)
                    serviceImplMap[exposedType] = implementationType;
                }
            }
        }

        return new TypeNode(
            ValidatedTypeInfo: host,
            Dependencies: dependencies.ToImmutable(),
            ProvidedServices: providedServices.ToImmutable(),
            ServiceImplementationMap: serviceImplMap.ToImmutable()
        );
    }

    private static TypeNode BuildUserNode(ValidatedTypeInfo user)
    {
        var dependencies = ImmutableArray.CreateBuilder<DependencyEdge>();

        // Collect injection dependencies
        foreach (var member in user.Members)
        {
            if (member.IsInjectMember)
            {
                dependencies.Add(
                    new DependencyEdge(
                        TargetType: member.MemberType,
                        Location: member.Location,
                        Source: DependencySource.InjectMember
                    )
                );
            }
        }

        return new TypeNode(
            ValidatedTypeInfo: user,
            Dependencies: dependencies.ToImmutable(),
            ProvidedServices: ImmutableArray<INamedTypeSymbol>.Empty,
            ServiceImplementationMap: ImmutableDictionary<INamedTypeSymbol, INamedTypeSymbol>.Empty
        );
    }

    private static ScopeNode? BuildScopeNode(
        ValidatedTypeInfo scope,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        ServiceIndexes indexes
    )
    {
        if (scope.ModulesInfo == null)
            return null;

        var hosts = scope.ModulesInfo.Hosts;

        // Validate Hosts
        ValidateScopeHosts(scope, hosts, symbols, diagnostics);

        // Validate service type conflicts within Scope
        ValidateScopeServiceConflicts(scope, hosts, indexes, diagnostics);

        // Check if empty
        if (hosts.IsEmpty)
        {
            diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ScopeModulesEmpty,
                    scope.Location,
                    scope.Symbol.Name
                )
            );
        }

        return new ScopeNode(ValidatedTypeInfo: scope, ExpectHosts: hosts);
    }

    private static void ValidateScopeHosts(
        ValidatedTypeInfo scope,
        ImmutableArray<INamedTypeSymbol> hosts,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        foreach (var type in hosts)
        {
            var isHost = type.HasAttribute(symbols.HostAttribute);

            if (!isHost)
            {
                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ScopeModulesHostMustBeHost,
                        scope.Location,
                        scope.Symbol.Name,
                        type.ToDisplayString()
                    )
                );
            }
        }
    }

    /// <summary>
    /// Validate service type conflicts within Scope
    /// Refactored version: Uses indexes directly, logic is clearer
    /// </summary>
    private static void ValidateScopeServiceConflicts(
        ValidatedTypeInfo scope,
        ImmutableArray<INamedTypeSymbol> hosts,
        ServiceIndexes indexes,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        // Collect all services provided by Hosts within this Scope
        var scopeServices = new Dictionary<ITypeSymbol, List<ServiceProvider>>(
            SymbolEqualityComparer.Default
        );

        foreach (var hostType in hosts)
        {
            // Find Host node
            if (!indexes.HostTypeToNode.TryGetValue(hostType, out var hostNode))
                continue;

            // Collect all services provided by this Host
            foreach (var providedService in hostNode.ProvidedServices)
            {
                if (!scopeServices.ContainsKey(providedService))
                {
                    scopeServices[providedService] = new List<ServiceProvider>();
                }

                var memberName = FindProviderMemberName(hostNode, providedService);
                scopeServices[providedService]
                    .Add(new ServiceProvider(hostType, hostNode, memberName));
            }
        }

        // Detect conflicts: If a service has multiple providers, report error
        foreach (var kvp in scopeServices)
        {
            var serviceType = kvp.Key;
            var providers = kvp.Value;

            if (providers.Count > 1)
            {
                // Build provider list string
                var providerDescriptions = providers
                    .Select(p => $"{p.HostType.ToDisplayString()}.{p.MemberName}")
                    .ToList();

                var providersText = string.Join(", ", providerDescriptions);

                diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ScopeServiceTypeDuplicated,
                        scope.Location,
                        scope.Symbol.Name,
                        serviceType.ToDisplayString(),
                        providersText
                    )
                );
            }
        }
    }

    /// <summary>
    /// Find the member name that provides a specific service type
    /// </summary>
    private static string FindProviderMemberName(TypeNode hostNode, ITypeSymbol exposedType)
    {
        foreach (var member in hostNode.ValidatedTypeInfo.Members)
        {
            if (member.IsProvideMember)
            {
                foreach (var exposed in member.ExposedTypes)
                {
                    if (SymbolEqualityComparer.Default.Equals(exposed, exposedType))
                    {
                        return member.Symbol.Name;
                    }
                }
            }
        }

        return "<unknown>";
    }

    /// <summary>
    /// Service provider information
    /// </summary>
    private record ServiceProvider(ITypeSymbol HostType, TypeNode HostNode, string MemberName);
}
