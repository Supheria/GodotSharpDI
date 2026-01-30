# 诊断代码参考

GodotSharp.DI 在编译时提供完整的错误检查。本文档列出所有诊断代码及其含义。

## 诊断代码分类

| 前缀  | 类别             | 说明             |
| ----- | ---------------- | ---------------- |
| GDI_C | Class            | 类级别错误       |
| GDI_M | Member           | 成员级别错误     |
| GDI_S | Constructor      | 构造函数级别错误 |
| GDI_D | Dependency Graph | 依赖图错误       |
| GDI_E | Internal Error   | 内部错误         |
| GDI_U | User Behavior    | 用户行为警告     |

------

## Class 级别错误 (GDI_C)

### GDI_C010: HostInvalidAttribute

**消息**: `Host '{0}' cannot use [{1}]`

**原因**: Host 使用了不兼容的特性（如 `[Singleton]` 或 `[Transient]`）。

**解决方案**: Host 不是 Service，移除生命周期标记。

```csharp
// ❌ 错误
[Host]
[Singleton(typeof(IGameState))]  // Host 不能用 Singleton
public partial class GameManager : Node { }

// ✅ 正确
[Host]
public partial class GameManager : Node, IGameState
{
    [Singleton(typeof(IGameState))]  // 在成员上使用
    private IGameState Self => this;
}
```

------

### GDI_C011: ScopeInvalidAttribute

**消息**: `Scope '{0}' cannot use [{1}]`

**原因**: Scope 使用了不兼容的特性（如 `[Singleton]`、`[Host]`、`[User]`）。

------

### GDI_C020: HostMustBeNode

**消息**: `Host '{0}' must inherit from Godot.Node`

**原因**: 标记为 `[Host]` 的类不是 Node 子类。

**解决方案**: Host 必须继承自 Node。

```csharp
// ❌ 错误
[Host]
public partial class MyHost { }  // 不是 Node

// ✅ 正确
[Host]
public partial class MyHost : Node { }
```

------

### GDI_C021: UserMustBeNode

**消息**: `User '{0}' must inherit from Godot.Node`

**原因**: 标记为 `[User]` 的类不是 Node 子类。

------

### GDI_C022: ScopeMustBeNode

**消息**: `Scope '{0}' must inherit from Godot.Node`

**原因**: 实现 `IScope` 的类不是 Node 子类。

------

### GDI_C030: ServiceReadyNeedUser

**消息**: `Type '{0}' implements IServicesReady but is not marked with [User]`

**原因**: 实现了 `IServicesReady` 但未标记 `[User]`。

**解决方案**: 添加 `[User]` 标记。

```csharp
// ❌ 错误
public partial class MyComponent : Node, IServicesReady
{
    public void OnServicesReady() { }
}

// ✅ 正确
[User]
public partial class MyComponent : Node, IServicesReady
{
    public void OnServicesReady() { }
}
```

------

### GDI_C040: ScopeMissingModules

**消息**: `Scope '{0}' must specify either [Modules] or [AutoModules]`

**原因**: Scope 既没有 `[Modules]` 也没有 `[AutoModules]`。

------

### GDI_C050: DiClassMustBePartial

**消息**: `DI-relative class '{0}' must be declared as partial to enable code generation`

**原因**: DI 相关的类未声明为 `partial`。

**解决方案**: 添加 `partial` 修饰符。

```csharp
// ❌ 错误
[Singleton(typeof(IService))]
public class MyService : IService { }

// ✅ 正确
[Singleton(typeof(IService))]
public partial class MyService : IService { }
```

------

### GDI_C060: ServiceTypeIsInvalid

**消息**: `Service '{0}' cannot inherit from Godot.Node, and must be non-abstract, non-static class type`

**原因**: Service 继承了 Node，或者类型不符合要求（抽象类、静态类等）。

------

### GDI_C070: ServiceExposedTypeNotImplemented

**消息**: `Service '{0}' has exposed type '{1}', but which is not implemented`

**原因**: Service 暴露了自身类型未实现的接口或未继承的类型。

```csharp
// ❌ 错误
[Singleton(typeof(IService))]
public partial class MyService { }

// ✅ 正确
[Singleton(typeof(IService))]
public partial class MyService : IService { }
```

------

## Member 级别错误 (GDI_M)

### GDI_M010: MemberHasSingletonButNotInHost

**消息**: `Type '{0}' must be marked as [Host] to use [Singleton] on members`

**原因**: 非 Host 类的成员使用了 `[Singleton]`。

```csharp
// ❌ 错误
[User]
public partial class MyUser : Node
{
    [Singleton(typeof(IService))]  // User 不能用
    private IService _service;
}
```

------

### GDI_M011: MemberHasInjectButNotInUser

**消息**: `Type '{0}' must be marked as [User] to use [Inject] on members`

**原因**: 非 User 类的成员使用了 `[Inject]`。

------

### GDI_M012: MemberConflictWithSingletonAndInject

**消息**: `[Singleton] and [Inject] cannot be applied to the same member`

**原因**: 同一成员同时标记了 `[Singleton]` 和 `[Inject]`。

------

### GDI_M020: InjectMemberNotAssignable

**消息**: `[Inject] member must be writable (field must not be readonly, property must have setter)`

**原因**: 注入目标不可写。

```csharp
// ❌ 错误
[User]
public partial class MyUser : Node
{
    [Inject] private readonly IService _service;  // readonly
    [Inject] public IConfig Config { get; }       // 无 setter
}

// ✅ 正确
[User]
public partial class MyUser : Node
{
    [Inject] private IService _service;
    [Inject] public IConfig Config { get; set; }
}
```

------

### GDI_M030: SingletonPropertyNotAccessible

**消息**: `[Singleton] property must have a getter`

**原因**: Host 成员属性没有 getter。

------

### GDI_M040: InjectMemberInvalidType

**消息**: `Injected member in '{0}' has type '{1}', which is not a Service`

**原因**: 注入目标的类型不是有效的服务类型。

------

### GDI_M041: InjectMemberIsHostType

**消息**: `[Inject] member '{0}' has type '{1}', which is a [Host] type and cannot be injected`

**原因**: 试图注入 Host 类型。

```csharp
// ❌ 错误
[Host]
public partial class GameManager : Node { }

[User]
public partial class MyUser : Node
{
    [Inject] private GameManager _manager;  // Host 不可注入
}

// ✅ 正确：注入 Host 暴露的接口
[User]
public partial class MyUser : Node
{
    [Inject] private IGameState _state;  // 注入接口
}
```

------

### GDI_M042: InjectMemberIsUserType

**消息**: `[Inject] member '{0}' has type '{1}', which is a [User] type and cannot be injected`

**原因**: 试图注入 User 类型。

------

### GDI_M043: InjectMemberIsScopeType

**消息**: `[Inject] member '{0}' has type '{1}', which is an IScope type and cannot be injected`

**原因**: 试图注入 Scope 类型。

------

### GDI_M044: InjectMemberIsStatic

**消息**: `[Inject] member '{0}' cannot be static`

**原因**: 静态成员使用了 `[Inject]`。

```csharp
// ❌ 错误
[User]
public partial class MyUser : Node
{
    [Inject] private static IService _service;
}
```

------

### GDI_M045: SingletonMemberIsStatic

**消息**: `[Singleton] member '{0}' cannot be static`

**原因**: 静态成员使用了 `[Singleton]`。

------

### GDI_M050: HostSingletonMemberIsServiceType

**消息**: `[Singleton] member '{0}' has type '{1}', which is already marked as a Service. Host should not hold Service instances directly`

**原因**: Host 成员的类型是 Service（标记了 `[Singleton]` ）。

```csharp
// ❌ 错误
[Singleton(typeof(IConfig))]
public partial class ConfigService : IConfig { }

[Host]
public partial class BadHost : Node
{
    [Singleton(typeof(IConfig))]
    private ConfigService _config = new();  // 类型是 Service
}

// ✅ 正确：使用 Host+User 组合
[Host, User]
public partial class GoodHost : Node
{
    [Inject] private IConfig _config;  // 注入 Service
}
```

------

### GDI_M060: ExposedTypeShouldBeInterface (Warning)

**消息**: `Exposed type '{0}' is a concrete class. Consider using an interface instead for better testability and loose coupling`

**原因**: 暴露的服务类型是具体类而非接口。

**严重程度**: 警告（不阻止编译）

```csharp
// ⚠️ 警告
[Singleton(typeof(ConfigService))]  // 具体类
public partial class ConfigService { }

// ✅ 推荐
[Singleton(typeof(IConfig))]  // 接口
public partial class ConfigService : IConfig { }
```
------

### GDI_M070: HostMemberExposedTypeNotImplemented

**消息**: `Host member '{0}' has exposed type '{1}', but which is not implemented`

**原因**: Host 成员暴露了该成员类型未实现的接口或未继承的类型。

```csharp
// ❌ 错误
[Singleton(typeof(IService))]
public partial class MyHost
{
    [Singleton(typeof(IService))]
    private MyHost Self => this;
}

// ✅ 正确
[Singleton(typeof(IService))]
public partial class MyHost : IService
{
    [Singleton(typeof(IService))]
    private MyHost Self => this;
}
```

------

## Constructor 级别错误 (GDI_S)

### GDI_S010: NoPublicConstructor

**消息**: `Service '{0}' must define at least one constructor`

**原因**: Service 没有公共构造函数。

------

### GDI_S011: AmbiguousConstructor

**消息**: `Service '{0}' has multiple constructors but no [InjectConstructor] is specified`

**原因**: 多个构造函数但未指定使用哪个。

```csharp
// ❌ 错误
[Singleton(typeof(IService))]
public partial class MyService : IService
{
    public MyService(IDep1 d1) { }
    public MyService(IDep1 d1, IDep2 d2) { }  // 歧义
}

// ✅ 正确
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

**消息**: `Type '{0}' is not a Service but uses [InjectConstructor]`

**原因**: 非 Service 类型使用了 `[InjectConstructor]`。

------

### GDI_S020: InjectConstructorParameterTypeInvalid

**消息**: `Inject constructor parameter must be an interface type, or a non-Node, non-Host, non-User and non-Scope class type`

**原因**: 构造函数参数类型无效。

------

## Dependency Graph 错误 (GDI_D)

### GDI_D001: ScopeModulesServicesEmpty

**消息**: `Scope '{0}' must specify at least one type in [Modules] Services`

**原因**: `[Modules]` 的 `Services` 为空。

------

### GDI_D002: ScopeModulesHostsEmpty (Info)

**消息**: `Scope '{0}' specifies no Host type in [Modules] Hosts`

**严重程度**: 信息（提示）

------

### GDI_D003: ScopeModulesServiceMustBeService

**消息**: `Scope '{0}' Modules Service type '{1}' must be a Service`

**原因**: `Services` 中的类型不是 Service。

------

### GDI_D004: ScopeModulesHostMustBeHost

**消息**: `Scope '{0}' Modules Host type '{1}' must be a Host`

**原因**: `Hosts` 中的类型不是 Host。

------

### GDI_D010: CircularDependencyDetected

**消息**: `Circular dependency detected: {0}`

**原因**: 服务之间存在循环依赖。

```csharp
// ❌ 循环依赖
[Singleton(typeof(IA))]
public partial class A : IA { public A(IB b) { } }

[Singleton(typeof(IB))]
public partial class B : IB { public B(IA a) { } }
// 检测到：A -> B -> A
```

------

### GDI_D020: ServiceConstructorParameterInvalid

**消息**: `Service '{0}' has constructor parameter of type '{1}', which is not a Service`

**原因**: Service 构造函数参数的类型不是有效的服务类型。

------

### GDI_D040: ServiceTypeConflict

**消息**: `Service type '{0}' is registered by multiple providers: {1}. Each service type must have exactly one provider within a Scope`

**原因**: 同一服务类型有多个提供者。

```csharp
// ❌ 冲突
[Singleton(typeof(IService))]
public partial class ServiceA : IService { }

[Singleton(typeof(IService))]
public partial class ServiceB : IService { }

[Modules(Services = [typeof(ServiceA), typeof(ServiceB)])]
public partial class MyScope : Node, IScope { }
// 两个都提供 IService，冲突
```

------

## Internal Error (GDI_E)

### GDI_E900: RequestCancellation

**消息**: `Generator receives cancellation request: {0}`

**原因**: 源生成器执行被取消。

------

### GDI_E910: GeneratorInternalError

**消息**: `Internal error in source generator: {0}`

**原因**: 源生成器内部错误。

------

### GDI_E920: UnknownTypeRole

**消息**: `Unknown DI Type Role`

**原因**: 未知的 DI 角色分类。

------

### GDI_E930: ScopeLosesAttributeUnexpectedly

**消息**: `Scope '{0}' Unexpectedly loses [Modules] or [AutoModules]`

**原因**: Scope 意外丢失了 `[Modules]` 或 `[AutoModules]`。

------

## User Behavior 警告 (GDI_U)

### GDI_U001: ManualCallGeneratedMethod

**消息**: `Do not manually call generated method '{0}' on '{1}'. This method is managed by the DI framework and will be called automatically at the appropriate time`

**原因**: 手动调用了框架生成的方法（如 `AttachToScope`、`ResolveUserDependencies`、`RegisterService` 等）。

**注意**: 对于非 Node 的 User，需要调用 `ResolveDependencies`（公共方法），而非 `ResolveUserDependencies`（私有方法）。
