# Release 1.2.0

## New Rules

### Member-level Diagnostics (M_)

| Rule ID  | Category   | Severity | Notes                                             |
|----------|------------|----------|---------------------------------------------------|
| GDI_M061 | GDI.Member | Warning  | [Provide] member exposed type should be interface |

### Dependency Graph Diagnostics (D_)

| Rule ID  | Category            | Severity | Notes                                                       |
|----------|---------------------|----------|-------------------------------------------------------------|
| GDI_D011 | GDI.DependencyGraph | Error    | Cross-host WaitFor deadlock detected (Tarjan SCC algorithm) |

## Removed Rules

| Rule ID  | Category            | Severity | Notes                                                                 |
|----------|---------------------|----------|-----------------------------------------------------------------------|
| GDI_C062 | GDI.Class           | Error    | Removed: service-type generic check (service architecture retired)    |
| GDI_C080 | GDI.Class           | Error    | Renumbered to GDI_C060 (MissingNotificationMethod)                    |
| GDI_C081 | GDI.Class           | Error    | Renumbered to GDI_C061 (InvalidNotificationMethodSignature)           |
| GDI_D002 | GDI.DependencyGraph | Error    | Removed: [Modules] service-type check (service architecture retired)  |
| GDI_E030 | GDI.Generator       | Warning  | Removed: service provider registration failure (no longer applicable) |

## Changed Rules

### Member-level Diagnostics (M_)

| Rule ID  | Category   | Old Severity | New Severity | Notes                                                             |
|----------|------------|--------------|--------------|-------------------------------------------------------------------|
| GDI_M081 | GDI.Member | Warning      | Error        | WaitFor field not [Inject] — TCS never resolves; now a hard error |

### Rule Renames (same ID, updated description)

The following rules kept their IDs but were renamed as part of the
provider-based architecture cleanup. The diagnostic behaviour is unchanged
except where noted.

| Rule ID  | Old Name                                  | New Name                                |
|----------|-------------------------------------------|-----------------------------------------|
| GDI_C060 | ServiceTypeIsInvalid                      | MissingNotificationMethod               |
| GDI_C061 | ServiceCannotBeNode                       | InvalidNotificationMethodSignature      |
| GDI_M051 | SingletonMemberTypeIsInvalid              | ProvideMemberTypeIsInvalid              |
| GDI_M053 | SingletonMemberIsUserType                 | ProvideMemberIsUserType                 |
| GDI_M054 | SingletonMemberIsScopeType                | ProvideMemberIsScopeType                |
| GDI_M055 | SingletonMemberIsRegularNode              | ProvideMemberIsRegularNode              |
| GDI_M056 | SingletonMemberTypeCannotBeGeneric        | ProvideMemberTypeCannotBeGeneric        |
| GDI_M060 | SingletonMemberExposedTypeNotImplemented  | ProvideMemberExposedTypeNotImplemented  |
| GDI_M062 | SingletonMemberExposedTypeCannotBeGeneric | ProvideMemberExposedTypeCannotBeGeneric |
| GDI_M070 | HostMissingSingletonMember                | HostMissingProvideMember                |

---

# Release 1.1.1

## New Rules

### User Behavior Diagnostics (U_)

| Rule ID  | Category | Severity | Notes                                                                   |
|----------|----------|----------|-------------------------------------------------------------------------|
| GDI_U006 | GDI.User | Error    | Missing implementation of [Inject(ReadyCallback = true)] partial method |

---

# Release 1.1.0

## New Rules

### Class-level Diagnostics (C_)

| Rule ID  | Category  | Severity | Notes                                   |
|----------|-----------|----------|-----------------------------------------|
| GDI_C023 | GDI.Class | Error    | Host cannot be an unbound generic type  |
| GDI_C024 | GDI.Class | Error    | User cannot be an unbound generic type  |
| GDI_C025 | GDI.Class | Error    | Scope cannot be an unbound generic type |

### Member-level Diagnostics (M_)

| Rule ID  | Category   | Severity | Notes                                                               |
|----------|------------|----------|---------------------------------------------------------------------|
| GDI_M031 | GDI.Member | Error    | [Provide] method cannot return void                                 |
| GDI_M032 | GDI.Member | Error    | [Provide] method must be parameterless                              |
| GDI_M045 | GDI.Member | Error    | [Inject] member type is a regular Godot.Node (not DI-annotated)     |
| GDI_M050 | GDI.Member | Error    | [Provide] member cannot be static                                   |
| GDI_M051 | GDI.Member | Error    | [Provide] member type is not a valid service type                   |
| GDI_M052 | GDI.Member | Error    | [Provide] member type is a [Host] type (Host exposes only itself)   |
| GDI_M053 | GDI.Member | Error    | [Provide] member cannot be a [User] type                            |
| GDI_M054 | GDI.Member | Error    | [Provide] member cannot be an IScope type                           |
| GDI_M055 | GDI.Member | Error    | [Provide] member cannot be a regular Godot.Node                     |
| GDI_M056 | GDI.Member | Error    | [Provide] member cannot be an unbound generic type                  |
| GDI_M060 | GDI.Member | Error    | [Provide] member exposed type is not implemented by the return type |
| GDI_M062 | GDI.Member | Error    | [Provide] member cannot expose an unbound generic type              |
| GDI_M070 | GDI.Member | Warning  | Host has no member marked as [Provide]                              |
| GDI_M080 | GDI.Member | Error    | WaitFor references a field that does not exist in this class        |
| GDI_M081 | GDI.Member | Warning  | WaitFor field is not [Inject]; TCS will never be resolved           |
| GDI_M082 | GDI.Member | Error    | WaitFor creates a circular dependency within this Host              |

### User Behavior Diagnostics (U_)

| Rule ID  | Category | Severity | Notes                                                                      |
|----------|----------|----------|----------------------------------------------------------------------------|
| GDI_U004 | GDI.User | Error    | Missing implementation of [Inject(FailureCallback = true)] partial method  |

## Removed Rules

The following rules were retired when the service constructor-injection architecture
was replaced by the provider-based (IScope / `[Provide]`) architecture.

| Rule ID  | Category            | Severity | Notes                                                         |
|----------|---------------------|----------|---------------------------------------------------------------|
| GDI_C060 | GDI.Class           | Error    | Removed: Service must be non-abstract non-static class        |
| GDI_C061 | GDI.Class           | Error    | Removed: Service cannot inherit from Godot.Node               |
| GDI_C062 | GDI.Class           | Error    | Removed: Service cannot be generic (replaced in 1.2.0)        |
| GDI_C070 | GDI.Class           | Warning  | Removed: Service exposed type should be interface             |
| GDI_C071 | GDI.Class           | Error    | Removed: Service must implement its exposed type              |
| GDI_C072 | GDI.Class           | Error    | Removed: Service cannot expose generic type                   |
| GDI_D002 | GDI.DependencyGraph | Error    | Removed: [Modules] Services must be Service types             |
| GDI_D020 | GDI.DependencyGraph | Error    | Removed: Service constructor parameter type is not a Service  |
| GDI_S001 | GDI.Constructor     | Error    | Removed: Service must have a public parameterless constructor |

---

# Release 1.0.0

## New Rules

### Class-level Diagnostics (C_)

| Rule ID  | Category  | Severity | Notes                                                                                 |
|----------|-----------|----------|---------------------------------------------------------------------------------------|
| GDI_C010 | GDI.Class | Error    | Host cannot use incompatible DI attributes                                            |
| GDI_C011 | GDI.Class | Error    | User cannot use incompatible DI attributes                                            |
| GDI_C012 | GDI.Class | Error    | Scope cannot use incompatible DI attributes                                           |
| GDI_C013 | GDI.Class | Error    | To use [Modules] the type must implement IScope                                       |
| GDI_C020 | GDI.Class | Error    | Host must inherit from Godot.Node                                                     |
| GDI_C021 | GDI.Class | Error    | User must inherit from Godot.Node                                                     |
| GDI_C022 | GDI.Class | Error    | Scope must inherit from Godot.Node                                                    |
| GDI_C030 | GDI.Class | Error    | IDependenciesResolved requires [User] or [Host]                                       |
| GDI_C040 | GDI.Class | Error    | Scope must specify [Modules]                                                          |
| GDI_C050 | GDI.Class | Error    | DI-relative class must be declared as partial                                         |
| GDI_C060 | GDI.Class | Error    | Service must be non-abstract, non-static class (retired in 1.1.0)                     |
| GDI_C061 | GDI.Class | Error    | Service cannot inherit from Godot.Node (retired in 1.1.0)                             |
| GDI_C062 | GDI.Class | Error    | Service cannot be generic type (retired in 1.2.0)                                     |
| GDI_C070 | GDI.Class | Warning  | Service exposed type should be interface (retired in 1.1.0)                           |
| GDI_C071 | GDI.Class | Error    | Service must implement its exposed type (retired in 1.1.0)                            |
| GDI_C072 | GDI.Class | Error    | Service cannot expose generic type (retired in 1.1.0)                                 |
| GDI_C080 | GDI.Class | Error    | Host/User/Scope missing `_Notification` declaration (renumbered to GDI_C060 in 1.2.0) |
| GDI_C081 | GDI.Class | Error    | `_Notification` has incorrect signature (renumbered to GDI_C061 in 1.2.0)             |

### Member-level Diagnostics (M_)

| Rule ID  | Category   | Severity | Notes                                                             |
|----------|------------|----------|-------------------------------------------------------------------|
| GDI_M010 | GDI.Member | Error    | [Provide] member must be in a [Host] type                         |
| GDI_M011 | GDI.Member | Error    | [Inject] member must be in a [Host] or [User] type                |
| GDI_M012 | GDI.Member | Error    | [Provide] and [Inject] cannot be on the same member               |
| GDI_M020 | GDI.Member | Error    | [Inject] member must be writable (not readonly, has setter)       |
| GDI_M030 | GDI.Member | Error    | [Provide] property must have a getter                             |
| GDI_M040 | GDI.Member | Error    | [Inject] member cannot be static                                  |
| GDI_M041 | GDI.Member | Error    | [Inject] member type is not a valid injectable type               |
| GDI_M042 | GDI.Member | Warning  | [Inject] member type is a [Host] (allowed, not recommended)       |
| GDI_M043 | GDI.Member | Error    | Cannot inject a [User] type                                       |
| GDI_M044 | GDI.Member | Error    | Cannot inject an IScope type                                      |
| GDI_M046 | GDI.Member | Warning  | [Inject] member type should be interface                          |
| GDI_M047 | GDI.Member | Error    | [Inject] member cannot be unbound generic type                    |
| GDI_M071 | GDI.Member | Warning  | User has no member marked as [Inject]                             |

### Constructor-level Diagnostics (S_)

| Rule ID  | Category        | Severity | Notes                                                                 |
|----------|-----------------|----------|-----------------------------------------------------------------------|
| GDI_S001 | GDI.Constructor | Error    | Service must have public parameterless constructor (retired in 1.1.0) |

### Dependency Graph Diagnostics (D_)

| Rule ID  | Category            | Severity | Notes                                                             |
|----------|---------------------|----------|-------------------------------------------------------------------|
| GDI_D001 | GDI.DependencyGraph | Warning  | Scope specifies no Hosts in [Modules]                             |
| GDI_D002 | GDI.DependencyGraph | Error    | [Modules] Services must be Service types (retired in 1.1.0)       |
| GDI_D003 | GDI.DependencyGraph | Error    | [Modules] Hosts must be [Host] types                              |
| GDI_D010 | GDI.DependencyGraph | Error    | Circular WaitFor dependency within a single Host                  |
| GDI_D020 | GDI.DependencyGraph | Error    | Service constructor parameter is not a Service (retired in 1.1.0) |
| GDI_D040 | GDI.DependencyGraph | Error    | Service type conflict — multiple providers in Scope               |
| GDI_D050 | GDI.DependencyGraph | Error    | [Inject] member type not exposed by any service                   |

### Internal Error Diagnostics (E_)

| Rule ID  | Category      | Severity | Notes                                                   |
|----------|---------------|----------|---------------------------------------------------------|
| GDI_E001 | GDI.Generator | Error    | Source generator initialization failed                  |
| GDI_E010 | GDI.Generator | Warning  | Class analysis failed                                   |
| GDI_E011 | GDI.Generator | Warning  | Symbol cache unavailable for class                      |
| GDI_E012 | GDI.Generator | Warning  | Class validation failed                                 |
| GDI_E020 | GDI.Generator | Error    | Dependency graph build failed                           |
| GDI_E021 | GDI.Generator | Error    | Graph build phase failed                                |
| GDI_E030 | GDI.Generator | Warning  | Service provider registration failed (retired in 1.2.0) |
| GDI_E040 | GDI.Generator | Warning  | Node build failed                                       |
| GDI_E050 | GDI.Generator | Warning  | Dependency graph validation failed                      |
| GDI_E100 | GDI.Generator | Error    | Code generation failed                                  |
| GDI_E101 | GDI.Generator | Error    | Source output failed                                    |

### User Behavior Diagnostics (U_)

| Rule ID  | Category | Severity | Notes                                                            |
|----------|----------|----------|------------------------------------------------------------------|
| GDI_U001 | GDI.User | Error    | Manual call to generated method is not allowed                   |
| GDI_U002 | GDI.User | Error    | Manual access to generated field is not allowed                  |
| GDI_U003 | GDI.User | Error    | Manual access to generated property is not allowed               |
| GDI_U005 | GDI.User | Error    | Manual assignment to injection-ready field is not allowed        |