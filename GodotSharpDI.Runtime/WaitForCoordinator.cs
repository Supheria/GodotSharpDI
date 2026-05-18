using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GodotSharpDI.Runtime;

/// <summary>
/// Coordinates WaitFor dependency resolution for a single [Provide] member.
/// Replaces the lambda + ContinueWith pattern in WaitForPhase.GenerateForMember.
///
/// Thread Safety: <see cref="_remaining"/> uses <see cref="Interlocked.Decrement"/>
/// because callbacks may fire from background threads (e.g. async providers).
/// </summary>
public class WaitForCoordinator
{
    private int _remaining;
    private readonly Func<Task> _onAllResolved;

    /// <summary>
    /// Create a new WaitFor coordinator.
    /// </summary>
    /// <param name="depCount">Number of dependencies to wait for.</param>
    /// <param name="onAllResolved">
    /// Async callback invoked when all dependencies have settled (success or failure).
    /// </param>
    public WaitForCoordinator(int depCount, Func<Task> onAllResolved)
    {
        _remaining = depCount;
        _onAllResolved = onAllResolved;
    }

    /// <summary>
    /// Register a callback on a dependency's callback list.
    /// When the dependency resolves (success or failure), this callback decrements the
    /// remaining count. When it reaches zero, <see cref="_onAllResolved"/> is invoked.
    /// </summary>
    /// <param name="callbackList">
    /// The dependency's callback list (from InjectionGenerator).
    /// </param>
    /// <param name="depName">The dependency member name, for error reporting.</param>
    /// <param name="memberName">The Provide member name, for error reporting.</param>
    /// <param name="dispatchToMainThread">
    /// Main-thread dispatcher. Typically <c>action => Callable.From(action).CallDeferred()</c>.
    /// </param>
    public void Register(
        List<Action<bool>> callbackList,
        string depName,
        string memberName,
        Action<Action> dispatchToMainThread)
    {
        callbackList.Add(ok =>
        {
            if (!ok)
            {
                ErrorReporter.ReportError(
                    $"[GodotSharpDI] WaitFor: dependency '{depName}' for '{memberName}' failed");
            }

            if (Interlocked.Decrement(ref _remaining) == 0)
            {
                _ = _onAllResolved().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        var ex = t.Exception?.GetBaseException();
                        dispatchToMainThread(() =>
                            ErrorReporter.ReportError(
                                $"[GodotSharpDI] WaitFor callback threw: {ex}"));
                    }
                }, TaskScheduler.Default);
            }
        });
    }
}
