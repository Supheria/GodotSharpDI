; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

# Analyzer Releases

## Release 1.0.0

### New Rules

# Class-level diagnostics (C)
GDI_C001 ServiceLifetimeConflict
GDI_C010 RoleConflict_ServiceHost
GDI_C011 RoleConflict_ServiceUser
GDI_C012 RoleConflict_ServiceScope
GDI_C013 RoleConflict_HostScope
GDI_C014 RoleConflict_UserScope
GDI_C015 RoleConflict_HostUserService
GDI_C016 RoleConflict_HostUserScope
GDI_C020 ServiceReadyNeedUser

# Service-level diagnostics (S)
GDI_S010 ServiceCannotBeNode
GDI_S020 NoPublicConstructor
GDI_S021 AmbiguousConstructor
GDI_S022 InvalidInjectConstructorAttribute
GDI_S030 ServiceConstructorParameterInvalid
GDI_S040 SingletonCannotDependOnTransient

# Member-level diagnostics (M)
GDI_M010 InvalidMemberAttribute
GDI_M011 MemberAttributeConflict
GDI_M020 InjectMemberNotAssignable
GDI_M030 SingletonPropertyNotAccessible
GDI_M040 InjectMemberInvalidType

# Scope-level diagnostics (P)
GDI_P010 ScopeMustBeNode
GDI_P020 InvalidModuleAttribute
GDI_P030 ScopeModulesConflict
GDI_P040 ScopeMissingModules
GDI_P050 ScopeInstantiateMustBeService
GDI_P060 ScopeExpectMustBeHost

# Dependency graph diagnostics (D)
GDI_D010 HostServiceNotFound
GDI_D011 HostServiceMustBeService
GDI_D020 CircularDependencyDetected

# User behavior diagnostics (U)
GDI_U004 ManualAttachToScope
GDI_U005 ManualResolveUserDependencies

# Generator diagnostics (G)
GDI_G900 GeneratorInternalError
