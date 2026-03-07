; All shipped analyzer rules for GodotSharpDI.SourceGenerator
; Reflects the current state of DiagnosticDescriptors.cs as of v1.3.0

# Release 1.3.0

## Rules

### Class-level Diagnostics (C_)

| Rule ID  | Category  | Severity | Notes                                               |
|----------|-----------|----------|-----------------------------------------------------|
| GDI_C010 | GDI.Class | Error    | Host cannot use incompatible DI attributes          |
| GDI_C011 | GDI.Class | Error    | User cannot use incompatible DI attributes          |
| GDI_C012 | GDI.Class | Error    | Scope cannot use incompatible DI attributes         |
| GDI_C013 | GDI.Class | Error    | To use [Modules] the type must implement IScope     |
| GDI_C020 | GDI.Class | Error    | Host must inherit from Godot.Node                   |
| GDI_C021 | GDI.Class | Error    | User must inherit from Godot.Node                   |
| GDI_C022 | GDI.Class | Error    | Scope must inherit from Godot.Node                  |
| GDI_C023 | GDI.Class | Error    | Host cannot be an unbound generic type              |
| GDI_C024 | GDI.Class | Error    | User cannot be an unbound generic type              |
| GDI_C025 | GDI.Class | Error    | Scope cannot be an unbound generic type             |
| GDI_C030 | GDI.Class | Error    | IDependenciesResolved requires [User] or [Host]     |
| GDI_C040 | GDI.Class | Error    | Scope must specify [Modules]                        |
| GDI_C050 | GDI.Class | Error    | DI-relative class must be declared as partial       |
| GDI_C060 | GDI.Class | Error    | Host/User/Scope missing `_Notification` declaration |
| GDI_C061 | GDI.Class | Error    | `_Notification` has incorrect signature             |

### Member-level Diagnostics (M_)

| Rule ID  | Category   | Severity | Notes                                                               |
|----------|------------|----------|---------------------------------------------------------------------|
| GDI_M010 | GDI.Member | Error    | [Provide] member must be in a [Host] type                           |
| GDI_M011 | GDI.Member | Error    | [Inject] member must be in a [Host] or [User] type                  |
| GDI_M012 | GDI.Member | Error    | [Provide] and [Inject] cannot be on the same member                 |
| GDI_M020 | GDI.Member | Error    | [Inject] member must be writable (not readonly, has setter)         |
| GDI_M030 | GDI.Member | Error    | [Provide] property must have a getter                               |
| GDI_M031 | GDI.Member | Error    | [Provide] method cannot return void                                 |
| GDI_M032 | GDI.Member | Error    | [Provide] method must be parameterless                              |
| GDI_M040 | GDI.Member | Error    | [Inject] member cannot be static                                    |
| GDI_M041 | GDI.Member | Error    | [Inject] member type is not a valid injectable type                 |
| GDI_M042 | GDI.Member | Warning  | [Inject] member type is a [Host] (allowed, not recommended)         |
| GDI_M044 | GDI.Member | Error    | Cannot inject an IScope type                                        |
| GDI_M046 | GDI.Member | Warning  | [Inject] member type should be interface                            |
| GDI_M047 | GDI.Member | Error    | [Inject] member cannot be an unbound generic type                   |
| GDI_M050 | GDI.Member | Error    | [Provide] member cannot be static                                   |
| GDI_M051 | GDI.Member | Error    | [Provide] member type is not a valid service type                   |
| GDI_M052 | GDI.Member | Error    | [Provide] member type is a [Host] type (Host exposes only itself)   |
| GDI_M054 | GDI.Member | Error    | [Provide] member cannot be an IScope type                           |
| GDI_M056 | GDI.Member | Error    | [Provide] member cannot be an unbound generic type                  |
| GDI_M060 | GDI.Member | Error    | [Provide] member exposed type is not implemented by the return type |
| GDI_M061 | GDI.Member | Warning  | [Provide] member exposed type should be interface                   |
| GDI_M062 | GDI.Member | Error    | [Provide] member cannot expose an unbound generic type              |
| GDI_M070 | GDI.Member | Warning  | Host has no member marked as [Provide]                              |
| GDI_M071 | GDI.Member | Warning  | User has no member marked as [Inject]                               |
| GDI_M080 | GDI.Member | Error    | WaitFor references a field that does not exist in this class        |
| GDI_M081 | GDI.Member | Error    | WaitFor field is not marked with [Inject]                           |
| GDI_M082 | GDI.Member | Error    | WaitFor creates a circular dependency within this Host              |

### Dependency Graph Diagnostics (D_)

| Rule ID  | Category            | Severity | Notes                                                       |
|----------|---------------------|----------|-------------------------------------------------------------|
| GDI_D001 | GDI.DependencyGraph | Warning  | Scope specifies no Hosts in [Modules]                       |
| GDI_D003 | GDI.DependencyGraph | Error    | [Modules] Hosts must be [Host] types                        |
| GDI_D010 | GDI.DependencyGraph | Error    | Circular WaitFor dependency within a single Host            |
| GDI_D011 | GDI.DependencyGraph | Error    | Cross-host WaitFor deadlock detected (Tarjan SCC algorithm) |
| GDI_D040 | GDI.DependencyGraph | Error    | Service type conflict — multiple providers in Scope         |
| GDI_D050 | GDI.DependencyGraph | Error    | [Inject] member type not exposed by any service             |

### Internal Error Diagnostics (E_)

| Rule ID  | Category      | Severity | Notes                                  |
|----------|---------------|----------|----------------------------------------|
| GDI_E001 | GDI.Generator | Error    | Source generator initialization failed |
| GDI_E010 | GDI.Generator | Warning  | Class analysis failed                  |
| GDI_E011 | GDI.Generator | Warning  | Symbol cache unavailable for class     |
| GDI_E012 | GDI.Generator | Warning  | Class validation failed                |
| GDI_E020 | GDI.Generator | Error    | Dependency graph build failed          |
| GDI_E021 | GDI.Generator | Error    | Graph build phase failed               |
| GDI_E040 | GDI.Generator | Warning  | Node build failed                      |
| GDI_E050 | GDI.Generator | Warning  | Dependency graph validation failed     |
| GDI_E100 | GDI.Generator | Error    | Code generation failed                 |
| GDI_E101 | GDI.Generator | Error    | Source output failed                   |

### User Behavior Diagnostics (U_)

| Rule ID  | Category | Severity | Notes                                                                     |
|----------|----------|----------|---------------------------------------------------------------------------|
| GDI_U001 | GDI.User | Error    | Manual call to generated method is not allowed                            |
| GDI_U002 | GDI.User | Error    | Manual access to generated field is not allowed                           |
| GDI_U003 | GDI.User | Error    | Manual access to generated property is not allowed                        |
| GDI_U004 | GDI.User | Error    | Missing implementation of [Inject(FailureCallback = true)] partial method |
| GDI_U005 | GDI.User | Error    | Manual assignment to injection-ready field is not allowed                 |
| GDI_U006 | GDI.User | Error    | Missing implementation of [Inject(ReadyCallback = true)] partial method   |