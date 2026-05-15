namespace GodotSharpDI.Runtime;

/// <summary>
/// Tracks whether a service has been created, is still pending, or has failed.
/// Used internally by generated Scope classes.
/// </summary>
public enum ServiceState
{
    NotCreated,
    Created,
    Failed
}

/// <summary>
/// Cache entry for a single service implementation type.
/// Used internally by generated Scope classes.
/// Fields are volatile to ensure visibility if accessed from background threads
/// (e.g. async provider completions dispatched via CallDeferred).
/// </summary>
public sealed class ServiceCacheEntry
{
    public volatile ServiceState State = ServiceState.NotCreated;
    public volatile object? Instance = null;
}
