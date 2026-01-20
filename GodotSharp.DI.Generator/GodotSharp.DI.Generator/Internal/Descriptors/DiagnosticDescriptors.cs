using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Descriptors;

internal static class DiagnosticDescriptors
{
    private static DiagnosticDescriptor Class(
        string idNumber,
        string content,
        DiagnosticSeverity severity = DiagnosticSeverity.Error
    )
    {
        var id = "GDI_C" + idNumber;
        return new DiagnosticDescriptor(
            id,
            id,
            content,
            "GDI.Class",
            severity,
            isEnabledByDefault: true
        );
    }

    private static DiagnosticDescriptor Member(
        string idNumber,
        string content,
        DiagnosticSeverity severity = DiagnosticSeverity.Error
    )
    {
        var id = "GDI_M" + idNumber;
        return new DiagnosticDescriptor(
            id,
            id,
            content,
            "GDI.Member",
            severity,
            isEnabledByDefault: true
        );
    }

    private static DiagnosticDescriptor Constructor(
        string idNumber,
        string content,
        DiagnosticSeverity severity = DiagnosticSeverity.Error
    )
    {
        var id = "GDI_S" + idNumber;
        return new DiagnosticDescriptor(
            id,
            id,
            content,
            "GDI.Constructor",
            severity,
            isEnabledByDefault: true
        );
    }

    private static DiagnosticDescriptor DependencyGraph(
        string idNumber,
        string content,
        DiagnosticSeverity severity = DiagnosticSeverity.Error
    )
    {
        var id = "GDI_D" + idNumber;
        return new DiagnosticDescriptor(
            id,
            id,
            content,
            "GDI.DependencyGraph",
            severity,
            isEnabledByDefault: true
        );
    }

    private static DiagnosticDescriptor InternalError(
        string idNumber,
        string content,
        DiagnosticSeverity severity = DiagnosticSeverity.Error
    )
    {
        var id = "GDI_E" + idNumber;
        return new DiagnosticDescriptor(
            id,
            id,
            content,
            "GDI.Generator",
            severity,
            isEnabledByDefault: true
        );
    }

    private static DiagnosticDescriptor UserBehavior(
        string idNumber,
        string content,
        DiagnosticSeverity severity = DiagnosticSeverity.Error
    )
    {
        var id = "GDI_U" + idNumber;
        return new DiagnosticDescriptor(
            id,
            id,
            content,
            "GDI.User",
            severity,
            isEnabledByDefault: true
        );
    }

    // ============================================================
    // C — Class-level
    // ============================================================

    public static readonly DiagnosticDescriptor ServiceLifetimeConflict = Class(
        "001",
        Resources.ServiceLifetimeConflict
    );

    public static readonly DiagnosticDescriptor HostInvalidAttribute = Class(
        "010",
        Resources.HostInvalidAttribute
    );

    public static readonly DiagnosticDescriptor UserInvalidAttribute = Class(
        "011",
        Resources.UserInvalidAttribute
    );

    public static readonly DiagnosticDescriptor ScopeInvalidAttribute = Class(
        "012",
        Resources.ScopeInvalidAttribute
    );

    public static readonly DiagnosticDescriptor HostMustBeNode = Class(
        "020",
        Resources.HostMustBeNode
    );

    public static readonly DiagnosticDescriptor ScopeMustBeNode = Class(
        "021",
        Resources.ScopeMustBeNode
    );

    public static readonly DiagnosticDescriptor ServiceReadyNeedUser = Class(
        "030",
        Resources.ServiceReadyNeedUser
    );

    public static readonly DiagnosticDescriptor InvalidModuleAttribute = Class(
        "040",
        Resources.InvalidModuleAttribute
    );

    public static readonly DiagnosticDescriptor ScopeModulesConflict = Class(
        "041",
        Resources.ScopeModulesConflict
    );

    public static readonly DiagnosticDescriptor ScopeMissingModules = Class(
        "042",
        Resources.ScopeMissingModules
    );

    // ============================================================
    // M — Member-level
    // ============================================================

    public static readonly DiagnosticDescriptor MemberHasSingletonButNotInHost = Member(
        "010",
        Resources.MemberHasSingletonButNotInHost
    );

    public static readonly DiagnosticDescriptor MemberHasInjectButNotInUser = Member(
        "011",
        Resources.MemberHasInjectButNotInUser
    );

    public static readonly DiagnosticDescriptor MemberConflictWithSingletonAndInject = Member(
        "012",
        Resources.MemberConflictWithSingletonAndInject
    );

    public static readonly DiagnosticDescriptor InjectMemberNotAssignable = Member(
        "020",
        Resources.InjectMemberNotAssignable
    );

    public static readonly DiagnosticDescriptor SingletonPropertyNotAccessible = Member(
        "030",
        Resources.SingletonPropertyNotAccessible
    );

    public static readonly DiagnosticDescriptor HostCannotBeMember = Member(
        "040",
        Resources.HostCannotBeMember
    );

    public static readonly DiagnosticDescriptor InjectMemberInvalidType = Member(
        "050",
        Resources.InjectMemberInvalidType
    );

    // ============================================================
    // S — Constructor-level
    // ============================================================

    public static readonly DiagnosticDescriptor ServiceCannotBeNode = Constructor(
        "010",
        Resources.ServiceCannotBeNode
    );

    public static readonly DiagnosticDescriptor NoPublicConstructor = Constructor(
        "020",
        Resources.NoPublicConstructor
    );

    public static readonly DiagnosticDescriptor AmbiguousConstructor = Constructor(
        "021",
        Resources.AmbiguousConstructor
    );

    public static readonly DiagnosticDescriptor InvalidInjectConstructorAttribute = Constructor(
        "022",
        Resources.InvalidInjectConstructorAttribute
    );

    // ============================================================
    // D — Dependency Graph
    // ============================================================

    public static readonly DiagnosticDescriptor ScopeModulesInstantiateEmpty = DependencyGraph(
        "001",
        Resources.ScopeModulesInstantiateEmpty
    );

    public static readonly DiagnosticDescriptor ScopeModulesExpectEmpty = DependencyGraph(
        "002",
        Resources.ScopeModulesExpectEmpty,
        DiagnosticSeverity.Info
    );

    public static readonly DiagnosticDescriptor ScopeInstantiateMustBeService = DependencyGraph(
        "003",
        Resources.ScopeInstantiateMustBeService
    );

    public static readonly DiagnosticDescriptor ScopeExpectMustBeHost = DependencyGraph(
        "004",
        Resources.ScopeExpectMustBeHost
    );

    public static readonly DiagnosticDescriptor HostServiceNotFound = DependencyGraph(
        "010",
        Resources.HostServiceNotFound
    );

    public static readonly DiagnosticDescriptor HostServiceMustBeService = DependencyGraph(
        "011",
        Resources.HostServiceMustBeService
    );

    public static readonly DiagnosticDescriptor CircularDependencyDetected = DependencyGraph(
        "020",
        Resources.CircularDependencyDetected
    );

    public static readonly DiagnosticDescriptor ServiceConstructorParameterInvalid =
        DependencyGraph("030", Resources.ServiceConstructorParameterInvalid);

    public static readonly DiagnosticDescriptor SingletonCannotDependOnTransient = DependencyGraph(
        "040",
        Resources.SingletonCannotDependOnTransient
    );

    // ============================================================
    // E — InternalError
    // ============================================================

    public static readonly DiagnosticDescriptor RequestCancellation = InternalError(
        "900",
        Resources.RequestCancellation
    );

    public static readonly DiagnosticDescriptor GeneratorInternalError = InternalError(
        "910",
        Resources.GeneratorInternalError
    );

    public static readonly DiagnosticDescriptor UnknownTypeRole = InternalError(
        "920",
        Resources.UnknownTypeRole
    );

    public static readonly DiagnosticDescriptor ScopeLosesAttributeUnexpectedly = InternalError(
        "930",
        Resources.ScopeLosesAttributeUnexpectedly
    );

    // ============================================================
    // U — User Behavior
    // ============================================================

    public static readonly DiagnosticDescriptor ManualAttachToScope = UserBehavior(
        "001",
        Resources.ManualAttachToScope
    );

    public static readonly DiagnosticDescriptor ManualResolveUserDependencies = UserBehavior(
        "002",
        Resources.ManualResolveUserDependencies
    );
}
