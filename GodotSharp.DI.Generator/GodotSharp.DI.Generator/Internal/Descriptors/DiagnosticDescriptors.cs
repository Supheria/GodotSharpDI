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

    public static readonly DiagnosticDescriptor ScopeInvalidAttribute = Class(
        "011",
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

    public static readonly DiagnosticDescriptor ScopeMissingModules = Class(
        "040",
        Resources.ScopeMissingModules
    );

    public static readonly DiagnosticDescriptor DiClassMustBePartial = Class(
        "050",
        Resources.DiClassMustBePartial
    );

    public static readonly DiagnosticDescriptor ServiceTypeIsInvalid = Class(
        "060",
        Resources.ServiceTypeIsInvalid
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

    public static readonly DiagnosticDescriptor InjectMemberInvalidType = Member(
        "040",
        Resources.InjectMemberInvalidType
    );

    public static readonly DiagnosticDescriptor InjectMemberIsHostType = Member(
        "041",
        Resources.InjectMemberIsHostType
    );

    public static readonly DiagnosticDescriptor InjectMemberIsUserType = Member(
        "042",
        Resources.InjectMemberIsUserType
    );

    public static readonly DiagnosticDescriptor InjectMemberIsScopeType = Member(
        "043",
        Resources.InjectMemberIsScopeType
    );

    public static readonly DiagnosticDescriptor InjectMemberIsStatic = Member(
        "044",
        Resources.InjectMemberIsStatic
    );

    public static readonly DiagnosticDescriptor SingletonMemberIsStatic = Member(
        "045",
        Resources.SingletonMemberIsStatic
    );

    public static readonly DiagnosticDescriptor HostSingletonMemberIsServiceType = Member(
        "050",
        Resources.HostSingletonMemberIsServiceType
    );

    public static readonly DiagnosticDescriptor ExposedTypeShouldBeInterface = Member(
        "060",
        Resources.ExposedTypeShouldBeInterface,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor UserMemberCannotBeNode = Member(
        "070",
        Resources.UserMemberCannotBeNode
    );

    public static readonly DiagnosticDescriptor NonNodeUserCannotContainUserMember = Member(
        "071",
        Resources.NonNodeUserCannotContainUserMember
    );

    public static readonly DiagnosticDescriptor UserMemberMustBeInitialized = Member(
        "072",
        Resources.UserMemberMustBeInitialized
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

    public static readonly DiagnosticDescriptor InjectConstructorAttributeIsInvalid = Constructor(
        "022",
        Resources.InjectConstructorAttributeIsInvalid
    );

    public static readonly DiagnosticDescriptor InjectConstructorParameterTypeInvalid = Constructor(
        "030",
        Resources.InjectConstructorParameterTypeInvalid
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

    public static readonly DiagnosticDescriptor CircularDependencyDetected = DependencyGraph(
        "010",
        Resources.CircularDependencyDetected
    );

    public static readonly DiagnosticDescriptor ServiceConstructorParameterInvalid =
        DependencyGraph("020", Resources.ServiceConstructorParameterInvalid);

    public static readonly DiagnosticDescriptor SingletonCannotDependOnTransient = DependencyGraph(
        "030",
        Resources.SingletonCannotDependOnTransient
    );

    public static readonly DiagnosticDescriptor ServiceTypeConflict = DependencyGraph(
        "040",
        Resources.ServiceTypeConflict
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

    public static readonly DiagnosticDescriptor ManualCallGeneratedMethod = UserBehavior(
        "001",
        Resources.ManualCallGeneratedMethod
    );
}
