# 角色详解

GodotSharp.DI 定义了四种角色类型，每种角色有明确的职责边界和约束规则。

## Service（服务）

### 职责

Service 是纯逻辑服务，封装业务逻辑和数据处理，不依赖 Godot Node 系统。

### 约束

| 约束项 | 要求 | 原因 |
|--------|------|------|
| 类型 | 必须是 class | 需要实例化 |
| 继承 | 不能是 Node | Node 生命周期由 Godot 控制，与 DI 容器冲突 |
| 修饰符 | 不能是 abstract 或 static | 需要实例化 |
| 泛型 | 不能是开放泛型 | 需要具体类型来实例化 |
| 声明 | 必须是 partial | 源生成器需要扩展类 |

### 生命周期标记

```csharp
// Singleton：Scope 内唯一实例
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats { }

// Transient：每次请求创建新实例
[Transient(typeof(IWeaponFactory))]
public partial class WeaponFactory : IWeaponFactory { }
```

### 构造函数注入

Service 支持通过构造函数注入依赖：

```csharp
[Singleton(typeof(ICombatSystem))]
public partial class CombatSystem : ICombatSystem
{
    private readonly IPlayerStats _stats;
    private readonly IWeaponFactory _weapons;
    
    public CombatSystem(IPlayerStats stats, IWeaponFactory weapons)
    {
        _stats = stats;
        _weapons = weapons;
    }
}
```

#### 构造函数选择规则

1. 如果有 `[InjectConstructor]` 标记的构造函数，使用它
2. 否则，选择参数最少的 public 构造函数
3. 如果有多个相同参数数量的构造函数，必须用 `[InjectConstructor]` 指定

```csharp
[Singleton(typeof(IService))]
public partial class MyService : IService
{
    // 多个构造函数时，必须指定
    [InjectConstructor]
    public MyService(IDep1 dep1) { }
    
    public MyService(IDep1 dep1, IDep2 dep2) { }
}
```

### 暴露类型

Service 通过特性参数指定暴露的服务类型：

```csharp
// 暴露单个接口
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats { }

// 暴露多个接口
[Singleton(typeof(IReader), typeof(IWriter))]
public partial class FileService : IReader, IWriter { }

// 不指定参数时，暴露类本身（不推荐）
[Singleton]  // 暴露 ConfigService 类型
public partial class ConfigService { }
```

> ⚠️ **最佳实践**：始终暴露接口而非具体类，以保持松耦合和可测试性。

---

## Host（宿主）

### 职责

Host 是 Godot Node 世界与 DI 世界的**桥接器（Adapter）**。它将 Node 管理的资源暴露为可注入的服务。

### 约束

| 约束项 | 要求 | 原因 |
|--------|------|------|
| 类型 | 必须是 class | 需要实例化 |
| 继承 | 必须是 Node | 需要接入场景树生命周期 |
| 声明 | 必须是 partial | 源生成器需要扩展类 |

### 典型使用模式

#### 模式 1：Host 暴露自身（最常见）

```csharp
[Host]
public partial class ChunkManager : Node3D, IChunkGetter, IChunkLoader
{
    [Singleton(typeof(IChunkGetter), typeof(IChunkLoader))]
    private IChunkGetter Self => this;
    
    // Node 管理的资源
    private Dictionary<Vector3I, Chunk> _chunks = new();
    
    // 实现接口
    public Chunk GetChunk(Vector3I pos) => _chunks.GetValueOrDefault(pos);
    public void LoadChunk(Vector3I pos) { /* ... */ }
}
```

这是 Host 最主要的使用方式：Node 自身实现服务接口，并将自己暴露给 DI 系统。

#### 模式 2：Host 持有并暴露其他对象

```csharp
[Host]
public partial class WorldManager : Node
{
    [Singleton(typeof(IWorldConfig))]
    private WorldConfig _config = new();
    
    [Singleton(typeof(IWorldState))]
    private WorldState _state = new();
}

// 这些类不是 Service，只是普通类
public class WorldConfig : IWorldConfig { /* ... */ }
public class WorldState : IWorldState { /* ... */ }
```

Host 可以持有和管理其他对象，并将它们暴露为服务。这些对象的生命周期由 Host 控制。

### Host 成员约束

| 约束项 | 要求 | 原因 |
|--------|------|------|
| 成员类型 | 不能是已标记为 Service 的类型 | 避免生命周期冲突 |
| static 成员 | 不允许 | 需要实例级别的服务 |
| 属性 | 必须有 getter | 需要读取值来注册服务 |

```csharp
// ❌ 错误：成员类型是 Service
[Singleton(typeof(IConfig))]
public partial class ConfigService : IConfig { }

[Host]
public partial class BadHost : Node
{
    [Singleton(typeof(IConfig))]
    private ConfigService _config = new();  // 编译错误 GDI_M060
}

// ✅ 正确：使用注入而非持有
[Host, User]
public partial class GoodHost : Node
{
    [Singleton(typeof(ISelf))]
    private ISelf Self => this;
    
    [Inject]
    private IConfig _config;  // 通过注入获取 Service
}
```

---

## User（消费者）

### 职责

User 是依赖消费者，通过字段或属性注入接收服务依赖。

### 约束

| 约束项 | 要求 | 原因 |
|--------|------|------|
| 类型 | 必须是 class | 需要实例化 |
| 继承 | Node 或普通 class 均可 | 灵活支持两种场景 |
| 声明 | 必须是 partial | 源生成器需要扩展类 |

### Node User（自动注入）

```csharp
[User]
public partial class PlayerController : CharacterBody3D, IServicesReady
{
    [Inject] private IPlayerStats _stats;
    [Inject] private ICombatSystem _combat;
    
    // 当所有依赖注入完成后自动调用
    public void OnServicesReady()
    {
        GD.Print("所有服务已就绪，可以开始游戏逻辑");
    }
}
```

Node 类型的 User 会在进入场景树时自动触发注入，无需手动操作。

### 非 Node User（手动注入）

```csharp
[User]
public partial class GameLogic  // 非 Node
{
    [Inject] private IPlayerStats _stats;
    
    // 生成的方法
    // public void ResolveDependencies(IScope scope)
}

// 使用时需要手动调用
var logic = new GameLogic();
logic.ResolveDependencies(scope);  // 需要提供 Scope 引用
```

> ⚠️ **注意**：非 Node User 需要调用者提供 Scope 引用。这通常意味着：
> 1. 调用者是 Node，可以通过场景树找到 Scope
> 2. 或者通过其他方式获得 Scope 引用

### Inject 成员约束

| 约束项 | 要求 | 原因 |
|--------|------|------|
| 成员类型 | interface 或普通 class | 必须是可注入类型 |
| 成员类型 | 不能是 Node/Host/User/Scope | 这些不是服务类型 |
| static 成员 | 不允许 | 需要实例级别的注入 |
| 属性 | 必须有 setter | 需要写入注入值 |

```csharp
[User]
public partial class MyUser : Node
{
    [Inject] private IService _service;           // ✅ 正确
    [Inject] private MyConcreteClass _concrete;   // ✅ 允许但不推荐
    [Inject] private Node _node;                  // ❌ 错误
    [Inject] private MyHost _host;                // ❌ 错误
    [Inject] private static IService _static;    // ❌ 错误
}
```

### IServicesReady 接口

实现 `IServicesReady` 接口可以在所有依赖注入完成后收到通知：

```csharp
[User]
public partial class MyComponent : Node, IServicesReady
{
    [Inject] private IServiceA _a;
    [Inject] private IServiceB _b;
    [Inject] private IServiceC _c;
    
    // 当 _a, _b, _c 都注入完成后调用
    public void OnServicesReady()
    {
        // 安全地使用所有依赖
        Initialize();
    }
}
```

---

## Scope（容器）

### 职责

Scope 是 DI 容器，负责：
1. 创建和管理 Service 实例
2. 处理依赖解析请求
3. 管理服务生命周期

### 约束

| 约束项 | 要求 | 原因 |
|--------|------|------|
| 类型 | 必须是 class | 需要实例化 |
| 继承 | 必须是 Node | 利用场景树实现 Scope 层级 |
| 接口 | 必须实现 IScope | 框架识别标志 |
| 特性 | 必须有 [Modules] 或 [AutoModules] | 声明管理的服务 |
| 声明 | 必须是 partial | 源生成器需要扩展类 |

### 定义 Scope

```csharp
[Modules(
    Services = [typeof(PlayerStatsService), typeof(CombatSystem)],
    Hosts = [typeof(GameManager), typeof(WorldManager)]
)]
public partial class GameScope : Node, IScope
{
    // 框架自动生成所有 IScope 实现
}
```

#### Modules 参数说明

| 参数 | 说明 | 约束 |
|------|------|------|
| `Services` | Scope 创建和管理的 Service 类型列表 | 必须是 Service（有 [Singleton] 或 [Transient]） |
| `Hosts` | Scope 期望接收的 Host 类型列表 | 必须是 Host（有 [Host]） |

### Scope 层级

Scope 通过场景树结构形成层级关系：

```
RootScope
├── GameManager (Host)
├── GlobalServices...
│
└── LevelScope
    ├── LevelManager (Host)
    ├── LevelServices...
    │
    └── Player
        └── PlayerUI (User)
```

依赖解析规则：
1. 首先在当前 Scope 查找
2. 如果未找到且服务类型不属于当前 Scope，向父 Scope 查找
3. 递归直到根 Scope 或找到服务

### Scope 生命周期事件

| 事件 | 触发时机 | 行为 |
|------|----------|------|
| `NotificationReady` | Node 准备就绪 | 创建所有 Singleton Service |
| `NotificationPredelete` | Node 即将删除 | 释放所有 Service（调用 IDisposable.Dispose） |

---

## Host + User 组合

一个 Node 可以同时是 Host 和 User：

```csharp
[Host, User]
public partial class GameManager : Node, IGameState, IServicesReady
{
    // Host 部分：暴露服务
    [Singleton(typeof(IGameState))]
    private IGameState Self => this;
    
    // User 部分：注入依赖
    [Inject] private IConfig _config;
    [Inject] private ISaveSystem _saveSystem;
    
    public void OnServicesReady()
    {
        // 依赖已就绪，可以初始化
        LoadLastSave();
    }
}
```

这在需要同时提供服务和消费服务的 Node 上非常有用。
