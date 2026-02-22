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

    public static readonly DiagnosticDescriptor HostCannotBeGenericType = Class(
        "023",
        Resources.C_HostCannotBeGenericType
    );

    public static readonly DiagnosticDescriptor UserCannotBeGenericType = Class(
        "024",
        Resources.C_UserCannotBeGenericType
    );

    public static readonly DiagnosticDescriptor ScopeCannotBeGenericType = Class(
        "025",
        Resources.C_ScopeCannotBeGenericType
    );

    public static readonly DiagnosticDescriptor IDependenciesResolvedInvalid = Class(
        "030",
        Resources.C_IDependenciesResolvedInvalid
    );

    public static readonly DiagnosticDescriptor ScopeMissingModules = Class(
        "040",
        Resources.C_ScopeMissingModules
    );

    public static readonly DiagnosticDescriptor DiClassMustBePartial = Class(
        "050",
        Resources.C_DiClassMustBePartial
    );

    public static readonly DiagnosticDescriptor MissingNotificationMethod = Class(
        "060",
        Resources.C_MissingNotificationMethod
    );

    public static readonly DiagnosticDescriptor InvalidNotificationMethodSignature = Class(
        "061",
        Resources.C_InvalidNotificationMethodSignature
    );

    // ============================================================
    // M — Member-level
    // ============================================================

    public static readonly DiagnosticDescriptor ProvideMemberNotInServiceOrHost = Member(
        "010",
        Resources.M_ProvideMemberNotInServiceOrHost
    );

    public static readonly DiagnosticDescriptor InjectMemberNotInServiceHostOrUser = Member(
        "011",
        Resources.M_InjectMemberNotInServiceHostOrUser
    );

    public static readonly DiagnosticDescriptor MemberConflictWithProvideAndInject = Member(
        "012",
        Resources.M_MemberConflictWithProvideAndInject
    );

    public static readonly DiagnosticDescriptor InjectMemberNotAssignable = Member(
        "020",
        Resources.M_InjectMemberNotAssignable
    );

    public static readonly DiagnosticDescriptor ProvidePropertyNotAccessible = Member(
        "030",
        Resources.M_ProvidePropertyNotAccessible
    );

    public static readonly DiagnosticDescriptor ProvideMethodReturnVoid = Member(
        "031",
        Resources.M_ProvideMethodReturnVoid
    );

    public static readonly DiagnosticDescriptor ProvideMethodNotParameterless = Member(
        "032",
        Resources.M_ProvideMethodNotParameterless
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
        Resources.M_InjectMemberIsRegularNode
    );

    public static readonly DiagnosticDescriptor InjectMemberTypeShouldBeInterface = Member(
        "046",
        Resources.M_InjectMemberTypeShouldBeInterface,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor InjectMemberTypeCannotBeGeneric = Member(
        "047",
        Resources.M_InjectMemberTypeCannotBeGeneric
    );

    public static readonly DiagnosticDescriptor ProvideMemberIsStatic = Member(
        "050",
        Resources.M_ProvideMemberIsStatic
    );

    public static readonly DiagnosticDescriptor ProvideMemberTypeIsInvalid = Member(
        "051",
        Resources.M_ProvideMemberTypeIsInvalid
    );

    public static readonly DiagnosticDescriptor ProvideMemberIsHostType = Member(
        "052",
        Resources.M_ProvideMemberIsHostType
    );

    public static readonly DiagnosticDescriptor ProvideMemberIsUserType = Member(
        "053",
        Resources.M_ProvideMemberIsUserType
    );

    public static readonly DiagnosticDescriptor ProvideMemberIsScopeType = Member(
        "054",
        Resources.M_ProvideMemberIsScopeType
    );

    public static readonly DiagnosticDescriptor ProvideMemberIsRegularNode = Member(
        "055",
        Resources.M_ProvideMemberIsRegularNode
    );

    public static readonly DiagnosticDescriptor ProvideMemberTypeCannotBeGeneric = Member(
        "056",
        Resources.M_ProvideMemberTypeCannotBeGeneric
    );

    public static readonly DiagnosticDescriptor ProvideMemberExposedTypeNotImplemented = Member(
        "060",
        Resources.M_ProvideMemberExposedTypeNotImplemented
    );

    public static readonly DiagnosticDescriptor ProvideMemberExposedTypeShouldBeInterface = Member(
        "061",
        Resources.M_ProvideMemberExposedTypeShouldBeInterface,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor ProvideMemberExposedTypeCannotBeGeneric = Member(
        "062",
        Resources.M_ProvideMemberExposedTypeCannotBeGeneric
    );

    public static readonly DiagnosticDescriptor HostMissingProvideMember = Member(
        "070",
        Resources.M_HostMissingProvideMember,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor UserMissingInjectMember = Member(
        "071",
        Resources.M_UserMissingInjectMember,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor WaitForFieldNotFound = Member(
        "080",
        Resources.M_WaitForFieldNotFound
    );

    public static readonly DiagnosticDescriptor WaitForFieldNotInjected = Member(
        "081",
        Resources.M_WaitForFieldNotInjected
    );

    public static readonly DiagnosticDescriptor WaitForCircularDependency = Member(
        "082",
        Resources.M_WaitForCircularDependency
    );

    // ============================================================
    // D — Dependency Graph
    // ============================================================

    public static readonly DiagnosticDescriptor ScopeModulesEmpty = DependencyGraph(
        "001",
        Resources.D_ScopeModulesEmpty,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor ScopeModulesHostMustBeHost = DependencyGraph(
        "003",
        Resources.D_ScopeModulesHostMustBeHost
    );

    public static readonly DiagnosticDescriptor CircularDependencyDetected = DependencyGraph(
        "010",
        Resources.D_CircularDependencyDetected
    );

    public static readonly DiagnosticDescriptor CrossHostDeadlockDetected = DependencyGraph(
        "011",
        Resources.D_CrossHostDeadlockDetected
    );

    public static readonly DiagnosticDescriptor ScopeServiceTypeConflict = DependencyGraph(
        "040",
        Resources.D_ScopeServiceTypeConflict
    );

    public static readonly DiagnosticDescriptor DuplicateServiceRegistration = DependencyGraph(
        "041",
        Resources.D_DuplicateServiceRegistration,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor InjectMemberTypeIsNotExposed = DependencyGraph(
        "050",
        Resources.D_InjectMemberTypeIsNotExposed
    );

    // ============================================================
    // E — InternalError
    // ============================================================

    public static readonly DiagnosticDescriptor GeneratorInitializationFailed = InternalError(
        "001",
        Resources.E_GeneratorInitializationFailed
    );

    public static readonly DiagnosticDescriptor ClassAnalysisFailed = InternalError(
        "010",
        Resources.E_ClassAnalysisFailed,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor SymbolCacheUnavailable = InternalError(
        "011",
        Resources.E_SymbolCacheUnavailable,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor ClassValidationFailed = InternalError(
        "012",
        Resources.E_ClassValidationFailed,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor GraphBuildFailed = InternalError(
        "020",
        Resources.E_GraphBuildFailed
    );

    public static readonly DiagnosticDescriptor GraphBuildPhaseFailed = InternalError(
        "021",
        Resources.E_GraphBuildPhaseFailed
    );

    public static readonly DiagnosticDescriptor NodeBuildFailed = InternalError(
        "040",
        Resources.E_NodeBuildFailed,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor GraphValidationFailed = InternalError(
        "050",
        Resources.E_GraphValidationFailed,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor CodeGenerationFailed = InternalError(
        "100",
        Resources.E_CodeGenerationFailed
    );

    public static readonly DiagnosticDescriptor SourceOutputFailed = InternalError(
        "101",
        Resources.E_SourceOutputFailed
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

    public static readonly DiagnosticDescriptor MissingInjectionFailureCallbackImplementation =
        UserBehavior("004", Resources.U_MissingInjectionFailureCallbackImplementation);

    public static readonly DiagnosticDescriptor MissingInjectionReadyCallbackImplementation =
        UserBehavior("006", Resources.U_MissingInjectionReadyCallbackImplementation);

    public static readonly DiagnosticDescriptor ManualSetInjectionReadyField = UserBehavior(
        "005",
        Resources.U_ManualSetInjectionReadyField
    );
}
