using System;

namespace GodotSharpDI.Abstractions;

/// <summary>
/// Marks a class as a service consumer (User) in the DI framework.
/// A User consumes services via <c>[Inject]</c> members. Must be a <c>Godot.Node</c> subclass.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class UserAttribute : Attribute { }
