namespace GodotSharpDI.Shared;

public static class GlobalNames
{
    // System
    public const string Action = "global::System.Action";
    public const string Exception = "global::System.Exception";
    public const string IDisposable = "global::System.IDisposable";
    public const string Object = "global::System.Object";
    public const string Type = "global::System.Type";
    public const string DateTime = "global::System.DateTime";
    public const string TimeSpan = "global::System.TimeSpan";
    public const string String = "global::System.String";
    public const string Long = "global::System.Int64";
    public const string Bool = "global::System.Boolean";

    // System.Threading.Tasks
    public const string Task = "global::System.Threading.Tasks.Task";

    // System.Collections.Generic
    public const string Dictionary = "global::System.Collections.Generic.Dictionary";
    public const string HashSet = "global::System.Collections.Generic.HashSet";
    public const string List = "global::System.Collections.Generic.List";

    // System.Text
    public const string StringBuilder = "global::System.Text.StringBuilder";

    // System.Diagnostics.CodeAnalysis
    public const string MemberNotNullWhen =
        "global::System.Diagnostics.CodeAnalysis.MemberNotNullWhen";

    // Godot
    public const string GodotGD = "global::Godot.GD";
    public const string GodotTimer = "global::Godot.Timer";
    public const string GodotCallable = "global::Godot.Callable";

    // GodotSharp.DI.Abstractions
    public const string IScope = "global::GodotSharpDI.Abstractions.IScope";
    public const string IDependenciesResolved =
        "global::GodotSharpDI.Abstractions.IDependenciesResolved";

    // GodotSharp.DI.Runtime
    public const string InjectionExecutor = "global::GodotSharpDI.Runtime.InjectionExecutor";
    public const string SyncProviderRunner = "global::GodotSharpDI.Runtime.SyncProviderRunner";
    public const string AsyncProviderRunner = "global::GodotSharpDI.Runtime.AsyncProviderRunner";
    public const string WaitForCoordinator = "global::GodotSharpDI.Runtime.WaitForCoordinator";
    public const string ErrorReporter = "global::GodotSharpDI.Runtime.ErrorReporter";
    public const string DeadlockDetector = "global::GodotSharpDI.Runtime.DeadlockDetector";
    public const string ServiceState = "global::GodotSharpDI.Runtime.ServiceState";
    public const string ServiceCacheEntry = "global::GodotSharpDI.Runtime.ServiceCacheEntry";
    public const string DependencyWaitInfo = "global::GodotSharpDI.Runtime.DependencyWaitInfo";

    // ─── Generated code local variable name conventions ─────────────────────
    // Centralized management through constants, ensuring all generators use the same local variable names, avoiding string inconsistencies
    public const string LocalScope    = "scope";
    public const string LocalInstance = "instance";
    public const string LocalResult   = "result";
    public const string LocalTask     = "task";
}
