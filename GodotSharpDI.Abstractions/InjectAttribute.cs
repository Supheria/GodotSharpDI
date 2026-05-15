using System;

namespace GodotSharpDI.Abstractions;

/// <summary>
/// Marks a field or property for dependency injection in a <c>[Host]</c> or <c>[User]</c> class.
/// The framework automatically resolves and assigns the value at the appropriate lifecycle stage.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class InjectAttribute : Attribute
{
    /// <summary>
    /// When <c>true</c>, generates a partial method callback invoked when injection fails.
    /// Implement <c>On{MemberName}InjectionFailed()</c> to handle the failure.
    /// </summary>
    public bool FailureCallback { get; set; }

    /// <summary>
    /// When <c>true</c>, generates a partial method callback invoked when injection succeeds.
    /// Implement <c>On{MemberName}InjectionReady()</c> to handle the success.
    /// </summary>
    public bool ReadyCallback { get; set; }
}
