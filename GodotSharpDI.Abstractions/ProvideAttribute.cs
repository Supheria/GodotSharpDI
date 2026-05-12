using System;

namespace GodotSharpDI.Abstractions;

/// <summary>
/// Marks a field, property, or method as a service provision point in a <c>[Host]</c> class.
/// The framework calls this member to create and register service instances.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ProvideAttribute : Attribute
{
    /// <summary>
    /// The interface or base types to expose. When empty, the member's own type is used.
    /// </summary>
    public Type[] ExposedTypes { get; set; } = [];

    /// <summary>
    /// Names of <c>[Inject]</c> members that must be resolved before this service is provided.
    /// Enables ordered initialization via <c>TaskCompletionSource</c> synchronization.
    /// </summary>
    public string[] WaitFor { get; set; } = [];
}
