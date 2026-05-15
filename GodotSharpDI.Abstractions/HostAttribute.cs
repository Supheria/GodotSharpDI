using System;

namespace GodotSharpDI.Abstractions;

/// <summary>
/// Marks a class as a service provider (Host) in the DI framework.
/// A Host exposes services via <c>[Provide]</c> members and may also consume
/// services via <c>[Inject]</c> members. Must be a <c>Godot.Node</c> subclass.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class HostAttribute : Attribute { }
