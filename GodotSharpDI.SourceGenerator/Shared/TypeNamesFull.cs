namespace GodotSharpDI.SourceGenerator.Shared;

public static class TypeNamesFull
{
    // 新增特性
    public const string ProviderAttribute = "GodotSharpDI.Abstractions.ProviderAttribute";
    public const string ProvidesAttribute = "GodotSharpDI.Abstractions.ProvidesAttribute";

    // 现有特性（保持向后兼容）
    public const string InjectAttribute = "GodotSharpDI.Abstractions.InjectAttribute";
    public const string InjectConstructorAttribute =
        "GodotSharpDI.Abstractions.InjectConstructorAttribute";
    public const string ModulesAttribute = "GodotSharpDI.Abstractions.ModulesAttribute";
    public const string SingletonAttribute = "GodotSharpDI.Abstractions.SingletonAttribute";
    public const string HostAttribute = "GodotSharpDI.Abstractions.HostAttribute";
    public const string UserAttribute = "GodotSharpDI.Abstractions.UserAttribute";

    public const string IScope = "GodotSharpDI.Abstractions.IScope";
    public const string IDependenciesResolved = "GodotSharpDI.Abstractions.IDependenciesResolved";

    public const string GodotNode = "Godot.Node";

    // System
    public const string IDisposable = "System.IDisposable";
    public const string Task = "System.Threading.Tasks.Task";
    public const string GenericTask = "System.Threading.Tasks.Task`1";
    public const string ValueTask = "System.Threading.Tasks.ValueTask";
    public const string GenericValueTask = "System.Threading.Tasks.ValueTask`1";
}
