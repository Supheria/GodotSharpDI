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

    public static readonly DiagnosticDescriptor IDependenciesResolvedNeedUser = Class(
        "030",
        Resources.C_IDependenciesResolvedNeedUser
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

    public static readonly DiagnosticDescriptor ServiceCannotBeNode = Class(
        "061",
        Resources.C_ServiceCannotBeNode
    );

    public static readonly DiagnosticDescriptor ServiceTypeCannotBeGeneric = Class(
        "062",
        Resources.C_ServiceTypeCannotBeGeneric
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
        Resources.M_InjectMemberIsStatic
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

    // TODO: change singleton member to provide member

    public static readonly DiagnosticDescriptor SingletonMemberTypeIsInvalid = Member(
        "051",
        Resources.M_SingletonMemberTypeIsInvalid
    );

    public static readonly DiagnosticDescriptor ProvideMemberIsServiceOrHostType = Member(
        "052",
        Resources.M_ProvideMemberIsServiceOrHostType,
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor SingletonMemberIsUserType = Member(
        "053",
        Resources.M_SingletonMemberIsUserType
    );

    public static readonly DiagnosticDescriptor SingletonMemberIsScopeType = Member(
        "054",
        Resources.M_SingletonMemberIsScopeType
    );

    public static readonly DiagnosticDescriptor SingletonMemberIsRegularNode = Member(
        "055",
        Resources.M_SingletonMemberIsRegularNode
    );

    public static readonly DiagnosticDescriptor SingletonMemberTypeCannotBeGeneric = Member(
        "056",
        Resources.M_SingletonMemberTypeCannotBeGeneric
    );

    public static readonly DiagnosticDescriptor SingletonMemberExposedTypeNotImplemented = Member(
        "060",
        Resources.M_SingletonMemberExposedTypeNotImplemented
    );

    public static readonly DiagnosticDescriptor SingletonMemberExposedTypeShouldBeInterface =
        Member(
            "061",
            Resources.M_SingletonMemberExposedTypeShouldBeInterface,
            DiagnosticSeverity.Warning
        );

    public static readonly DiagnosticDescriptor SingletonMemberExposedTypeCannotBeGeneric = Member(
        "062",
        Resources.M_SingletonMemberExposedTypeCannotBeGeneric
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

    // WaitFor 相关诊断
    public static readonly DiagnosticDescriptor WaitForFieldNotFound = Member(
        "080",
        "WaitFor 引用的字段 '{0}' 在成员 '{1}' 中不存在"
    );

    public static readonly DiagnosticDescriptor WaitForFieldNotInjected = Member(
        "081",
        "WaitFor 引用的字段 '{0}' 没有 [Inject] 特性，可能导致运行时错误",
        DiagnosticSeverity.Warning
    );

    public static readonly DiagnosticDescriptor WaitForCircularDependency = Member(
        "082",
        "成员 '{0}' 的 WaitFor 依赖存在循环引用"
    );

    // ============================================================
    // S — Constructor-level
    // ============================================================

    public static readonly DiagnosticDescriptor ServiceHasNoPublicParameterlessConstructor =
        Constructor("001", Resources.S_ServiceHasNoPublicParameterlessConstructor);

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

    public static readonly DiagnosticDescriptor ServiceProviderRegistrationFailed = InternalError(
        "030",
        Resources.E_ServiceProviderRegistrationFailed,
        DiagnosticSeverity.Warning
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

    public static readonly DiagnosticDescriptor ManualSetInjectionReadyField = UserBehavior(
        "005",
        Resources.U_ManualSetInjectionReadyField
    );
}
