using System;

namespace GodotSharpDI.Abstractions;

/// <summary>
/// Specifies which <c>[Host]</c> types are managed by a <c>[Scope]</c> (IScope) class.
/// The scope will instantiate and coordinate dependency resolution for the listed hosts.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ModulesAttribute : Attribute
{
    /// <summary>
    /// The <c>[Host]</c> types to include in this scope.
    /// </summary>
    public Type[] Hosts { get; set; } = [];
}
