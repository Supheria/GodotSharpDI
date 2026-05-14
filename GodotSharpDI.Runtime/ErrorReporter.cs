using System;
using System.Text;

namespace GodotSharpDI.Runtime;

/// <summary>
/// Centralized error message formatting for GodotSharpDI runtime.
/// All error messages follow a consistent structured format.
/// The actual output is delegated to a configurable callback (defaults to System.Diagnostics.Debug.WriteLine).
/// </summary>
public static class ErrorReporter
{
    /// <summary>
    /// Error output callback. Defaults to <see cref="System.Diagnostics.Debug.WriteLine(string)"/>.
    /// Users can override this to redirect errors (e.g., to Godot's GD.PrintErr).
    /// </summary>
    public static Action<string> Output { get; set; } = msg => System.Diagnostics.Debug.WriteLine(msg);

    // ─── Injection errors ────────────────────────────────────────────

    public static void ReportInjectionAssignFailed(
        string typeName, string memberName, string memberType, string exMsg)
    {
        var sb = new StringBuilder()
            .AppendLine("[GodotSharpDI] Failed to assign injected value.")
            .AppendLine($"  Type: {typeName}")
            .AppendLine($"  Member: {memberName}")
            .AppendLine($"  Member Type: {memberType}")
            .AppendLine($"  Exception: {exMsg}");
        Output(sb.ToString());
    }

    public static void ReportInjectionReadyCallbackFailed(
        string typeName, string memberName, string memberType, string exMsg)
    {
        var sb = new StringBuilder()
            .AppendLine("[GodotSharpDI] OnXxxInjectionReady callback threw an exception.")
            .AppendLine($"  Type: {typeName}")
            .AppendLine($"  Member: {memberName}")
            .AppendLine($"  Member Type: {memberType}")
            .AppendLine($"  Exception: {exMsg}");
        Output(sb.ToString());
    }

    public static void ReportInjectionFailedCallbackFailed(
        string typeName, string memberName, string memberType, string exMsg)
    {
        var sb = new StringBuilder()
            .AppendLine("[GodotSharpDI] OnXxxInjectionFailed callback threw an exception.")
            .AppendLine($"  Type: {typeName}")
            .AppendLine($"  Member: {memberName}")
            .AppendLine($"  Member Type: {memberType}")
            .AppendLine($"  Exception: {exMsg}");
        Output(sb.ToString());
    }

    // ─── Provider errors ─────────────────────────────────────────────

    public static void ReportProviderThrew(string implType, string exMsg)
    {
        Output($"[GodotSharpDI] Provider for {implType} threw: {exMsg}");
    }

    public static void ReportAsyncProviderThrew(string implType, string exMsg)
    {
        Output($"[GodotSharpDI] Async provider for {implType} threw: {exMsg}");
    }

    // ─── Scope errors ────────────────────────────────────────────────

    public static string BuildServiceNotFoundMessage(
        string scopeName, string exposedType, string requestor, string scopeChain, string depChain)
    {
        return new StringBuilder()
            .AppendLine($"[GodotSharpDI] Cannot find service {exposedType}")
            .AppendLine("  Reason: No Scope in scene tree contains this service")
            .AppendLine($"  Scope: {scopeName}")
            .AppendLine($"  Impl Type: N/A")
            .AppendLine($"  Requestor: {requestor}")
            .AppendLine($"  Scope Chain: {scopeChain}")
            .AppendLine($"  Dependency Chain: {depChain}")
            .ToString();
    }

    public static string BuildServiceErrorMessage(
        string title, string reason, string scopeName,
        string implType, string requestor, string scopeChain, string depChain)
    {
        return new StringBuilder()
            .AppendLine($"[GodotSharpDI] {title}")
            .AppendLine($"  Reason: {reason}")
            .AppendLine($"  Scope: {scopeName}")
            .AppendLine($"  Impl Type: {implType}")
            .AppendLine($"  Requestor: {requestor}")
            .AppendLine($"  Scope Chain: {scopeChain}")
            .AppendLine($"  Dependency Chain: {depChain}")
            .ToString();
    }
}
