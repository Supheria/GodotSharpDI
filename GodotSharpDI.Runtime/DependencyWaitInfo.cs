using System;

namespace GodotSharpDI.Runtime;

/// <summary>
/// Information about a pending dependency resolution request.
/// Used by the generated Scope code to track waiters.
/// </summary>
public sealed class DependencyWaitInfo
{
    /// <summary>Callback to invoke when the dependency is resolved. Null means failure.</summary>
    public Action<object?> ResultCallback { get; }

    /// <summary>Timestamp (DateTime.Now.Ticks) when the request was made.</summary>
    public long RequestTicks { get; }

    /// <summary>Name of the requesting type, for diagnostics.</summary>
    public string RequestorType { get; }

    /// <summary>Scope chain path, for diagnostics.</summary>
    public string ScopeChain { get; }

    /// <summary>Dependency chain path, for diagnostics.</summary>
    public string DependencyChain { get; }

    public DependencyWaitInfo(
        Action<object?> resultCallback,
        long requestTicks,
        string requestorType,
        string scopeChain,
        string dependencyChain)
    {
        ResultCallback = resultCallback;
        RequestTicks = requestTicks;
        RequestorType = requestorType;
        ScopeChain = scopeChain;
        DependencyChain = dependencyChain;
    }
}
