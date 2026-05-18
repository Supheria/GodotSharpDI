using System;
using System.Threading;
using System.Threading.Tasks;

namespace GodotSharpDI.Runtime;

/// <summary>
/// Runs an asynchronous service provider with cancellation and error handling.
/// Replaces the ~35-line ProvideAsync_xxx method generated per async member.
/// </summary>
public static class AsyncProviderRunner
{
    /// <summary>
    /// Execute an async provider and register the result with the scope.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="task">
    /// The async task that produces the service instance.
    /// </param>
    /// <param name="provide">
    /// Registration callback. Typically <c>(inst, pt) => scope.ProvideService&lt;T&gt;(inst, pt)</c>
    /// </param>
    /// <param name="providerType">The provider type name, for diagnostics.</param>
    /// <param name="ct">
    /// Cancellation token (typically __lifetime_cancellation_tokens.Token).
    /// Cancelled when the node exits the scene tree.
    /// </param>
    /// <param name="dispatchToMainThread">
    /// Main-thread dispatcher. Typically <c>action => Callable.From(action).CallDeferred()</c>.
    /// </param>
    public static async Task Run<T>(
        Task<T> task,
        Action<T?, string> provide,
        string providerType,
        CancellationToken ct,
        Action<Action> dispatchToMainThread) where T : class
    {
        try
        {
            var result = await task;
            ct.ThrowIfCancellationRequested();

            dispatchToMainThread(() =>
            {
                if (ct.IsCancellationRequested) return;
                provide(result, providerType);
            });
        }
        catch (OperationCanceledException)
        {
            // Node exited scene tree – silent cancellation
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;

            ErrorReporter.ReportAsyncProviderThrew(typeof(T).Name, ex);

            dispatchToMainThread(() =>
            {
                if (ct.IsCancellationRequested) return;
                provide(null, providerType);
            });
        }
    }
}
