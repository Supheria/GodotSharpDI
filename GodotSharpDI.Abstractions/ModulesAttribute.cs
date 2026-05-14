using System;

namespace GodotSharpDI.Abstractions;

/// <summary>
/// Specifies which <c>[Host]</c> types are managed by a <c>[Scope]</c> (IScope) class.
/// The scope will instantiate and coordinate dependency resolution for the listed hosts.
/// </summary>
/// <example>
/// <code>
/// [Modules(typeof(GameHost), typeof(PlayerHost))]
/// public partial class GameScope : Node, IScope { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ModulesAttribute : Attribute
{
    /// <summary>
    /// The <c>[Host]</c> types to include in this scope.
    /// </summary>
    public Type[] Hosts { get; }

    /// <summary>
    /// Creates a new <see cref="ModulesAttribute"/> with the specified host types.
    /// </summary>
    /// <param name="hosts">The <c>[Host]</c> types to include in this scope.</param>
    public ModulesAttribute(params Type[] hosts)
    {
        Hosts = hosts;
    }
}
