namespace GodotSharpDI.SourceGenerator.Shared;

/// <summary>
/// Metadata names for <see cref="Microsoft.CodeAnalysis.Compilation.GetTypeByMetadataName"/>.
/// These are NOT <c>global::</c> qualified — for emitted code see <see cref="GlobalNames"/>.
/// </summary>
public static class TypeNamesFull
{
    // Existing attributes (maintain backward compatibility)
    public const string InjectAttribute = "GodotSharpDI.Abstractions.InjectAttribute";
    public const string ProvideAttribute = "GodotSharpDI.Abstractions.ProvideAttribute";
    public const string ModulesAttribute = "GodotSharpDI.Abstractions.ModulesAttribute";
    public const string HostAttribute = "GodotSharpDI.Abstractions.HostAttribute";
    public const string UserAttribute = "GodotSharpDI.Abstractions.UserAttribute";

    public const string IScope = "GodotSharpDI.Abstractions.IScope";
    public const string IDependenciesResolved = "GodotSharpDI.Abstractions.IDependenciesResolved";

    public const string GodotNode = "Godot.Node";

    // System
    public const string IDisposable = "System.IDisposable";
    public const string GenericTask = "System.Threading.Tasks.Task`1";
    public const string GenericValueTask = "System.Threading.Tasks.ValueTask`1";
}
