namespace GodotSharpDI.Abstractions;

/// <summary>
/// Implement this interface on a Host or User node to receive a callback
/// when all <c>[Inject]</c> members have been resolved (success or failure).
/// </summary>
/// <remarks>
/// <para><b>Timing guarantee:</b></para>
/// <list type="bullet">
///   <item>Called exactly once, after ALL <c>[Inject]</c> dependencies are resolved.</item>
///   <item>Synchronous <c>[Provide]</c> services in the same Host are complete when called.</item>
///   <item>
///     Asynchronous <c>[Provide]</c> services (<c>Task&lt;T&gt;</c>) may still be executing.
///     Use <c>IsXxxInjectionReady</c> properties to check individual injection status.
///   </item>
/// </list>
/// </remarks>
public interface IDependenciesResolved
{
    void OnDependenciesResolved(bool isAllDependenciesReady);
}
