# 生命周期管理

GodotSharp.DI 的生命周期管理与 Godot 的场景树紧密集成。

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

### Node User 的注入时序

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

GodotSharp.DI 使用回调模式而非同步返回：

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
