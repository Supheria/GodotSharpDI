# 生命周期管理

GodotSharpDI 的生命周期管理与 Godot 的场景树紧密集成。

## 服务生命周期

### Singleton（单例）

Singleton 服务在所属 Scope 内唯一，随 Scope 创建和销毁。

```csharp
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats, IDisposable
{
    public int Health { get; set; } = 100;
    
    public void Dispose()
    {
        GD.Print("PlayerStatsService 被释放");
    }
}
```

#### 生命周期时序

```
Scope 进入场景树 (NotificationEnterTree)
    ↓
Scope 就绪 (NotificationReady)
    ↓ 创建 Singleton 实例
    ↓ 注册到服务容器
    ↓ 通知等待的消费者
    ↓
... 服务运行中 ...
    ↓
Scope 即将删除 (NotificationPredelete)
    ↓ 调用 IDisposable.Dispose()（如果实现）
    ↓ 从容器移除
    ↓
Scope 删除完成
```

#### 释放单例

1. 遍历 _disposableSingletons 中管理的 IDisposable 单例
2. 调用 `Dispose()` 方法
3. 捕获并记录任何异常，继续释放其他服务
4. 清空存储列表

```csharp
// 生成的代码
private void DisposeScopeSingletons()
{
    foreach (var disposable in _disposableSingletons)
    {
        try
        {
            disposable.Dispose();
        }
        catch (Exception ex)
        {
            GD.PushError(ex);
        }
    }
    _disposableSingletons.Clear();
    _singletonServices.Clear();
}
```

---

## Scope 层级

### 层级结构

Scope 通过 Godot 场景树形成自然的层级结构：

```
Application
└── RootScope                    ← 根 Scope
    ├── GlobalServices           ← 全局服务
    │   ├── IConfigService
    │   └── ISaveService
    │
    └── LevelScene
        └── LevelScope           ← 子 Scope
            ├── LevelServices    ← 关卡服务
            │   ├── IEnemySpawner
            │   └── ILootSystem
            │
            └── Player
                └── PlayerScope  ← 更深层 Scope
                    └── PlayerServices
                        └── IInventory
```

### 服务解析规则

当 User 请求依赖时：

1. **检查当前 Scope**
   - 如果服务类型在当前 Scope 的 `SingletonServiceTypes` 中，在当前 Scope 解析
   - 如果已注册，立即返回
   - 如果未注册，加入等待队列

2. **向上委托**
   - 如果服务类型不属于当前 Scope，向父 Scope 递归查找
   - 直到根 Scope 或找到服务

3. **错误处理**
   - 如果直到根 Scope 都未找到，记录错误

```csharp
// 简化的解析逻辑
void ResolveDependency<T>(Action<T> onResolved)
{
    var type = typeof(T);
    
    // 尝试 Transient 工厂
    if (TransientFactories.TryGetValue(type, out var factory))
    {
        factory.Invoke(this, instance => onResolved((T)instance));
        return;
    }
    
    // 检查是否属于当前 Scope
    if (!SingletonServiceTypes.Contains(type))
    {
        // 委托给父 Scope
        GetParentScope()?.ResolveDependency(onResolved);
        return;
    }
    
    // 尝试已注册的服务
    if (_singletonServices.TryGetValue(type, out var singleton))
    {
        onResolved((T)singleton);
        return;
    }
    
    // 加入等待队列
    AddToWaitList(type, onResolved);
}
```

### 服务可见性

| 服务位置 | 可见范围 |
|----------|----------|
| 根 Scope | 所有子 Scope |
| 父 Scope | 所有子孙 Scope |
| 子 Scope | 仅该 Scope 及其子孙 |

```
RootScope
├── IGlobalConfig       ← 对所有 Scope 可见
│
└── LevelScope
    ├── ILevelConfig    ← 对 LevelScope 及其子 Scope 可见
    │                      对 RootScope 不可见
    │
    └── PlayerScope
        └── IInventory  ← 仅对 PlayerScope 可见
```

### Scope 生命周期事件

#### 进入场景树

```csharp
case NotificationEnterTree:
case NotificationExitTree:
    _parentScope = null;  // 清除缓存，下次访问时重新查找
    break;
```

清除父 Scope 缓存的原因：
- 场景树结构可能改变（reparent）
- 确保下次查找时获取正确的父 Scope

#### 准备就绪

```csharp
case NotificationReady:
    InstantiateScopeSingletons();  // 创建所有 Singleton
    CheckWaitList();               // 检查未完成的依赖
    break;
```

#### 即将删除

```csharp
case NotificationPredelete:
    DisposeScopeSingletons();  // 释放所有服务
    break;
```

---

## 依赖注入时序

### User 的注入时序

```
User Node 进入场景树 (NotificationEnterTree)
    ↓
AttachToScope()
    ↓
GetServiceScope() ← 向上查找最近的 IScope
    ↓
ResolveUserDependencies(scope)
    ↓
scope.ResolveDependency<T>(callback) ← 每个 [Inject] 成员
    ↓
[等待服务就绪或立即回调]
    ↓
OnServicesReady() ← 所有依赖注入完成（如果实现 IServicesReady）
```

### Host 的服务注册时序

```
Host Node 进入场景树 (NotificationEnterTree)
    ↓
AttachToScope()
    ↓
GetServiceScope() ← 向上查找最近的 IScope
    ↓
AttachHostServices(scope)
    ↓
scope.RegisterService<T>(this.Member) ← 每个 [Singleton] 成员
    ↓
[通知等待该服务的 User]
```

### 完整时序示例

假设场景结构：
```
GameScope
├── GameManager (Host, 提供 IGameState)
└── PlayerUI (User, 需要 IGameState)
```

执行时序：

```
1. GameScope 进入场景树
2. GameScope._Notification(EnterTree)
   
3. GameManager 进入场景树
4. GameManager._Notification(EnterTree)
   → GetServiceScope() 找到 GameScope
   → AttachHostServices(GameScope)
   → GameScope.RegisterService<IGameState>(this)
   → [此时 PlayerUI 可能还未进入，加入等待队列为空]

5. PlayerUI 进入场景树
6. PlayerUI._Notification(EnterTree)
   → GetServiceScope() 找到 GameScope
   → ResolveUserDependencies(GameScope)
   → GameScope.ResolveDependency<IGameState>(callback)
   → [IGameState 已注册，立即回调]
   → _gameState = injectedValue
   → OnServicesReady() [如果实现]

7. GameScope._Notification(Ready)
   → InstantiateScopeSingletons() [创建 Service]
   → CheckWaitList() [检查未完成的依赖]
```

---

## 异步回调模式

### 设计原因

GodotSharpDI 使用回调模式而非同步返回：

```csharp
// 回调模式
void ResolveDependency<T>(Action<T> onResolved);

// 而非同步模式
T GetService<T>();
```

**原因**：

1. **支持延迟解析**：服务可能在 User 请求时尚未注册
2. **非阻塞**：不会因等待服务而阻塞主线程
3. **与 Godot 契合**：Godot 是单线程、帧驱动的

### 等待队列机制

当请求的服务尚未注册时：

```csharp
// 1. 加入等待队列
if (!_waiters.TryGetValue(type, out var waiterList))
{
    waiterList = new List<Action<object>>();
    _waiters[type] = waiterList;
}
waiterList.Add(obj => onResolved((T)obj));

// 2. 服务注册时通知
void RegisterService<T>(T instance)
{
    _singletonServices[type] = instance;
    
    if (_waiters.Remove(type, out var waiterList))
    {
        foreach (var callback in waiterList)
        {
            callback(instance);
        }
    }
}
```

### 未完成依赖检测

在 Scope 就绪时，检查是否有未解析的依赖：

```csharp
private void CheckWaitList()
{
    if (_waiters.Count == 0) return;
    
    // 报告未完成的依赖
    var types = string.Join(", ", _waiters.Keys.Select(t => t.Name));
    GD.PushError($"存在未完成注入的服务类型：{types}");
    _waiters.Clear();
}
```

这有助于在开发阶段发现配置错误。

---

## Host + User 与循环依赖

在 GodotSharpDI 中，一个类型可以同时标记为 `[Host, User]`，即既提供服务又消费服务。 为了避免误判循环依赖，需要明确 Host 与 User 在生命周期和注入时序上的区别。

### Host 与 User 的注入时序差异

**Host（服务提供者）**

- 在 **EnterTree** 阶段注册其 `[Singleton]` 成员提供的服务
- 注册服务时 **不会触发任何依赖注入**
- 不会触发自身的 User 注入
- 不会触发其他 User 的注入

**User（服务消费者）**

- 在 **EnterTree** 阶段附着到最近的 Scope
- 立即对所有 `[Inject]` 成员发起依赖解析
- 如果服务尚未注册，则加入等待队列
- 在服务注册或 Scope Ready 时被回调注入
- 所有依赖注入完成后触发 `OnServicesReady()`

**结论**

> **Host 的服务注册阶段不参与依赖注入链。** **User 的依赖注入只在 Node 进入场景树后、或服务注册完成后触发。**

这条规则保证了 Host+User 不会因为“自提供、自消费”而形成循环依赖。

### 示例 1：Host+User 自注入不是循环依赖

```csharp
public interface IMyService { }

[Host, User]
public partial class MyService : Node, IMyService
{
    [Singleton(typeof(IMyService))]
    private MyService Self => this;

    [Inject]
    private IMyService _self;
}
```

**依赖关系**

- Host 部分提供 `IMyService`
- User 部分消费 `IMyService`

**为什么不是循环依赖？**

1. Host 注册 `Self` 时 **不会触发** `_self` **的注入**
2. `_self` 的注入发生在 User 注入阶段（EnterTree → AttachToScope）
3. 此时 `IMyService` 已经注册，因此注入成功
4. 整个过程没有构造函数链路，也没有形成依赖闭环

**结论**

> **Host+User 自注入是合法的，不属于循环依赖。**

### 示例 2：Host 提供服务 + 自身消费另一个 Service 也不是循环依赖

```csharp
public interface IServiceA { }
public interface IServiceB { }

[Singleton(typeof(IServiceA))]
public partial class ServiceA : IServiceA
{
    public ServiceA(IServiceB b) { }
}

[Host, User]
public partial class HostUser : Node, IServiceB
{
    [Singleton(typeof(IServiceB))]
    private HostUser Self => this;

    [Inject]
    private IServiceA _serviceA;
}
```

**依赖关系**

- `HostUser`（Host）提供 `IServiceB`
- `ServiceA` 构造函数依赖 `IServiceB` → 注入 `HostUser`
- `HostUser`（User）依赖 `IServiceA`

**为什么不是循环依赖？**

1. HostUser 注册 `IServiceB` 时 **不会触发** `_serviceA` **的注入**
2. ServiceA 构造函数解析 `IServiceB` → 得到 HostUser
3. ServiceA 构造完成后，HostUser 的 `_serviceA` 在 User 注入阶段被赋值
4. 整个链路中没有构造函数环路

**依赖图如下：**

```
ServiceA → IServiceB (HostUser)
HostUser(User) → IServiceA
```

这是一个“菱形依赖”，不是循环。

#### 结论

> **Host 提供服务 + 自身作为 User 消费其他服务是合法的，不属于循环依赖。**

### 循环依赖检测的适用范围

GodotSharpDI 的循环依赖检测仅针对：

- **Service → Service 的构造函数依赖链**

不包括：

- User 的 `[Inject]` 成员
- Host 的 `[Singleton]` 成员
- Host+User 的自注入
- Host 与 User 之间的交叉依赖

原因：

> **User 注入发生在所有 Service 构造完成之后，不参与构造时的依赖闭环。**

因此，只有以下情况会被判定为循环依赖：

```csharp
[Singleton(typeof(IA))]
class A : IA { public A(IB b) {} }

[Singleton(typeof(IB))]
class B : IB { public B(IA a) {} }
```

### 5. 总结

| 情况                               | 是否循环依赖 | 原因                                 |
| ---------------------------------- | ------------ | ------------------------------------ |
| Host+User 自注入                   | ❌            | Host 注册不触发注入，User 注入在之后 |
| Host 提供服务 + 自身作为 User 注入 | ❌            | 注入时序分离，不形成构造函数环       |
| Service ↔ Service 构造函数互相依赖 | ✔️            | 构造函数闭环                         |

最终规则：

> **只要依赖链不在 Service 构造函数之间形成闭环，就不是循环依赖。Host+User 的注入时序天然避免构造函数循环。**

------

## 最佳实践

### 1. Scope 粒度设计

```csharp
// ✅ 好的设计：按功能/生命周期划分 Scope
RootScope          // 全局服务
├── MainMenuScope  // 主菜单服务
└── GameScope      // 游戏服务
    └── LevelScope // 关卡服务

// ❌ 避免：过多或过少的 Scope
// 过多：每个 Node 一个 Scope（过度设计）
// 过少：整个游戏一个 Scope（无法隔离）
```

### 2. 服务释放

```csharp
// ✅ 实现 IDisposable 进行清理
[Singleton(typeof(IResourceLoader))]
public partial class ResourceLoader : IResourceLoader, IDisposable
{
    private List<Resource> _loadedResources = new();
    
    public void Dispose()
    {
        foreach (var res in _loadedResources)
        {
            res.Free();  // 释放 Godot 资源
        }
        _loadedResources.Clear();
    }
}
```

### 3. 避免循环依赖

```csharp
// ❌ 循环依赖
[Singleton(typeof(IA))]
public partial class A : IA
{
    public A(IB b) { }  // A 依赖 B
}

[Singleton(typeof(IB))]
public partial class B : IB
{
    public B(IA a) { }  // B 依赖 A → 循环！
}

// ✅ 打破循环：使用事件或回调
[Singleton(typeof(IA))]
public partial class A : IA
{
    public event Action<int> OnValueChanged;
}

[Singleton(typeof(IB))]
public partial class B : IB
{
    public B(IA a)
    {
        a.OnValueChanged += HandleValueChanged;
    }
}
```
