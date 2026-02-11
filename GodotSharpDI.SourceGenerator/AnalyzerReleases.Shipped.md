# Release 1.0.0

## New Rules

### Class-level Diagnostics (C_)

| Rule ID  | Category    | Severity | Notes                                                          |
|----------|-------------|----------|----------------------------------------------------------------|
| GDI_C010 | GDI.Class   | Error    | Host cannot use incompatible attributes                        |
| GDI_C011 | GDI.Class   | Error    | User cannot use incompatible attributes                        |
| GDI_C012 | GDI.Class   | Error    | Scope cannot use incompatible attributes                       |
| GDI_C013 | GDI.Class   | Error    | To use [Modules] must implement IScope                         |
| GDI_C020 | GDI.Class   | Error    | Host must inherit from Godot.Node                              |
| GDI_C021 | GDI.Class   | Error    | User must inherit from Godot.Node                              |
| GDI_C022 | GDI.Class   | Error    | Scope must inherit from Godot.Node                             |
| GDI_C030 | GDI.Class   | Error    | IDependenciesResolved implementation requires [User] attribute |
| GDI_C040 | GDI.Class   | Error    | Scope must specify [Modules]                                   |
| GDI_C050 | GDI.Class   | Error    | DI-relative class must be partial                              |
| GDI_C060 | GDI.Class   | Error    | Service must be non-abstract, non-static class                 |
| GDI_C061 | GDI.Class   | Error    | Service cannot inherit from Godot.Node                         |
| GDI_C062 | GDI.Class   | Error    | Service cannot be generic type                                 |
| GDI_C070 | GDI.Class   | Warning  | Service exposed type should be interface                       |
| GDI_C071 | GDI.Class   | Error    | Service exposes type but does not implement it                 |
| GDI_C072 | GDI.Class   | Error    | Service cannot expose generic type                             |
| GDI_C080 | GDI.Class   | Error    | Host, User or Scope missing _Notification declaration          |
| GDI_C081 | GDI.Class   | Error    | Unexpected Signature of _Notification declaration              |

### Member-level Diagnostics (M_)

| Rule ID  | Category    | Severity | Notes                                                                       |
|----------|-------------|----------|-----------------------------------------------------------------------------|
| GDI_M010 | GDI.Member  | Error    | [Provide] member must in Service or Host type                               |
| GDI_M011 | GDI.Member  | Error    | [Inject] member must in Service, Host or User type                          |
| GDI_M012 | GDI.Member  | Error    | [Provide] and [Inject] cannot be on same member                             |
| GDI_M020 | GDI.Member  | Error    | [Inject] member must be writable                                            |
| GDI_M030 | GDI.Member  | Error    | [Provide] property must have getter                                         |
| GDI_M031 | GDI.Member  | Error    | [Provide] method cannot return void                                         |
| GDI_M032 | GDI.Member  | Error    | [Provide] method must be parameterless                                      |
| GDI_M040 | GDI.Member  | Error    | [Inject] member cannot be static                                            |
| GDI_M041 | GDI.Member  | Error    | [Inject] member type must be valid injectable type                          |
| GDI_M042 | GDI.Member  | Warning  | Inject member is Host type (allowed but not recommended)                    |
| GDI_M043 | GDI.Member  | Error    | Cannot inject User type                                                     |
| GDI_M044 | GDI.Member  | Error    | Cannot inject Scope type                                                    |
| GDI_M045 | GDI.Member  | Error    | Inject member cannot be regular Node                                        |
| GDI_M046 | GDI.Member  | Warning  | Inject member type should be interface (for better testability)             |
| GDI_M047 | GDI.Member  | Error    | Inject member cannot be generic type                                        |
| GDI_M050 | GDI.Member  | Error    | [Provide] member cannot be static                                           |
| GDI_M051 | GDI.Member  | Error    | Singleton member type is invalid                                            |
| GDI_M052 | GDI.Member  | Warning  | [Provide] member is Service or Host type (they can only expose themselvies) |
| GDI_M053 | GDI.Member  | Error    | Singleton member cannot be User type                                        |
| GDI_M054 | GDI.Member  | Error    | Singleton member cannot be Scope type                                       |
| GDI_M055 | GDI.Member  | Error    | Singleton member cannot be regular Node                                     |
| GDI_M056 | GDI.Member  | Error    | Singleton member cannot be generic type                                     |
| GDI_M061 | GDI.Member  | Warning  | Singleton member exposed type should be interface                           |
| GDI_M062 | GDI.Member  | Warning  | Singleton member cannot expose generic type                                 |
| GDI_M070 | GDI.Member  | Warning  | Host has no member marked as [Singleton]                                    |
| GDI_M071 | GDI.Member  | Warning  | User has no member marked as [Inject]                                       |

### Constructor-level Diagnostics (S_)

| Rule ID  | Category        | Severity | Notes                                              |
|----------|-----------------|----------|----------------------------------------------------|
| GDI_S001 | GDI.Constructor | Error    | Service must have public parameterless constructor |

### Dependency Graph Diagnostics (D_)

| Rule ID  | Category            | Severity | Notes                                             |
|----------|---------------------|----------|---------------------------------------------------|
| GDI_D001 | GDI.DependencyGraph | Warning  | Scope specifies no Services or Hosts in [Modules] |
| GDI_D002 | GDI.DependencyGraph | Error    | Modules Services must be Service types            |
| GDI_D003 | GDI.DependencyGraph | Error    | Modules Hosts must be Host types                  |
| GDI_D010 | GDI.DependencyGraph | Error    | Circular dependency detected                      |
| GDI_D020 | GDI.DependencyGraph | Error    | Service constructor parameter invalid             |
| GDI_D040 | GDI.DependencyGraph | Error    | Service type conflict - multiple providers        |
| GDI_D050 | GDI.DependencyGraph | Error    | Inject member type is not exposed by any service  |

### Internal Error Diagnostics (E_)

| Rule ID  | Category      | Severity | Notes                                  |
|----------|---------------|----------|----------------------------------------|
| GDI_E001 | GDI.Generator | Error    | Source generator initialization failed |
| GDI_E010 | GDI.Generator | Warning  | Class analysis failed                  |
| GDI_E011 | GDI.Generator | Warning  | Symbol cache unavailable for class     |
| GDI_E012 | GDI.Generator | Warning  | Class validation failed                |
| GDI_E020 | GDI.Generator | Error    | Dependency graph build failed          |
| GDI_E021 | GDI.Generator | Error    | Graph build phase failed               |
| GDI_E030 | GDI.Generator | Warning  | Service provider registration failed   |
| GDI_E040 | GDI.Generator | Warning  | Node build failed                      |
| GDI_E050 | GDI.Generator | Warning  | Dependency graph validation failed     |
| GDI_E100 | GDI.Generator | Error    | Code generation failed                 |
| GDI_E101 | GDI.Generator | Error    | Source output failed                   |


### User Behavior Diagnostics (U_)

| Rule ID  | Category  | Severity | Notes                                                                    |
|----------|-----------|----------|--------------------------------------------------------------------------|
| GDI_U001 | GDI.User  | Error    | Manual call to generated method not allowed                              |
| GDI_U002 | GDI.User  | Error    | Manual access to generated field not allowed                             |
| GDI_U003 | GDI.User  | Error    | Manual access to generated property not allowed                          |
| GDI_U004 | GDI.User  | Error    | Missing implementation of injection failure callback for [Inject] member |
| GDI_U005 | GDI.User  | Error    | Manual assign injection ready field is not allowed                       |

## Design Decisions

### Host Type Injection (GDI_M041, GDI_S021)

**Decision**: Allow with Warning (changed from Error)

**Rationale**:
- Documentation stated "allowed but not recommended"
- Some legitimate use cases exist (e.g., quick prototypes, specific scenarios)
- Warning Provide guidance without blocking compilation
- Users can suppress warning if they understand the trade-offs

**Best Practice**: Inject interfaces exposed by Host instead of Host itself

**Example**:
```csharp
// ⚠️ Warning - Allowed but not recommended
[User]
public partial class MyUser : Node
{
    [Inject] private GameManager _hostInstance;  // Warning: GDI_M041
}

// ✅ Recommended - No warning
[User]
public partial class MyUser : Node
{
    [Inject] private IGameState _state;  // Inject interface exposed by Host
}
```

### Comprehensive Type Validation

**Decision**: Provide specific diagnostics for each invalid type

**Rationale**:
- More precise error messages help developers understand what went wrong
- Separate diagnostics for Host, User, Scope, and regular Node types
- Consistent validation across injection points (members, constructors, parameters)

**Coverage**:
- Member injection validation: GDI_M041-M045
- Constructor parameter validation: GDI_S021-S025
- Singleton member validation: GDI_M050-M056

### Interface Recommendation (GDI_M045, GDI_S025, GDI_M056)

**Decision**: Warn when concrete classes are used instead of interfaces

**Rationale**:
- Promotes dependency inversion principle (SOLID)
- Improves testability (easier to mock)
- Encourages loose coupling
- Warning level allows flexibility for legitimate concrete class usage

## Resource Organization

All diagnostic messages use prefixed resource names for better organization:
- `C_*` - Class-level diagnostics
- `M_*` - Member-level diagnostics
- `S_*` - Constructor-level diagnostics
- `D_*` - Dependency graph diagnostics
- `E_*` - Internal error diagnostics
- `U_*` - User behavior diagnostics

This naming convention improves:
- Code maintainability
- Resource file navigation
- Diagnostic categorization
- Documentation clarity

## Localization

Full localization support provided:
- English (en): `Resources.resx`
- Simplified Chinese (zh-Hans): `Resources_zh-hans.resx`

All diagnostic messages are fully translated in both languages.

