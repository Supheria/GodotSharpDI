# API 参考

## 特性（Attributes）

### SingletonAttribute

标记一个类为 Singleton 生命周期的服务，或标记 Host 成员为暴露的服务。

```csharp
namespace GodotSharp.DI.Abstractions;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false,
    AllowMultiple = false
)]
public sealed class SingletonAttribute : Attribute
{
    public Type[] ServiceTypes { get; }
    
    public SingletonAttribute(params Type[] serviceTypes);
}
```

#### 用法

**在类上（Service）**：

```csharp
// 暴露单个接口
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats { }

// 暴露多个接口
[Singleton(typeof(IReader), typeof(IWriter))]
public partial class FileService : IReader, IWriter { }

// 无参数时暴露类本身（不推荐）
[Singleton]
public partial class ConfigService { }
```

**在成员上（Host）**：

```csharp
[Host]
public partial class GameManager : Node, IGameState
{
    [Singleton(typeof(IGameState))]
    private IGameState Self => this;
}
```

------

### TransientAttribute

标记一个类为 Transient 生命周期的服务。

```csharp
namespace GodotSharp.DI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class TransientAttribute : Attribute
{
    public Type[] ServiceTypes { get; }
    
    public TransientAttribute(params Type[] serviceTypes);
}
```

#### 用法

```csharp
[Transient(typeof(IBullet))]
public partial class Bullet : IBullet { }
```

------

### HostAttribute

标记一个类为 Host（服务提供者）。

```csharp
namespace GodotSharp.DI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class HostAttribute : Attribute { }
```

#### 用法

```csharp
[Host]
public partial class ChunkManager : Node3D, IChunkGetter
{
    [Singleton(typeof(IChunkGetter))]
    private IChunkGetter Self => this;
}
```

------

### UserAttribute

标记一个类为 User（服务消费者）。

```csharp
namespace GodotSharp.DI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class UserAttribute : Attribute { }
```

#### 用法

```csharp
[User]
public partial class PlayerUI : Control
{
    [Inject] private IPlayerStats _stats;
}
```

------

### InjectAttribute

标记一个字段或属性为注入目标。

```csharp
namespace GodotSharp.DI.Abstractions;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class InjectAttribute : Attribute { }
```

#### 用法

```csharp
[User]
public partial class MyComponent : Node
{
    [Inject] private IService _service;           // 字段
    [Inject] public IConfig Config { get; set; }  // 属性（需要 setter）
}
```

------

### InjectConstructorAttribute

指定 Service 使用的构造函数。

```csharp
namespace GodotSharp.DI.Abstractions;

[AttributeUsage(AttributeTargets.Constructor)]
public sealed class InjectConstructorAttribute : Attribute { }
```

#### 用法

```csharp
[Singleton(typeof(IService))]
public partial class MyService : IService
{
    [InjectConstructor]
    public MyService(IDep1 dep1) { }
    
    public MyService(IDep1 dep1, IDep2 dep2) { }
}
```

------

### ModulesAttribute

声明 Scope 管理的服务和期望的 Host。

```csharp
namespace GodotSharp.DI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ModulesAttribute : Attribute
{
    public Type[] Services { get; set; }
    public Type[] Hosts { get; set; }
}
```

#### 参数

| 参数       | 说明                                |
| ---------- | ----------------------------------- |
| `Services` | Scope 创建和管理的 Service 类型列表 |
| `Hosts`    | Scope 期望接收的 Host 类型列表      |

#### 用法

```csharp
[Modules(
    Services = [typeof(PlayerStatsService), typeof(CombatSystem)],
    Hosts = [typeof(GameManager), typeof(WorldManager)]
)]
public partial class GameScope : Node, IScope { }
```

------

## 接口（Interfaces）

### IScope

DI 容器接口。

```csharp
namespace GodotSharp.DI.Abstractions;

public interface IScope
{
    void RegisterService<T>(T instance) where T : notnull;
    void UnregisterService<T>() where T : notnull;
    void ResolveDependency<T>(Action<T> onResolved) where T : notnull;
}
```

#### 方法

**RegisterService<T>**

```csharp
void RegisterService<T>(T instance) where T : notnull;
```

注册服务实例。通常由框架自动调用，不建议手动调用。

**UnregisterService<T>**

```csharp
void UnregisterService<T>() where T : notnull;
```

注销服务。通常由框架在 Host 退出场景树时自动调用。

**ResolveDependency<T>**

```csharp
void ResolveDependency<T>(Action<T> onResolved) where T : notnull;
```

解析依赖。如果服务已注册，立即回调；否则加入等待队列。

#### 用法

```csharp
// 通常不需要直接使用 IScope
// 框架会自动处理依赖解析

// 如果需要动态解析依赖
scope.ResolveDependency<IService>(service =>
{
    // 使用服务
    service.DoSomething();
});
```

------

### IServicesReady

服务就绪通知接口。

```csharp
namespace GodotSharp.DI.Abstractions;

public interface IServicesReady
{
    void OnServicesReady();
}
```

#### 用法

```csharp
[User]
public partial class MyComponent : Node, IServicesReady
{
    [Inject] private IServiceA _a;
    [Inject] private IServiceB _b;
    
    public void OnServicesReady()
    {
        // 所有依赖已注入，安全使用
        _a.Initialize();
        _b.Connect(_a);
    }
}
```

------

## 生成的代码

### Node User 生成的方法

对于标记为 `[User]` 的 Node 类型，框架生成：

```csharp
// 服务 Scope 引用
private IScope? _serviceScope;

// 获取最近的 Scope
private IScope? GetServiceScope();

// 附加到 Scope（注入依赖）
private void AttachToScope();

// 从 Scope 分离（Host 用）
private void UnattachToScope();

// 生命周期通知处理
public override void _Notification(int what);

// 解析用户依赖
private void ResolveUserDependencies(IScope scope);
```

### Host 生成的方法

对于标记为 `[Host]` 的类型，框架生成：

```csharp
// 注册 Host 服务到 Scope
private void AttachHostServices(IScope scope);

// 从 Scope 注销 Host 服务
private void UnattachHostServices(IScope scope);
```

### Service 生成的方法

对于标记为 `[Singleton]` 或 `[Transient]` 的服务，框架生成工厂方法：

```csharp
// 创建服务实例
public static void CreateService(
    IScope scope,
    Action<object, IScope> onCreated  // Singleton
    // 或
    Action<object> onCreated          // Transient
);
```

### Scope 生成的方法

对于实现 `IScope` 的类型，框架生成完整的容器实现：

```csharp
// 静态集合
private static readonly HashSet<Type> SingletonServiceTypes;
private static readonly Dictionary<Type, Action<IScope, Action<object>>> TransientFactories;

// 实例字段
private readonly Dictionary<Type, object> _singletonServices;
private readonly HashSet<object> _scopeSingletonInstances;
private readonly Dictionary<Type, List<Action<object>>> _waiters;
private IScope? _parentScope;

// 生命周期方法
private IScope? GetParentScope();
private void InstantiateScopeSingletons();
private void DisposeScopeSingletons();
private void CheckWaitList();
public override void _Notification(int what);

// IScope 实现
void IScope.ResolveDependency<T>(Action<T> onResolved);
void IScope.RegisterService<T>(T instance);
void IScope.UnregisterService<T>();
```

------

## 场景树集成

### 生命周期事件

框架监听以下 Godot 通知：

| 通知                    | 处理                                                         |
| ----------------------- | ------------------------------------------------------------ |
| `NotificationEnterTree` | User: 附加到 Scope，触发注入<br>Host: 注册服务<br>Scope: 清除父 Scope 缓存 |
| `NotificationExitTree`  | User: 清除 Scope 引用<br>Host: 注销服务<br>Scope: 清除父 Scope 缓存 |
| `NotificationReady`     | Scope: 创建 Singleton，检查等待队列                          |
| `NotificationPredelete` | Scope: 释放所有服务                                          |

### 场景树查找

获取 Scope 的逻辑：

```csharp
private IScope? GetServiceScope()
{
    if (_serviceScope is not null)
        return _serviceScope;
    
    var parent = GetParent();
    while (parent is not null)
    {
        if (parent is IScope scope)
        {
            _serviceScope = scope;
            return _serviceScope;
        }
        parent = parent.GetParent();
    }
    
    GD.PushError("没有找到最近的 Service Scope");
    return null;
}
```
