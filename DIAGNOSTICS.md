# Diagnostic Code Reference

<p align="left"> <a href="DIAGNOSTICS.zh-CN.md">中文版</a> </p>

GodotSharpDI provides comprehensive error checking at compile time. This document lists all diagnostic codes and their meanings.

## Diagnostic Code Categories

| Prefix | Category | Description |
| ------ | ---------------- | ---------------- |
| GDI_C | Class | Class-level errors |
| GDI_M | Member | Member-level errors |
| GDI_S | Constructor | Constructor-level errors |
| GDI_D | Dependency Graph | Dependency graph errors |
| GDI_E | Internal Error | Internal errors |
| GDI_U | User Behavior | User behavior warnings |

------

## Class-Level Errors (GDI_C)

### GDI_C010: HostInvalidAttribute

**Message**: `Host '{0}' cannot use [{1}]`

**Cause**: Host is using incompatible attributes (`[Singleton]`).

**Solution**: Host is not a Service, remove lifecycle annotation.

```csharp
// ❌ Error
[Host]
[Singleton(typeof(IGameState))]  // Host cannot use Singleton
public partial class GameManager : Node { }

// ✅ Correct
[Host]
public partial class GameManager : Node, IGameState
{
    [Singleton(typeof(IGameState))]  // Use on members
    private IGameState Self => this;
}
```

------

### GDI_C011: UserInvalidAttribute

**Message**: `User '{0}' cannot use [{1}]`

**Cause**: User is using incompatible attributes (`[Singleton]`).

------

### GDI_C012: ScopeInvalidAttribute

**Message**: `Scope '{0}' cannot use [{1}]`

**Cause**: Scope is using incompatible attributes (such as `[Singleton]`, `[Host]`, `[User]`).

------

### GDI_C013: OnlyScopeCanUseModules

**Message**: `To use [Modules], Type '{0}' must implement IScope`

**Cause**: A class marked with [Modules] does not implement the IScope interface.

------

### GDI_C020: HostMustBeNode

**Message**: `Host '{0}' must inherit from Godot.Node`

**Cause**: A class marked as `[Host]` is not a Node subclass.

**Solution**: Host must inherit from Node.

```csharp
// ❌ Error
[Host]
public partial class MyHost { }  // Not a Node

// ✅ Correct
[Host]
public partial class MyHost : Node { }
```

------

### GDI_C021: UserMustBeNode

**Message**: `User '{0}' must inherit from Godot.Node`

**Cause**: A class marked as `[User]` is not a Node subclass.

------

### GDI_C022: ScopeMustBeNode

**Message**: `Scope '{0}' must inherit from Godot.Node`

**Cause**: A class implementing `IScope` is not a Node subclass.

------

### GDI_C030: ServiceReadyNeedUser

**Message**: `Type '{0}' implements IServicesReady but is not marked with [User]`

**Cause**: Implements `IServicesReady` but is not marked with `[User]`.

**Solution**: Add `[User]` annotation.

```csharp
// ❌ Error
public partial class MyComponent : Node, IServicesReady
{
    public void OnServicesReady() { }
}

// ✅ Correct
[User]
public partial class MyComponent : Node, IServicesReady
{
    public void OnServicesReady() { }
}
```

------

### GDI_C040: ScopeMissingModules

**Message**: `Scope '{0}' must specify [Modules]`

**Cause**: Scope does not have the `[Modules]` annotation.

**Solution**: Add `[Modules]` annotation.

------

### GDI_C050: DiClassMustBePartial

**Message**: `DI-relative class '{0}' must be declared as partial to enable code generation`

**Cause**: DI-related class is not declared as `partial`.

**Solution**: Add `partial` modifier.

```csharp
// ❌ Error
[Singleton(typeof(IService))]
public class MyService : IService { }

// ✅ Correct
[Singleton(typeof(IService))]
public partial class MyService : IService { }
```

------

### GDI_C060: ServiceTypeIsInvalid

**Message**: `Service '{0}' cannot inherit from Godot.Node, and must be non-abstract, non-static class type`

**Cause**: Service inherits from Node, or the type does not meet requirements (abstract class, static class, etc.).

------

### GDI_C070: ServiceExposedTypeNotImplemented

**Message**: `Service '{0}' has exposed type '{1}', but which is not implemented`

**Cause**: Service exposes an interface not implemented by its own type or a class type it doesn't inherit from.

```csharp
// ❌ Error
[Singleton(typeof(IService))]
public partial class MyService { }

// ✅ Correct
[Singleton(typeof(IService))]
public partial class MyService : IService { }
```

------

## Member-Level Errors (GDI_M)

### GDI_M010: MemberHasSingletonButNotInHost

**Message**: `Type '{0}' must be marked as [Host] to use [Singleton] on members`

**Cause**: A member of a non-Host class uses `[Singleton]`.

```csharp
// ❌ Error
[User]
public partial class MyUser : Node
{
    [Singleton(typeof(IService))]  // User cannot use
    private IService _service;
}
```

------

### GDI_M011: MemberHasInjectButNotInUser

**Message**: `Type '{0}' must be marked as [User] to use [Inject] on members`

**Cause**: A member of a non-User class uses `[Inject]`.

------

### GDI_M012: MemberConflictWithSingletonAndInject

**Message**: `[Singleton] and [Inject] cannot be applied to the same member`

**Cause**: The same member is marked with both `[Singleton]` and `[Inject]`.

------

### GDI_M020: InjectMemberNotAssignable

**Message**: `[Inject] member must be writable (field must not be readonly, property must have setter)`

**Cause**: Injection target is not writable.

```csharp
// ❌ Error
[User]
public partial class MyUser : Node
{
    [Inject] private readonly IService _service;  // readonly
    [Inject] public IConfig Config { get; }       // no setter
}

// ✅ Correct
[User]
public partial class MyUser : Node
{
    [Inject] private IService _service;
    [Inject] public IConfig Config { get; set; }
}
```

------

### GDI_M030: SingletonPropertyNotAccessible

**Message**: `[Singleton] property must have a getter`

**Cause**: Host member property does not have a getter.

------

### GDI_M040: InjectMemberInvalidType

**Message**: `Injected member in '{0}' has type '{1}', which is not a Service`

**Cause**: The type of the injection target is not a valid service type.

------

### GDI_M041: InjectMemberIsHostType

**Message**: `[Inject] member '{0}' has type '{1}', which is a [Host] type and cannot be injected`

**Cause**: Attempting to inject a Host type.

```csharp
// ❌ Error
[Host]
public partial class GameManager : Node { }

[User]
public partial class MyUser : Node
{
    [Inject] private GameManager _manager;  // Host cannot be injected
}

// ✅ Correct: Inject the interface exposed by Host
[User]
public partial class MyUser : Node
{
    [Inject] private IGameState _state;  // Inject interface
}
```

------

### GDI_M042: InjectMemberIsUserType

**Message**: `[Inject] member '{0}' has type '{1}', which is a [User] type and cannot be injected`

**Cause**: Attempting to inject a User type.

------

### GDI_M043: InjectMemberIsScopeType

**Message**: `[Inject] member '{0}' has type '{1}', which is an IScope type and cannot be injected`

**Cause**: Attempting to inject a Scope type.

------

### GDI_M044: InjectMemberIsStatic

**Message**: `[Inject] member '{0}' cannot be static`

**Cause**: A static member uses `[Inject]`.

```csharp
// ❌ Error
[User]
public partial class MyUser : Node
{
    [Inject] private static IService _service;
}
```

------

### GDI_M045: SingletonMemberIsStatic

**Message**: `[Singleton] member '{0}' cannot be static`

**Cause**: A static member uses `[Singleton]`.

------

### GDI_M050: HostSingletonMemberIsServiceType

**Message**: `[Singleton] member '{0}' has type '{1}', which is already marked as a Service. Host should not hold Service instances directly`

**Cause**: A Host member's type is a Service (marked with `[Singleton]`).

```csharp
// ❌ Error
[Singleton(typeof(IConfig))]
public partial class ConfigService : IConfig { }

[Host]
public partial class BadHost : Node
{
    [Singleton(typeof(IConfig))]
    private ConfigService _config = new();  // Type is a Service
}

// ✅ Correct: Use Host+User combination
[Host, User]
public partial class GoodHost : Node
{
    [Inject] private IConfig _config;  // Inject Service
}
```

------

### GDI_M060: ExposedTypeShouldBeInterface (Warning)

**Message**: `Exposed type '{0}' is a concrete class. Consider using an interface instead for better testability and loose coupling`

**Cause**: The exposed service type is a concrete class rather than an interface.

**Severity**: Warning (does not block compilation)

```csharp
// ⚠️ Warning
[Singleton(typeof(ConfigService))]  // Concrete class
public partial class ConfigService { }

// ✅ Recommended
[Singleton(typeof(IConfig))]  // Interface
public partial class ConfigService : IConfig { }
```

------

### GDI_M070: HostMemberExposedTypeNotImplemented

**Message**: `Host member '{0}' has exposed type '{1}', but which is not implemented`

**Cause**: A Host member exposes an interface not implemented by that member's type or a class type it doesn't inherit from.

```csharp
// ❌ Error
[Singleton(typeof(IService))]
public partial class MyHost
{
    [Singleton(typeof(IService))]
    private MyHost Self => this;
}

// ✅ Correct
[Singleton(typeof(IService))]
public partial class MyHost : IService
{
    [Singleton(typeof(IService))]
    private MyHost Self => this;
}
```

------

## Constructor-Level Errors (GDI_S)

### GDI_S010: NoNonStaticConstructor

**Message**: `Service '{0}' must define at least one non-static constructor`

**Cause**: Service does not have a non-static constructor.

------

### GDI_S011: AmbiguousConstructor

**Message**: `Service '{0}' has multiple constructors but no [InjectConstructor] is specified`

**Cause**: Multiple constructors exist but none is specified for use.

```csharp
// ❌ Error
[Singleton(typeof(IService))]
public partial class MyService : IService
{
    public MyService(IDep1 d1) { }
    public MyService(IDep1 d1, IDep2 d2) { }  // Ambiguous
}

// ✅ Correct
[Singleton(typeof(IService))]
public partial class MyService : IService
{
    [InjectConstructor]
    public MyService(IDep1 d1) { }
    
    public MyService(IDep1 d1, IDep2 d2) { }
}
```

------

### GDI_S012: InjectConstructorAttributeIsInvalid

**Message**: `Type '{0}' is not a Service but uses [InjectConstructor]`

**Cause**: A non-Service type uses `[InjectConstructor]`.

------

### GDI_S020: InjectConstructorParameterTypeInvalid

**Message**: `Inject constructor parameter must be an interface type, or a non-Node, non-Host, non-User and non-Scope class type`

**Cause**: Constructor parameter type is invalid.

------

## Dependency Graph Errors (GDI_D)

### GDI_D001: ScopeModulesServicesEmpty

**Message**: `Scope '{0}' must specify at least one type in [Modules] Services`

**Cause**: `Services` in `[Modules]` is empty.

------

### GDI_D002: ScopeModulesHostsEmpty (Info)

**Message**: `Scope '{0}' specifies no Host type in [Modules] Hosts`

**Severity**: Info (suggestion)

------

### GDI_D003: ScopeModulesServiceMustBeService

**Message**: `Scope '{0}' Modules Service type '{1}' must be a Service`

**Cause**: A type in `Services` is not a Service.

------

### GDI_D004: ScopeModulesHostMustBeHost

**Message**: `Scope '{0}' Modules Host type '{1}' must be a Host`

**Cause**: A type in `Hosts` is not a Host.

------

### GDI_D010: CircularDependencyDetected

**Message**: `Circular dependency detected: {0}`

**Cause**: Circular dependencies exist between services.

```csharp
// ❌ Circular dependency
[Singleton(typeof(IA))]
public partial class A : IA { public A(IB b) { } }

[Singleton(typeof(IB))]
public partial class B : IB { public B(IA a) { } }
// Detected: A -> B -> A
```

------

### GDI_D020: ServiceConstructorParameterInvalid

**Message**: `Service '{0}' has constructor parameter of type '{1}', which is not a Service`

**Cause**: A Service constructor parameter's type is not a valid service type.

------

### GDI_D040: ServiceTypeConflict

**Message**: `Service type '{0}' is registered by multiple providers: {1}. Each service type must have exactly one provider within a Scope`

**Cause**: The same service type has multiple providers.

```csharp
// ❌ Conflict
[Singleton(typeof(IService))]
public partial class ServiceA : IService { }

[Singleton(typeof(IService))]
public partial class ServiceB : IService { }

[Modules(Services = [typeof(ServiceA), typeof(ServiceB)])]
public partial class MyScope : Node, IScope { }
// Both provide IService, conflict
```

------

## Internal Errors (GDI_E)

### GDI_E900: RequestCancellation

**Message**: `Generator receives cancellation request: {0}`

**Cause**: Source generator execution was cancelled.

------

### GDI_E910: GeneratorInternalError

**Message**: `Internal error in source generator: {0}`

**Cause**: Internal error in the source generator.

------

### GDI_E920: UnknownTypeRole

**Message**: `Unknown DI Type Role`

**Cause**: Unknown DI role classification.

------

### GDI_E930: ScopeLosesAttributeUnexpectedly

**Message**: `Scope '{0}' Unexpectedly loses [Modules] or [AutoModules]`

**Cause**: Scope unexpectedly lost `[Modules]` or `[AutoModules]`.

------

## User Behavior Warnings (GDI_U)

### GDI_U001: ManualCallGeneratedMethod

**Message**: `Do not manually call generated method '{0}' on '{1}'. This method is managed by the DI framework and will be called automatically at the appropriate time`

**Cause**: Manually calling a framework-generated method.

------

### GDI_U002: ManualAccessGeneratedField

**Message**: `Do not manually access generated field '{0}' on '{1}'. This field is managed by the DI framework and should not be accessed directly by user code`

**Cause**: Manually accessing a framework-generated private field.

------

### GDI_U003: ManualAccessGeneratedProperty

**Message**: `Do not manually access generated property '{0}' on '{1}'. This property is managed by the DI framework and should not be accessed directly by user code`

**Cause**: Manually accessing a framework-generated property.
