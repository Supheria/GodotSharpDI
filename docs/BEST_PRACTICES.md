# 最佳实践

本文档总结了使用 GodotSharp.DI 的推荐模式和常见陷阱。

## 接口设计

### ✅ 始终通过接口暴露服务

```csharp
// ✅ 推荐：暴露接口
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats { }

// ⚠️ 允许但不推荐：暴露具体类
[Singleton(typeof(PlayerStatsService))]
public partial class PlayerStatsService { }
// 会产生 Warning: GDI_M070
```

**原因**：

- **依赖倒置原则**：高层模块不应依赖低层模块的实现
- **可测试性**：接口易于 Mock
- **可替换性**：可以轻松替换实现
- **松耦合**：减少组件间的直接依赖

### ✅ 接口职责单一

```csharp
// ✅ 好的设计：职责单一
public interface IChunkLoader
{
    void LoadChunk(Vector3I position);
    void UnloadChunk(Vector3I position);
}

public interface IChunkGetter
{
    Chunk? GetChunk(Vector3I position);
    bool HasChunk(Vector3I position);
}

// 一个 Host 可以实现多个接口
[Host]
public partial class ChunkManager : Node3D, IChunkLoader, IChunkGetter
{
    [Singleton(typeof(IChunkLoader), typeof(IChunkGetter))]
    private IChunkLoader Self => this;
}
// ❌ 避免：接口过于庞大
public interface IChunkSystem
{
    void LoadChunk(Vector3I position);
    void UnloadChunk(Vector3I position);
    Chunk? GetChunk(Vector3I position);
    bool HasChunk(Vector3I position);
    void GenerateChunk(Vector3I position);
    void SaveChunk(Vector3I position);
    // ... 更多方法
}
```

------

## Scope 设计

### ✅ 按功能和生命周期划分 Scope

```csharp
// 游戏结构示例
[Modules(Services = [typeof(ConfigService), typeof(SaveService)])]
public partial class RootScope : Node, IScope { }
    // 全局服务，整个应用生命周期

[Modules(
    Services = [typeof(EnemySpawner), typeof(LootSystem)],
    Hosts = [typeof(LevelManager)]
)]
public partial class LevelScope : Node, IScope { }
    // 关卡服务，随关卡加载/卸载

[Modules(
    Services = [typeof(InventoryService)],
    Hosts = [typeof(PlayerController)]
)]
public partial class PlayerScope : Node, IScope { }
    // 玩家服务，随玩家实例化/销毁
```

### ❌ 避免过度或不足的 Scope 划分

```csharp
// ❌ 过度划分：每个组件一个 Scope
public partial class ButtonScope : Node, IScope { }  // 没必要
public partial class LabelScope : Node, IScope { }   // 没必要

// ❌ 划分不足：整个游戏一个 Scope
[Modules(Services = [
    typeof(ConfigService),
    typeof(SaveService),
    typeof(EnemySpawner),
    typeof(LootSystem),
    typeof(InventoryService),
    // ... 所有服务都在一起
])]
public partial class GameScope : Node, IScope { }
// 问题：关卡服务在主菜单也存在，无法按需加载
```

### ✅ Scope 层级的最佳结构

```
Application
└── RootScope                 ← 全局配置、存档、音频
    ├── MainMenuScope         ← 主菜单特定服务
    │
    └── GameScope             ← 游戏核心服务
        ├── WorldScope        ← 世界/地图服务
        │   └── RegionScope   ← 区域特定服务
        │
        └── UIScope           ← UI 服务
```

------

## 依赖设计

### ✅ 构造函数注入优于字段注入

```csharp
// ✅ 推荐：构造函数注入（Service）
[Singleton(typeof(ICombatSystem))]
public partial class CombatSystem : ICombatSystem
{
    private readonly IPlayerStats _stats;
    private readonly IWeaponRegistry _weapons;
    
    public CombatSystem(IPlayerStats stats, IWeaponRegistry weapons)
    {
        _stats = stats;
        _weapons = weapons;
    }
}
// 优点：依赖明确、不可变、易于测试

// ✅ 合理：字段注入（User/Node）
[User]
public partial class PlayerUI : Control
{
    [Inject] private IPlayerStats _stats;  // Node 只能用字段注入
}
```

### ✅ 依赖数量适中

```csharp
// ✅ 好的设计：3-5 个依赖
[Singleton(typeof(IGameManager))]
public partial class GameManager : IGameManager
{
    public GameManager(
        IConfigService config,
        ISaveService save,
        IAudioService audio
    ) { }
}

// ⚠️ 警告信号：过多依赖（> 7 个）
[Singleton(typeof(IGodClass))]
public partial class GodClass : IGodClass
{
    public GodClass(
        IDep1 d1, IDep2 d2, IDep3 d3, IDep4 d4,
        IDep5 d5, IDep6 d6, IDep7 d7, IDep8 d8
    ) { }
    // 考虑拆分成多个服务
}
```

### ❌ 避免循环依赖

```csharp
// ❌ 循环依赖（编译时检测 GDI_D020）
[Singleton(typeof(IA))]
public partial class A : IA { public A(IB b) { } }

[Singleton(typeof(IB))]
public partial class B : IB { public B(IA a) { } }

// ✅ 解决方案 1：引入中间服务
[Singleton(typeof(IMediator))]
public partial class Mediator : IMediator { }

[Singleton(typeof(IA))]
public partial class A : IA { public A(IMediator m) { } }

[Singleton(typeof(IB))]
public partial class B : IB { public B(IMediator m) { } }

// ✅ 解决方案 2：使用事件/回调
[Singleton(typeof(IA))]
public partial class A : IA
{
    public event Action<int> OnValueChanged;
}

[Singleton(typeof(IB))]
public partial class B : IB
{
    public B(IA a) { a.OnValueChanged += Handle; }
}

// ✅ 解决方案 3：延迟解析
[User]
public partial class A : Node
{
    [Inject] private IB _b;  // 字段注入是延迟的
}
```

### ❌ Singleton 不依赖 Transient

```csharp
// ❌ 错误（编译时检测 GDI_D040）
[Singleton(typeof(IManager))]
public partial class Manager : IManager
{
    public Manager(ITransientDep dep) { }  // 错误！
}

// ✅ 正确：注入工厂
[Singleton(typeof(IManager))]
public partial class Manager : IManager
{
    private readonly Func<ITransientDep> _factory;
    
    public Manager(ITransientDepFactory factory)
    {
        _factory = factory.Create;
    }
    
    public void DoWork()
    {
        var dep = _factory();  // 需要时创建
        // 使用 dep...
    }
}
```

------

## Host 设计

### ✅ Host 暴露自身的标准模式

```csharp
[Host]
public partial class GameManager : Node, IGameState, IPauseController
{
    // 标准模式：通过属性暴露自身
    [Singleton(typeof(IGameState), typeof(IPauseController))]
    private IGameState Self => this;
    
    // 实现接口
    public GameState State { get; private set; }
    public void Pause() { /* ... */ }
    public void Resume() { /* ... */ }
}
```

### ✅ Host 管理的资源对象

```csharp
[Host]
public partial class WorldManager : Node
{
    // Host 持有并暴露普通对象
    [Singleton(typeof(IWorldConfig))]
    private WorldConfig _config = new();
    
    [Singleton(typeof(IWorldState))]
    private WorldState _state = new();
}

// 注意：这些不是 Service，不需要标记
public class WorldConfig : IWorldConfig
{
    public int Seed { get; set; }
    public int ChunkSize { get; set; }
}

public class WorldState : IWorldState
{
    public TimeSpan PlayTime { get; set; }
}
```

### ❌ Host 不应持有 Service 实例

```csharp
// ❌ 错误（编译时检测 GDI_M060）
[Singleton(typeof(IDataService))]
public partial class DataService : IDataService { }

[Host]
public partial class BadHost : Node
{
    [Singleton(typeof(IDataService))]
    private DataService _data = new();  // 错误！DataService 是 Service
}

// ✅ 正确：通过注入获取 Service
[Host, User]
public partial class GoodHost : Node
{
    [Inject] private IDataService _data;  // 注入 Service
}
```

------

## User 设计

### ✅ 使用 IServicesReady 确保依赖就绪

```csharp
[User]
public partial class PlayerController : CharacterBody3D, IServicesReady
{
    [Inject] private IPlayerStats _stats;
    [Inject] private IInputService _input;
    [Inject] private ICombatSystem _combat;
    
    // 所有依赖注入完成后调用
    public void OnServicesReady()
    {
        // 安全使用所有依赖
        _input.OnAttack += _combat.PerformAttack;
        _stats.Health = _stats.MaxHealth;
    }
    
    // ❌ 避免在 _Ready 中使用注入的依赖
    // 此时依赖可能尚未注入完成
}
```

### ✅ 非 Node User 的正确使用（作为 Node User 成员）

非 Node User 主要用于将 Node User 的复杂逻辑模块化，通过自动递归注入来解析依赖。

```csharp
// 定义非 Node User 逻辑模块
[User]
public partial class GameLogic  // 非 Node
{
    [Inject] private IConfig _config;
    [Inject] private ICombatSystem _combat;
    
    public void ProcessTurn()
    {
        // 使用注入的服务
        var damage = _combat.CalculateDamage();
        // ...
    }
}

// ✅ 推荐：作为 Node User 的成员使用
[User]
public partial class GameManager : Node
{
    [Inject] private ISaveService _save;
    
    // 自动识别为 UserMember，框架会递归注入其依赖
    private GameLogic _logic = new();
    
    public override void _Ready()
    {
        // 此时 _logic 的依赖已经自动注入完成
        _logic.ProcessTurn();
    }
}
```

**优点**：

- **代码组织清晰**：将复杂逻辑拆分为多个小模块
- **依赖自动注入**：框架自动处理递归注入，无需手动调用
- **易于测试**：逻辑模块可以独立测试

**重要约束**：

- 必须初始化：`private GameLogic _logic = new();`
- 只能作为 Node User 的成员（不能嵌套非 Node User）
- 不能是 Node 类型

### ❌ 避免错误的非 Node User 使用模式

```csharp
// ❌ 错误 1：非 Node User 包含非 Node User（编译错误 GDI_M071）
[User]
public partial class OuterLogic
{
    private InnerLogic _inner = new();  // 错误！不能嵌套
}

[User]
public partial class InnerLogic
{
    [Inject] private IService _service;
}

// ❌ 错误 2：非 Node User 成员未初始化（编译错误 GDI_M072）
[User]
public partial class GameManager : Node
{
    private GameLogic _logic;  // 错误！必须初始化
}

// ❌ 错误 3：期望独立使用的逻辑使用非 Node User
[User]
public partial class BadDesign
{
    [Inject] private IService _service;
    
    // 问题：如果这个类需要独立创建和使用，应该改为 Service
}

// ✅ 正确：根据使用场景选择合适的角色

// 场景 1：需要独立实例化 → 使用 Service
[Singleton(typeof(IMyLogic))]
public partial class MyLogic : IMyLogic
{
    public MyLogic(IService service)  // 构造函数注入
    {
    }
}

// 场景 2：需要场景树生命周期 → 使用 Node User
[User]
public partial class MyLogic : Node
{
    [Inject] private IService _service;  // 自动注入
}

// 场景 3：作为 Node User 的逻辑模块 → 使用非 Node User
[User]
public partial class MyLogic  // 非 Node
{
    [Inject] private IService _service;
}

[User]
public partial class ParentNode : Node
{
    private MyLogic _logic = new();  // 作为成员使用
}
```

------

## 资源管理

### ✅ 实现 IDisposable 进行清理

```csharp
[Singleton(typeof(IResourceLoader))]
public partial class ResourceLoader : IResourceLoader, IDisposable
{
    private readonly List<Resource> _loaded = new();
    
    public T Load<T>(string path) where T : Resource
    {
        var resource = GD.Load<T>(path);
        _loaded.Add(resource);
        return resource;
    }
    
    public void Dispose()
    {
        foreach (var res in _loaded)
        {
            res.Free();  // 释放 Godot 资源
        }
        _loaded.Clear();
    }
}
```

### ✅ Transient 服务的调用者负责释放

```csharp
[Transient(typeof(INetworkRequest))]
public partial class NetworkRequest : INetworkRequest, IDisposable
{
    private HttpClient _client = new();
    
    public void Dispose() => _client.Dispose();
}

// 调用者负责管理
[User]
public partial class NetworkManager : Node
{
    private List<INetworkRequest> _pendingRequests = new();
    
    public void MakeRequest()
    {
        scope.ResolveDependency<INetworkRequest>(request =>
        {
            _pendingRequests.Add(request);
        });
    }
    
    public override void _ExitTree()
    {
        // 调用者负责释放
        foreach (var req in _pendingRequests)
        {
            (req as IDisposable)?.Dispose();
        }
    }
}
```

------

## 测试

### ✅ 接口便于 Mock

```csharp
// 生产代码
[User]
public partial class PlayerUI : Control
{
    [Inject] private IPlayerStats _stats;
    
    public void UpdateHealthBar()
    {
        _healthBar.Value = _stats.Health / (float)_stats.MaxHealth;
    }
}

// 测试代码
public class PlayerUITests
{
    [Test]
    public void UpdateHealthBar_SetsCorrectValue()
    {
        // 使用 Mock
        var mockStats = new MockPlayerStats { Health = 50, MaxHealth = 100 };
        
        var ui = new PlayerUI();
        // 通过反射或测试辅助方法注入 Mock
        SetPrivateField(ui, "_stats", mockStats);
        
        ui.UpdateHealthBar();
        
        Assert.AreEqual(0.5f, ui._healthBar.Value);
    }
}

// Mock 实现
public class MockPlayerStats : IPlayerStats
{
    public int Health { get; set; }
    public int MaxHealth { get; set; }
}
```

### ✅ 独立测试 Service

```csharp
// Service 可以独立实例化测试
[Singleton(typeof(IDamageCalculator))]
public partial class DamageCalculator : IDamageCalculator
{
    public int Calculate(int baseDamage, float multiplier)
    {
        return (int)(baseDamage * multiplier);
    }
}

// 测试
public class DamageCalculatorTests
{
    [Test]
    public void Calculate_ReturnsCorrectValue()
    {
        var calc = new DamageCalculator();
        
        var result = calc.Calculate(100, 1.5f);
        
        Assert.AreEqual(150, result);
    }
}
```

------

## 常见陷阱

### ⚠️ 在 _Ready 中使用未注入的依赖

```csharp
[User]
public partial class BadExample : Node
{
    [Inject] private IService _service;
    
    public override void _Ready()
    {
        // ❌ 危险：_service 可能尚未注入
        _service.DoSomething();  // NullReferenceException!
    }
}

// ✅ 使用 IServicesReady
[User]
public partial class GoodExample : Node, IServicesReady
{
    [Inject] private IService _service;
    
    public void OnServicesReady()
    {
        // ✅ 安全：所有依赖已注入
        _service.DoSomething();
    }
}
```

### ⚠️ 混淆 Service 和 Host 的职责

```csharp
// ❌ 错误：试图让 Service 管理 Node 资源
[Singleton(typeof(ISceneManager))]
public partial class SceneManager : ISceneManager
{
    private Node _root;  // ❌ Service 不应持有 Node
    
    public void LoadScene(string path)
    {
        // ❌ Service 不应操作场景树
        _root.GetTree().ChangeSceneToFile(path);
    }
}

// ✅ 正确：使用 Host 管理 Node 资源
[Host]
public partial class SceneManager : Node, ISceneManager
{
    [Singleton(typeof(ISceneManager))]
    private ISceneManager Self => this;
    
    public void LoadScene(string path)
    {
        // ✅ Host 可以安全操作场景树
        GetTree().ChangeSceneToFile(path);
    }
}
```

### ⚠️ 忘记标记 partial

```csharp
// ❌ 编译错误 GDI_C050
[Singleton(typeof(IService))]
public class MyService : IService { }  // 忘记 partial

// ✅ 正确
[Singleton(typeof(IService))]
public partial class MyService : IService { }
```

### ⚠️ 在静态成员上使用注入

```csharp
// ❌ 编译错误 GDI_M054
[User]
public partial class BadUser : Node
{
    [Inject] private static IService _service;  // 静态不允许
}

// ✅ 正确
[User]
public partial class GoodUser : Node
{
    [Inject] private IService _service;  // 实例成员
}
```
