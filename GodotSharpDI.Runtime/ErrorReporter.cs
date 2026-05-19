using System;
using System.Text;

namespace GodotSharpDI.Runtime;

/// <summary>
/// Centralized error message formatting for GodotSharpDI runtime.
/// All error messages follow a consistent structured format.
/// The actual output is delegated to a configurable callback passed by the caller.
/// </summary>
public static class ErrorReporter
{
    // ─── Injection errors ────────────────────────────────────────────

    public static void ReportInjectionAssignFailed(
        string typeName,
        string memberName,
        string memberType,
        Exception ex,
        Action<string> errorOutput
    )
    {
        var sb = new StringBuilder()
            .AppendLine("[GodotSharpDI] Failed to assign injected value.")
            .AppendLine($"  Type: {typeName}")
            .AppendLine($"  Member: {memberName}")
            .AppendLine($"  Member Type: {memberType}")
            .AppendLine($"  Exception: {ex}");
        errorOutput(sb.ToString());
    }

    public static void ReportInjectionReadyCallbackFailed(
        string typeName,
        string memberName,
        string memberType,
        Exception ex,
        Action<string> errorOutput
    )
    {
        var sb = new StringBuilder()
            .AppendLine("[GodotSharpDI] OnXxxInjectionReady callback threw an exception.")
            .AppendLine($"  Type: {typeName}")
            .AppendLine($"  Member: {memberName}")
            .AppendLine($"  Member Type: {memberType}")
            .AppendLine($"  Exception: {ex}");
        errorOutput(sb.ToString());
    }

    public static void ReportInjectionFailedCallbackFailed(
        string typeName,
        string memberName,
        string memberType,
        Exception ex,
        Action<string> errorOutput
    )
    {
        var sb = new StringBuilder()
            .AppendLine("[GodotSharpDI] OnXxxInjectionFailed callback threw an exception.")
            .AppendLine($"  Type: {typeName}")
            .AppendLine($"  Member: {memberName}")
            .AppendLine($"  Member Type: {memberType}")
            .AppendLine($"  Exception: {ex}");
        errorOutput(sb.ToString());
    }

    // ─── Provider errors ─────────────────────────────────────────────

    public static void ReportProviderThrew(string implType, Exception ex, Action<string> errorOutput)
    {
        errorOutput($"[GodotSharpDI] Provider for {implType} threw: {ex}");
    }

    public static void ReportAsyncProviderThrew(string implType, Exception ex, Action<string> errorOutput)
    {
        errorOutput($"[GodotSharpDI] Async provider for {implType} threw: {ex}");
    }

    // ─── Convenience methods ──────────────────────────────────────────

    /// <summary>
    /// Report an error-level message via the provided callback.
    /// </summary>
    public static void ReportError(string message, Action<string> errorOutput)
    {
        errorOutput(message);
    }

    // ─── Scope callback errors ───────────────────────────────────────

    public static void ReportWaiterCallbackException(
        string implType,
        string requestor,
        Exception ex,
        Action<string> errorOutput
    )
    {
        var sb = new StringBuilder()
            .AppendLine("[GodotSharpDI] Exception in dependency injection callback")
            .AppendLine($"  Impl Type: {implType}")
            .AppendLine($"  Requestor: {requestor}")
            .AppendLine($"  Exception: {ex}");
        errorOutput(sb.ToString());
    }

    public static void ReportResolveDependencyCallbackException(
        string exposedType,
        string requestor,
        string scopeChain,
        string depChain,
        Exception ex,
        Action<string> errorOutput
    )
    {
        var sb = new StringBuilder()
            .AppendLine("[GodotSharpDI] Exception in dependency injection callback")
            .AppendLine($"  Exposed Type: {exposedType}")
            .AppendLine($"  Requestor: {requestor}")
            .AppendLine($"  Scope Chain: {scopeChain}")
            .AppendLine($"  Dependency Chain: {depChain}")
            .AppendLine($"  Exception: {ex}");
        errorOutput(sb.ToString());
    }

    // ─── Scope errors ────────────────────────────────────────────────

    public static void ReportParentScopeNotFound(string typeName, Action<string> errorOutput)
    {
        errorOutput($"[GodotSharpDI] {typeName}: Cannot find parent Scope in scene tree.");
    }

    public static void ReportServiceNotFound(
        string scopeName,
        string exposedType,
        string requestor,
        string scopeChain,
        string depChain,
        Action<string> errorOutput
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
        errorOutput(sb.ToString());
    }

    public static void ReportServiceError(
        string title,
        string reason,
        string scopeName,
        string implType,
        string requestor,
        string scopeChain,
        string depChain,
        Action<string> errorOutput
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
        errorOutput(sb.ToString());
    }
}
