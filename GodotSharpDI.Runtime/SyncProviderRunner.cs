using System;

namespace GodotSharpDI.Runtime;

/// <summary>
/// Runs a synchronous service provider with try-catch error handling.
/// Replaces the inline try-catch pattern in ServiceProvisionPhase.GenerateSyncProvide.
/// </summary>
public static class SyncProviderRunner
{
    /// <summary>
    /// Execute a synchronous provider and register the result with the scope.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="factory">
    /// Factory function that creates the service instance.
    /// Example: <c>() => MyHost.CreateService()</c>
    /// </param>
    /// <param name="provide">
    /// Registration callback. Typically <c>(inst, pt) => scope.ProvideService&lt;T&gt;(inst, pt)</c>
    /// </param>
    /// <param name="providerType">The provider type name, for diagnostics.</param>
    /// <param name="errorOutput">Error output callback. Typically <c>GD.PrintErr</c>.</param>
    public static void Run<T>(
        Func<T> factory,
        Action<T?, string> provide,
        string providerType,
        Action<string> errorOutput) where T : class
    {
        try
        {
            var instance = factory();
            provide(instance, providerType);
        }
        catch (Exception ex)
        {
            ErrorReporter.ReportProviderThrew(typeof(T).Name, ex, errorOutput);
            provide(null, providerType);
        }
    }
}
