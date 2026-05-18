using System;
using System.Collections.Generic;

namespace GodotSharpDI.Runtime;

/// <summary>
/// Tracks WaitFor dependency edges at runtime and detects cycles via DFS.
/// An instance is created per Scope node; the generated code calls
/// <see cref="TrackAndDetect"/> inside a <c>#if DEBUG</c> block.
///
/// The wait-for graph is bipartite (providers → services), so we maintain
/// a service-to-provider mapping (via <see cref="RegisterServiceProvider"/>)
/// and build a provider-to-provider dependency graph for cycle detection.
/// </summary>
public sealed class DeadlockDetector
{
    // provider → set of service type names it waits for
    private readonly Dictionary<string, HashSet<string>> _providerWaitsFor = new();
    // service type name → set of providers that expose it
    private readonly Dictionary<string, HashSet<string>> _serviceToProviders = new();
    // provider → set of providers it depends on (provider-to-provider graph)
    private readonly Dictionary<string, HashSet<string>> _providerDeps = new();

    /// <summary>
    /// Register that <paramref name="providerName"/> provides <paramref name="serviceTypeName"/>.
    /// The generated code calls this for each service mapping at initialization.
    /// </summary>
    public void RegisterServiceProvider(string providerName, string serviceTypeName)
    {
        if (!_serviceToProviders.TryGetValue(serviceTypeName, out var providers))
        {
            providers = new HashSet<string>();
            _serviceToProviders[serviceTypeName] = providers;
        }
        providers.Add(providerName);
    }

    /// <summary>
    /// Parse the <c>GDI_WF:</c> prefix from <paramref name="requestorType"/>,
    /// record the edge, and run DFS to detect a cycle.
    /// If a cycle is found, an error is reported via <paramref name="errorOutput"/>.
    /// </summary>
    public void TrackAndDetect(string requestorType, string waitingForTypeName, Action<string> errorOutput)
    {
        const string prefix = "GDI_WF:";
        if (!requestorType.StartsWith(prefix))
            return;

        var rest = requestorType.Substring(prefix.Length);
        var colonIdx = rest.IndexOf(':');
        if (colonIdx < 0)
            return;

        var providerName = rest.Substring(0, colonIdx);

        // Record that providerName waits for waitingForTypeName
        if (!_providerWaitsFor.TryGetValue(providerName, out var waitsFor))
        {
            waitsFor = new HashSet<string>();
            _providerWaitsFor[providerName] = waitsFor;
        }
        waitsFor.Add(waitingForTypeName);

        // Build provider-to-provider edges and check for cycles
        if (!_providerDeps.TryGetValue(providerName, out var deps))
        {
            deps = new HashSet<string>();
            _providerDeps[providerName] = deps;
        }

        // Find all providers that expose waitingForTypeName
        if (!_serviceToProviders.TryGetValue(waitingForTypeName, out var serviceProviders))
            return;

        foreach (var targetProvider in serviceProviders)
        {
            if (targetProvider == providerName)
            {
                // Self-dependency
                ErrorReporter.ReportError(
                    "[GodotSharpDI] Runtime WaitFor Deadlock: " + providerName + " -> " + waitingForTypeName,
                    errorOutput);
                continue;
            }

            // Add edge: providerName depends on targetProvider
            if (!deps.Add(targetProvider))
                continue; // Edge already exists, skip duplicate cycle check

            // Check for cycle: search from targetProvider back to providerName
            var cycle = FindCycle(targetProvider, providerName, new HashSet<string>(), new List<string>());
            if (cycle != null)
            {
                var path = providerName + " -> " + string.Join(" -> ", cycle);
                ErrorReporter.ReportError("[GodotSharpDI] Runtime WaitFor Deadlock: " + path, errorOutput);
            }
        }
    }

    /// <summary>
    /// DFS through the provider-to-provider dependency graph.
    /// Returns the path from <paramref name="current"/> to <paramref name="target"/>
    /// (including target) or <c>null</c> if no path exists.
    /// </summary>
    internal List<string>? FindCycle(
        string current,
        string target,
        HashSet<string> visited,
        List<string> path)
    {
        if (current == target)
        {
            var result = new List<string>(path);
            result.Add(current);
            return result;
        }

        if (visited.Contains(current))
            return null;

        if (!_providerDeps.TryGetValue(current, out var nbrs))
            return null;

        visited.Add(current);
        path.Add(current);

        foreach (var nb in nbrs)
        {
            var r = FindCycle(nb, target, visited, path);
            if (r != null)
                return r;
        }

        path.RemoveAt(path.Count - 1);
        return null;
    }
}
