using System;

namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Constructor)]
public sealed class InjectConstructorAttribute : Attribute { }
