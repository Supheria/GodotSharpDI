using System;

namespace GodotSharp.DI.Abstractions;

[AttributeUsage(AttributeTargets.Constructor)]
public sealed class InjectConstructorAttribute : Attribute { }
