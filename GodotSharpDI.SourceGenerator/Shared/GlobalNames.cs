namespace GodotSharpDI.SourceGenerator.Shared;

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
    public const string ResolutionResult = "global::GodotSharpDI.Abstractions.ResolutionResult";

    // ─── Generated code local variable name conventions ─────────────────────
    // 通过常量集中管理，确保所有生成器使用相同的局部变量名，避免字符串不一致
    public const string LocalScope    = "scope";
    public const string LocalInstance = "instance";
    public const string LocalResult   = "result";
    public const string LocalTask     = "task";
}
