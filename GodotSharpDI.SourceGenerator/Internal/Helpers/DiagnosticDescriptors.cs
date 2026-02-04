using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Helpers;

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

    public static readonly DiagnosticDescriptor HostInvalidAttribute = Class(
        "010",
        Resources.C_HostInvalidAttribute
    );

    public static readonly DiagnosticDescriptor UserInvalidAttribute = Class(
        "011",
        Resources.C_UserInvalidAttribute
    );

    public static readonly DiagnosticDescriptor ScopeInvalidAttribute = Class(
        "012",
        Resources.C_ScopeInvalidAttribute
    );

    public static readonly DiagnosticDescriptor OnlyScopeCanUseModules = Class(
        "013",
        Resources.C_OnlyScopeCanUseModules
    );

    public static readonly DiagnosticDescriptor HostMustBeNode = Class(
        "020",
        Resources.C_HostMustBeNode
    );

    public static readonly DiagnosticDescriptor UserMustBeNode = Class(
        "021",
        Resources.C_UserMustBeNode
    );

    public static readonly DiagnosticDescriptor ScopeMustBeNode = Class(
        "022",
        Resources.C_ScopeMustBeNode
    );

    public static readonly DiagnosticDescriptor ServiceReadyNeedUser = Class(
        "030",
        Resources.C_ServiceReadyNeedUser
    );

    public static readonly DiagnosticDescriptor ScopeMissingModules = Class(
        "040",
        Resources.C_ScopeMissingModules
    );

    public static readonly DiagnosticDescriptor DiClassMustBePartial = Class(
        "050",
        Resources.C_DiClassMustBePartial
    );

    public static readonly DiagnosticDescriptor ServiceTypeIsInvalid = Class(
        "060",
        Resources.C_ServiceTypeIsInvalid
    );

    public static readonly DiagnosticDescriptor ServiceExposedTypeShouldBeInterface = Class(
        "070",
        Resources.C_ServiceExposedTypeShouldBeInterface,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor ServiceExposedTypeNotImplemented = Class(
        "071",
        Resources.C_ServiceExposedTypeNotImplemented
    );

    public static readonly DiagnosticDescriptor MissingNotificationMethod = Class(
        "080",
        Resources.C_MissingNotificationMethod
    );

    public static readonly DiagnosticDescriptor InvalidNotificationMethodSignature = Class(
        "081",
        Resources.C_InvalidNotificationMethodSignature
    );

    // ============================================================
    // M — Member-level
    // ============================================================

    public static readonly DiagnosticDescriptor MemberHasSingletonButNotInHost = Member(
        "010",
        Resources.M_MemberHasSingletonButNotInHost
    );

    public static readonly DiagnosticDescriptor MemberHasInjectButNotInUser = Member(
        "011",
        Resources.M_MemberHasInjectButNotInUser
    );

    public static readonly DiagnosticDescriptor MemberConflictWithSingletonAndInject = Member(
        "012",
        Resources.M_MemberConflictWithSingletonAndInject
    );

    public static readonly DiagnosticDescriptor InjectMemberNotAssignable = Member(
        "020",
        Resources.M_InjectMemberNotAssignable
    );

    public static readonly DiagnosticDescriptor SingletonPropertyNotAccessible = Member(
        "030",
        Resources.M_SingletonPropertyNotAccessible
    );

    public static readonly DiagnosticDescriptor InjectMemberIsStatic = Member(
        "040",
        Resources.M_InjectMemberIsStatic
    );

    public static readonly DiagnosticDescriptor InjectMemberTypeIsInvalid = Member(
        "041",
        Resources.M_InjectMemberTypeIsInvalid
    );

    public static readonly DiagnosticDescriptor InjectMemberIsHostType = Member(
        "042",
        Resources.M_InjectMemberIsHostType,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor InjectMemberIsUserType = Member(
        "043",
        Resources.M_InjectMemberIsUserType
    );

    public static readonly DiagnosticDescriptor InjectMemberIsScopeType = Member(
        "044",
        Resources.M_InjectMemberIsScopeType
    );

    public static readonly DiagnosticDescriptor InjectMemberIsRegularNode = Member(
        "045",
        Resources.M_InjectMemberIsStatic
    );

    public static readonly DiagnosticDescriptor InjectMemberTypeShouldBeInterface = Member(
        "046",
        Resources.M_InjectMemberTypeShouldBeInterface
    );

    public static readonly DiagnosticDescriptor SingletonMemberIsStatic = Member(
        "050",
        Resources.M_SingletonMemberIsStatic
    );

    public static readonly DiagnosticDescriptor SingletonMemberTypeIsInvalid = Member(
        "051",
        Resources.M_SingletonMemberTypeIsInvalid
    );

    public static readonly DiagnosticDescriptor SingletonMemberIsServiceType = Member(
        "052",
        Resources.M_SingletonMemberIsServiceType
    );

    public static readonly DiagnosticDescriptor SingletonMemberIsHostType = Member(
        "053",
        Resources.M_SingletonMemberIsHostType,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor SingletonMemberIsUserType = Member(
        "054",
        Resources.M_SingletonMemberIsUserType
    );

    public static readonly DiagnosticDescriptor SingletonMemberIsScopeType = Member(
        "055",
        Resources.M_SingletonMemberIsScopeType
    );

    public static readonly DiagnosticDescriptor SingletonMemberIsRegularNode = Member(
        "056",
        Resources.M_SingletonMemberIsRegularNode
    );

    public static readonly DiagnosticDescriptor SingletonMemberExposedTypeNotImplemented = Member(
        "057",
        Resources.M_SingletonMemberExposedTypeNotImplemented
    );

    public static readonly DiagnosticDescriptor SingletonMemberExposedTypeShouldBeInterface =
        Member(
            "058",
            Resources.M_SingletonMemberExposedTypeShouldBeInterface,
            DiagnosticSeverity.Warning
        );

    public static readonly DiagnosticDescriptor HostMissingSingletonMember = Member(
        "070",
        Resources.M_HostMissingSingletonMember,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor UserMissingInjectMember = Member(
        "071",
        Resources.M_UserMissingInjectMember,
        DiagnosticSeverity.Warning
    );

    // ============================================================
    // S — Constructor-level
    // ============================================================

    public static readonly DiagnosticDescriptor NoNonStaticConstructor = Constructor(
        "010",
        Resources.S_NoNonStaticConstructor
    );

    public static readonly DiagnosticDescriptor AmbiguousConstructor = Constructor(
        "011",
        Resources.S_AmbiguousConstructor
    );

    public static readonly DiagnosticDescriptor InjectConstructorAttributeIsInvalid = Constructor(
        "012",
        Resources.S_InjectConstructorAttributeIsInvalid
    );

    public static readonly DiagnosticDescriptor InjectCtorParamTypeInvalid = Constructor(
        "020",
        Resources.S_InjectConstructorParameterTypeInvalid
    );

    public static readonly DiagnosticDescriptor InjectCtorParamIsHostType = Constructor(
        "021",
        Resources.S_InjectCtorParamIsHostType
    );

    public static readonly DiagnosticDescriptor InjectCtorParamIsUserType = Constructor(
        "022",
        Resources.S_InjectCtorParamIsUserType
    );

    public static readonly DiagnosticDescriptor InjectCtorParamIsScopeType = Constructor(
        "023",
        Resources.S_InjectCtorParamIsScopeType
    );

    public static readonly DiagnosticDescriptor InjectCtorParamIsRegularNode = Constructor(
        "024",
        Resources.S_InjectCtorParamIsRegularNode
    );

    public static readonly DiagnosticDescriptor InjectCtorParamTypeShouldBeInterface = Constructor(
        "025",
        Resources.S_InjectCtorParamTypeShouldBeInterface,
        DiagnosticSeverity.Warning
    );

    // ============================================================
    // D — Dependency Graph
    // ============================================================

    public static readonly DiagnosticDescriptor ScopeModulesEmpty = DependencyGraph(
        "001",
        Resources.D_ScopeModulesEmpty,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor ScopeModulesServiceMustBeService = DependencyGraph(
        "002",
        Resources.D_ScopeModulesServiceMustBeService
    );

    public static readonly DiagnosticDescriptor ScopeModulesHostMustBeHost = DependencyGraph(
        "003",
        Resources.D_ScopeModulesHostMustBeHost
    );

    public static readonly DiagnosticDescriptor CircularDependencyDetected = DependencyGraph(
        "010",
        Resources.D_CircularDependencyDetected
    );

    public static readonly DiagnosticDescriptor ServiceConstructorParameterInvalid =
        DependencyGraph("020", Resources.D_ServiceConstructorParameterInvalid);

    public static readonly DiagnosticDescriptor ServiceTypeConflict = DependencyGraph(
        "040",
        Resources.D_ServiceTypeConflict
    );

    public static readonly DiagnosticDescriptor InjectMemberTypeIsNotExposed = DependencyGraph(
        "050",
        Resources.D_InjectMemberTypeIsNotExposed
    );

    // ============================================================
    // E — InternalError
    // ============================================================

    public static readonly DiagnosticDescriptor RequestCancellation = InternalError(
        "900",
        Resources.E_RequestCancellation
    );

    public static readonly DiagnosticDescriptor GeneratorInternalError = InternalError(
        "910",
        Resources.E_GeneratorInternalError
    );

    public static readonly DiagnosticDescriptor UnknownTypeRole = InternalError(
        "920",
        Resources.E_UnknownTypeRole
    );

    public static readonly DiagnosticDescriptor ScopeLosesAttributeUnexpectedly = InternalError(
        "930",
        Resources.E_ScopeLosesAttributeUnexpectedly
    );

    // ============================================================
    // U — User Behavior
    // ============================================================

    public static readonly DiagnosticDescriptor ManualCallGeneratedMethod = UserBehavior(
        "001",
        Resources.U_ManualCallGeneratedMethod
    );

    public static readonly DiagnosticDescriptor ManualAccessGeneratedField = UserBehavior(
        "002",
        Resources.U_ManualAccessGeneratedField
    );

    public static readonly DiagnosticDescriptor ManualAccessGeneratedProperty = UserBehavior(
        "003",
        Resources.U_ManualAccessGeneratedProperty
    );
}
