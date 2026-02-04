## Release 1.0.0

### New Rules

| Rule ID  | Category            | Severity | Notes                                                          |
|----------|---------------------|----------|----------------------------------------------------------------|
| GDI_C010 | GDI.Class           | Error    | Host cannot use incompatible attributes                        |
| GDI_C011 | GDI.Class           | Error    | User cannot use incompatible attributes                        |
| GDI_C012 | GDI.Class           | Error    | Scope cannot use incompatible attributes                       |
| GDI_C013 | GDI.Class           | Error    | To use [Modules] must implement IScope                         |
| GDI_C020 | GDI.Class           | Error    | Host must inherit from Godot.Node                              |
| GDI_C021 | GDI.Class           | Error    | User must inherit from Godot.Node                              |
| GDI_C022 | GDI.Class           | Error    | Scope must inherit from Godot.Node                             |
| GDI_C030 | GDI.Class           | Error    | IServicesReady implementation requires [User] attribute        |
| GDI_C040 | GDI.Class           | Error    | Scope must specify [Modules]                                   |
| GDI_C050 | GDI.Class           | Error    | DI-relative class must be partial                              |
| GDI_C060 | GDI.Class           | Error    | Service must be non-Godot.Node, non-abstract, non-static class |
| GDI_M070 | GDI.Class           | Warning  | Serive exposed type should be interface                        |
| GDI_C071 | GDI.Class           | Error    | Service exposes type but does not implement it                 |
| GDI_C080 | GDI.Class           | Error    | Host, User or Scope missing _Notification declaration          |
| GDI_C081 | GDI.Class           | Error    | Unexpected Signature of _Notification declaration              |
| GDI_M010 | GDI.Member          | Error    | [Singleton] on member requires [Host] on type                  |
| GDI_M011 | GDI.Member          | Error    | [Inject] on member requires [User] on type                     |
| GDI_M012 | GDI.Member          | Error    | [Singleton] and [Inject] cannot be on same member              |
| GDI_M020 | GDI.Member          | Error    | [Inject] member must be writable                               |
| GDI_M030 | GDI.Member          | Error    | [Singleton] property must have getter                          |
| GDI_M040 | GDI.Member          | Error    | Injected member type must be a Service                         |
| GDI_M041 | GDI.Member          | Warning  | Should not inject Host type directly (prefer interfaces)       |
| GDI_M042 | GDI.Member          | Error    | Cannot inject User type                                        |
| GDI_M043 | GDI.Member          | Error    | Cannot inject Scope type                                       |
| GDI_M044 | GDI.Member          | Error    | [Inject] member cannot be static                               |
| GDI_M045 | GDI.Member          | Error    | [Singleton] member cannot be static                            |
| GDI_M050 | GDI.Member          | Error    | Host member cannot be Service type                             |
| GDI_M060 | GDI.Member          | Warning  | Host member exposed type should be interface                   |
| GDI_M061 | GDI.Member          | Error    | Host member exposed type is not injectable                     |
| GDI_M062 | GDI.Member          | Error    | Host member exposes type but member type does not implement it |
| GDI_M070 | GDI.Member          | Warning  | Host has not member marked as [Singleton]                      |
| GDI_M071 | GDI.Member          | Warning  | User has not member marked as [Inject]                         |
| GDI_S010 | GDI.Constructor     | Error    | Service must define at least one non-static constructor        |
| GDI_S011 | GDI.Constructor     | Error    | Multiple constructors require [InjectConstructor]              |
| GDI_S012 | GDI.Constructor     | Error    | [InjectConstructor] is invalid on non-Service                  |
| GDI_S020 | GDI.Constructor     | Error    | Inject constructor parameter type invalid                      |
| GDI_D001 | GDI.DependencyGraph | Warning  | Scope specifies no Services or Hosts in [Modules]              |
| GDI_D002 | GDI.DependencyGraph | Error    | Modules Services must be Service types                         |
| GDI_D003 | GDI.DependencyGraph | Error    | Modules Hosts must be Host types                               |
| GDI_D010 | GDI.DependencyGraph | Error    | Circular dependency detected                                   |
| GDI_D020 | GDI.DependencyGraph | Error    | Service constructor parameter invalid                          |
| GDI_D040 | GDI.DependencyGraph | Error    | Service type conflict - multiple providers                     |
| GDI_E900 | GDI.Generator       | Error    | Generator cancellation requested                               |
| GDI_E910 | GDI.Generator       | Error    | Internal generator error                                       |
| GDI_E920 | GDI.Generator       | Error    | Unknown DI type role                                           |
| GDI_E930 | GDI.Generator       | Error    | Scope unexpectedly loses Modules attribute                     |
| GDI_U001 | GDI.User            | Error    | Manual call to generated method not allowed                    |
| GDI_U002 | GDI.User            | Error    | Manual access to generated field not allowed                   |
| GDI_U003 | GDI.User            | Error    | Manual access to generated property not allowed                |