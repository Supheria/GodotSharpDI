## Release 1.0.0

### New Rules

| Rule ID | Category | Severity | Notes |
|---------|----------|----------|-------|
| GDI_C001 | GDI.Class | Error | Service cannot be both Singleton and Transient |
| GDI_C010 | GDI.Class | Error | Host cannot use incompatible attributes |
| GDI_C011 | GDI.Class | Error | Scope cannot use incompatible attributes |
| GDI_C020 | GDI.Class | Error | Host must inherit from Godot.Node |
| GDI_C021 | GDI.Class | Error | Scope must inherit from Godot.Node |
| GDI_C030 | GDI.Class | Error | IServicesReady implementation requires [User] attribute |
| GDI_C040 | GDI.Class | Error | Scope must specify [Modules] or [AutoModules] |
| GDI_C050 | GDI.Class | Error | DI-relative class must be partial |
| GDI_C060 | GDI.Class | Error | Service type must be non-abstract, non-static class |
| GDI_M010 | GDI.Member | Error | [Singleton] on member requires [Host] on type |
| GDI_M011 | GDI.Member | Error | [Inject] on member requires [User] on type |
| GDI_M012 | GDI.Member | Error | [Singleton] and [Inject] cannot be on same member |
| GDI_M020 | GDI.Member | Error | [Inject] member must be writable |
| GDI_M030 | GDI.Member | Error | [Singleton] property must have getter |
| GDI_M040 | GDI.Member | Error | Injected member type must be a Service |
| GDI_M041 | GDI.Member | Error | Cannot inject Host type |
| GDI_M042 | GDI.Member | Error | Cannot inject User type |
| GDI_M043 | GDI.Member | Error | Cannot inject Scope type |
| GDI_M044 | GDI.Member | Error | [Inject] member cannot be static |
| GDI_M045 | GDI.Member | Error | [Singleton] member cannot be static |
| GDI_M050 | GDI.Member | Error | Host member cannot be Service type |
| GDI_M060 | GDI.Member | Warning | Exposed type should be interface |
| GDI_M070 | GDI.Member | Error | User member cannot be Node type |
| GDI_M071 | GDI.Member | Error | Non-Node User cannot contain User member |
| GDI_M072 | GDI.Member | Error | User member must be initialized |
| GDI_S010 | GDI.Constructor | Error | Service cannot inherit from Godot.Node |
| GDI_S020 | GDI.Constructor | Error | Service must define at least one constructor |
| GDI_S021 | GDI.Constructor | Error | Multiple constructors require [InjectConstructor] |
| GDI_S022 | GDI.Constructor | Error | [InjectConstructor] is invalid on non-Service |
| GDI_S030 | GDI.Constructor | Error | Inject constructor parameter type invalid |
| GDI_D001 | GDI.DependencyGraph | Error | Scope Services cannot be empty |
| GDI_D002 | GDI.DependencyGraph | Info | Scope Hosts is empty |
| GDI_D003 | GDI.DependencyGraph | Error | Modules Services must be Service types |
| GDI_D004 | GDI.DependencyGraph | Error | Modules Hosts must be Host types |
| GDI_D010 | GDI.DependencyGraph | Error | Circular dependency detected |
| GDI_D020 | GDI.DependencyGraph | Error | Service constructor parameter invalid |
| GDI_D030 | GDI.DependencyGraph | Error | Singleton cannot depend on Transient |
| GDI_D040 | GDI.DependencyGraph | Error | Service type conflict - multiple providers |
| GDI_E900 | GDI.Generator | Error | Generator cancellation requested |
| GDI_E910 | GDI.Generator | Error | Internal generator error |
| GDI_E920 | GDI.Generator | Error | Unknown DI type role |
| GDI_E930 | GDI.Generator | Error | Scope unexpectedly loses Modules attribute |
| GDI_U001 | GDI.User | Error | Manual call to generated method not allowed |