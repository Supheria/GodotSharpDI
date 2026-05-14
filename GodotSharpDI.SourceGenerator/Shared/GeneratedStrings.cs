namespace GodotSharpDI.SourceGenerator.Shared;

/// <summary>
/// Runtime string constants used in generated code (centralized management, easy for internationalization)
/// </summary>
internal static class GeneratedStrings
{
    // ─── Scope error messages ────────────────────────────────────────
    public const string ErrServiceNotFound =
        "[GodotSharpDI] Service '{0}' not found. No Scope in parent tree contains this service.";
    public const string ErrServiceAlreadyProvided =
        "[GodotSharpDI] Service '{0}' has already been provided (duplicate provision).";
    public const string ErrParentScopeNotFound =
        "[GodotSharpDI] {0}: Cannot find parent Scope in scene tree.";
    public const string ErrProvisionFailed =
        "[GodotSharpDI] {0}: Service provision failed: {1}";

    // ─── Timeout / monitoring messages ──────────────────────────────
    public const string WarnInjectionTimeout =
        "[GodotSharpDI] Dependency injection timeout.";
    public const string WarnUnresolvedDependencies =
        "[GodotSharpDI] {0} has unresolved dependencies.";

    // ─── Assignment error ────────────────────────────────────────────
    public const string ErrInjectionAssignFailed =
        "[GodotSharpDI] Failed to assign injected value.";

    // ─── Structured log field labels ─────────────────────────────────
    public const string LabelCurrentScope   = "  Scope: ";
    public const string LabelServiceType    = "  Service Type: ";
    public const string LabelImplType       = "  Impl Type: ";
    public const string LabelRequestor      = "  Requestor: ";
    public const string LabelElapsed        = "  Elapsed: ";
    public const string LabelScopeChain     = "  Scope Chain: ";
    public const string LabelDependency     = "  Dependency Chain: ";
    public const string LabelReason         = "  Reason: ";
    public const string LabelPendingServices = "  Pending Services: ";
    public const string LabelWaiters        = "  Waiting queue count: ";

    // ─── Phase separator comments (ASCII only, no Unicode box chars) ─
    public const string Phase1Comment =
        "// === Phase 1: Resolve injected dependencies (non-blocking) ===";
    public const string Phase23Comment =
        "// === Phase 2 & 3: Provide services (independent of injection) ===";
    public const string MemberSeparatorFmt  = "// --- Member: {0} ---";
}
