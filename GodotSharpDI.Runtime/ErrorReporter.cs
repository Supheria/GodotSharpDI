using System;
using System.Text;
using Godot;

namespace GodotSharpDI.Runtime;

/// <summary>
/// Centralized error message formatting for GodotSharpDI runtime.
/// All error messages follow a consistent structured format.
/// The actual output is delegated to a configurable callback (defaults to System.Diagnostics.Debug.WriteLine).
/// </summary>
public static class ErrorReporter
{
    /// <summary>
    /// Warning-level output callback. Defaults to <see cref="Godot.GD.PushWarning(object?[])"/>.
    /// Users can override this to redirect warnings.
    /// </summary>
    internal static Action<string> Output { get; set; } = GD.PushWarning;

    /// <summary>
    /// Error-level output callback. Defaults to <see cref="Godot.GD.PrintErr(object?[])"/>.
    /// Users can override this to redirect errors.
    /// </summary>
    internal static Action<string> ErrorOutput { get; set; } = GD.PrintErr;

    // ─── Injection errors ────────────────────────────────────────────

    public static void ReportInjectionAssignFailed(
        string typeName,
        string memberName,
        string memberType,
        Exception ex
    )
    {
        var sb = new StringBuilder()
            .AppendLine("[GodotSharpDI] Failed to assign injected value.")
            .AppendLine($"  Type: {typeName}")
            .AppendLine($"  Member: {memberName}")
            .AppendLine($"  Member Type: {memberType}")
            .AppendLine($"  Exception: {ex}");
        ErrorOutput(sb.ToString());
    }

    public static void ReportInjectionReadyCallbackFailed(
        string typeName,
        string memberName,
        string memberType,
        Exception ex
    )
    {
        var sb = new StringBuilder()
            .AppendLine("[GodotSharpDI] OnXxxInjectionReady callback threw an exception.")
            .AppendLine($"  Type: {typeName}")
            .AppendLine($"  Member: {memberName}")
            .AppendLine($"  Member Type: {memberType}")
            .AppendLine($"  Exception: {ex}");
        ErrorOutput(sb.ToString());
    }

    public static void ReportInjectionFailedCallbackFailed(
        string typeName,
        string memberName,
        string memberType,
        Exception ex
    )
    {
        var sb = new StringBuilder()
            .AppendLine("[GodotSharpDI] OnXxxInjectionFailed callback threw an exception.")
            .AppendLine($"  Type: {typeName}")
            .AppendLine($"  Member: {memberName}")
            .AppendLine($"  Member Type: {memberType}")
            .AppendLine($"  Exception: {ex}");
        ErrorOutput(sb.ToString());
    }

    // ─── Provider errors ─────────────────────────────────────────────

    public static void ReportProviderThrew(string implType, Exception ex)
    {
        ErrorOutput($"[GodotSharpDI] Provider for {implType} threw: {ex}");
    }

    public static void ReportAsyncProviderThrew(string implType, Exception ex)
    {
        ErrorOutput($"[GodotSharpDI] Async provider for {implType} threw: {ex}");
    }

    // ─── Convenience methods ──────────────────────────────────────────

    /// <summary>
    /// Report an error-level message via <see cref="ErrorOutput"/>.
    /// </summary>
    public static void ReportError(string message)
    {
        ErrorOutput(message);
    }

    /// <summary>
    /// Report a warning-level message via <see cref="Output"/>.
    /// </summary>
    public static void ReportWarning(string message)
    {
        Output(message);
    }

    // ─── Scope callback errors ───────────────────────────────────────

    public static void ReportWaiterCallbackException(
        string implType,
        string requestor,
        Exception ex
    )
    {
        var sb = new StringBuilder()
            .AppendLine("[GodotSharpDI] Exception in dependency injection callback")
            .AppendLine($"  Impl Type: {implType}")
            .AppendLine($"  Requestor: {requestor}")
            .AppendLine($"  Exception: {ex}");
        ErrorOutput(sb.ToString());
    }

    public static void ReportResolveDependencyCallbackException(
        string exposedType,
        string requestor,
        string scopeChain,
        string depChain,
        Exception ex
    )
    {
        var sb = new StringBuilder()
            .AppendLine("[GodotSharpDI] Exception in dependency injection callback")
            .AppendLine($"  Exposed Type: {exposedType}")
            .AppendLine($"  Requestor: {requestor}")
            .AppendLine($"  Scope Chain: {scopeChain}")
            .AppendLine($"  Dependency Chain: {depChain}")
            .AppendLine($"  Exception: {ex}");
        ErrorOutput(sb.ToString());
    }

    // ─── Scope errors ────────────────────────────────────────────────

    public static void ReportParentScopeNotFound(string typeName)
    {
        ErrorOutput($"[GodotSharpDI] {typeName}: Cannot find parent Scope in scene tree.");
    }

    public static void ReportServiceNotFound(
        string scopeName,
        string exposedType,
        string requestor,
        string scopeChain,
        string depChain
    )
    {
        var sb = new StringBuilder()
            .AppendLine($"[GodotSharpDI] Cannot find service {exposedType}")
            .AppendLine("  Reason: No Scope in scene tree contains this service")
            .AppendLine($"  Scope: {scopeName}")
            .AppendLine($"  Impl Type: N/A")
            .AppendLine($"  Requestor: {requestor}")
            .AppendLine($"  Scope Chain: {scopeChain}")
            .AppendLine($"  Dependency Chain: {depChain}");
        ErrorOutput(sb.ToString());
    }

    public static void ReportServiceError(
        string title,
        string reason,
        string scopeName,
        string implType,
        string requestor,
        string scopeChain,
        string depChain
    )
    {
        var sb = new StringBuilder()
            .AppendLine($"[GodotSharpDI] {title}")
            .AppendLine($"  Reason: {reason}")
            .AppendLine($"  Scope: {scopeName}")
            .AppendLine($"  Impl Type: {implType}")
            .AppendLine($"  Requestor: {requestor}")
            .AppendLine($"  Scope Chain: {scopeChain}")
            .AppendLine($"  Dependency Chain: {depChain}");
        ErrorOutput(sb.ToString());
    }
}
