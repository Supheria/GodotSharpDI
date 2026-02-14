# GodotSharpDI

<img src="icon.png" style="zoom:50%;" />

<p align="left"> <a href="README.md">English</a> </p>

一个专为 Godot 4 设计的编译时依赖注入框架,通过 C# 源生成器实现零反射、高性能的 DI 支持。

[![NuGet Version](https://img.shields.io/nuget/v/GodotSharpDI.svg?style=flat)](https://www.nuget.org/packages/GodotSharpDI/)

## 目录

- [设计理念](#设计理念)
- [安装](#安装)
- [快速开始](#快速开始)
  - [1. 定义服务接口](#1-定义服务接口)
  - [2. 定义带服务提供者的 Host](#2-定义带服务提供者的-host)
  - [3. 定义 Scope](#3-定义-scope)
  - [4. 定义 User](#4-定义-user)
  - [5. 场景树结构](#5-场景树结构)
- [核心概念](#核心概念)
  - [三种角色类型](#三种角色类型)
  - [服务生命周期](#服务生命周期)
- [角色详解](#角色详解)
  - [Host (服务提供者)](#host-服务提供者)
  - [User (消费者)](#user-消费者)
  - [Scope (容器)](#scope-容器)
- [使用 [Provide] 提供服务](#使用-provide-提供服务)
  - [属性提供者](#属性提供者)
  - [方法提供者](#方法提供者)
  - [异步提供者](#异步提供者)
  - [WaitFor 机制](#waitfor-机制)
- [生命周期管理](#生命周期管理)
  - [服务生命周期](#服务生命周期-1)
  - [Scope 层级](#scope-层级)
  - [依赖注入时序](#依赖注入时序)
  - [Host + User 与循环依赖](#host--user-与循环依赖)
- [类型约束](#类型约束)
  - [角色类型约束](#角色类型约束)
  - [注入类型约束](#注入类型约束)
  - [暴露类型约束](#暴露类型约束)
  - [其他约束](#其他约束)
- [API 参考](#api-参考)
  - [特性](#特性)
  - [接口](#接口)
  - [生成的代码](#生成的代码)
  - [场景树集成](#场景树集成)
- [最佳实践](#最佳实践)
  - [Scope 粒度设计](#scope-粒度设计)
  - [服务释放](#服务释放)
  - [避免循环依赖](#避免循环依赖)
  - [接口优先原则](#接口优先原则)
  - [Host + User 组合使用](#host--user-组合使用)
  - [使用服务工厂](#使用服务工厂)
- [从 1.0.0-rc.3 迁移指南](#从-100-rc3-迁移指南)
- [诊断代码](#诊断代码)
- [许可证](#许可证)
- [附录：需要显式声明 _Notification 方法](#附录需要显式声明-_notification-方法)
- [Todo List](#todo-list)

---

## 设计理念

GodotSharpDI 的核心设计理念是**将 Godot 的场景树生命周期与传统 DI 容器模式融合**：

- **场景树即容器层级**：利用 Godot 的场景树结构实现作用域 (Scope) 层级
- **Node 生命周期集成**：服务的创建和销毁与 Node 的进入/退出场景树事件绑定
- **编译时安全**：通过 Source Generator 在编译期完成依赖分析和代码生成，提供完整的编译时错误检查
- **基于提供者的架构**：服务通过 Host 使用 `[Provide]` 特性提供，提供更大的灵活性和控制力

---

## 安装

```xml
<PackageReference Include="GodotSharpDI" Version="1.1.0-rc.1" />
```
⚠️ **确保项目中同时添加了 GodotSharp 软件包**：生成的代码依赖 Godot.Node 和 Godot.GD。

---

## 快速开始

### 1. 定义服务接口

```csharp
// 定义服务接口
public interface IPlayerStats
{
    int Health { get; set; }
    int Mana { get; set; }
}

public interface IGameState
{
    GameState CurrentState { get; set; }
}
```

### 2. 定义带服务提供者的 Host

```csharp
[Host]
public partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // 将自身作为 IGameState 服务提供
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
    
    // 提供 IPlayerStats 服务
    [Provide(ExposedTypes = [typeof(IPlayerStats)])]
    public IPlayerStats CreatePlayerStats()
    {
        return new PlayerStatsService { Health = 100, Mana = 50 };
    }
    
    public GameState CurrentState { get; set; }
    
    // 在所有依赖解析后调用
    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        if (isAllDependenciesReady)
        {
            GD.Print("GameManager 已就绪，所有依赖已解析");
        }
    }
    
    // Godot 生命周期集成所需
    public override partial void _Notification(int what);
}

// 服务实现（不再需要 [Singleton]）
public class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; }
    public int Mana { get; set; }
}
```

### 3. 定义 Scope

```csharp
[Modules(Hosts = [typeof(GameManager)])]
public partial class GameScope : Node, IScope
{
    // 框架自动生成 IScope 实现
    
    // Godot 生命周期集成所需
    public override partial void _Notification(int what);
}
```

### 4. 定义 User

```csharp
[User]
public partial class PlayerUI : Control, IDependenciesResolved
{
    [Inject] private IPlayerStats _stats;
    [Inject] private IGameState _gameState;
    
    // 在所有依赖解析后调用
    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        if (isAllDependenciesReady)
        {
            UpdateUI();
        }
        else
        {
            GD.Print("部分依赖注入失败");
        }
    }
    
    private void UpdateUI()
    {
        GD.Print($"生命值: {_stats.Health}, 法力值: {_stats.Mana}");
        GD.Print($"游戏状态: {_gameState.CurrentState}");
    }
    
    // Godot 生命周期集成所需
    public override partial void _Notification(int what);
}
```

### 5. 场景树结构

```
GameScope (IScope)
├── GameManager (Host) ← 提供服务
└── PlayerUI (User) ← 消费服务
```

---

## 核心概念

### 三种角色类型

| 角色 | 描述 | 约束 |
|------|------|------|
| **Host** | 服务提供者，连接 Node 资源与 DI 世界，通过 `[Provide]` 成员提供服务 | 必须是 Node |
| **User** | 依赖消费者，接收注入 | 必须是 Node |
| **Scope** | DI 容器，管理服务生命周期 | 必须是 Node，实现 IScope |

**1.1.0 的重要变化**：移除了 `[Singleton]` 特性和独立的服务类。服务现在直接通过 Host 使用 `[Provide]` 特性提供，提供更灵活统一的架构。

---

## 角色详解

### Host (服务提供者)

#### 职责

Host 是 Godot Node 系统与 DI 系统之间的桥梁，通过 `[Provide]` 成员提供服务。

#### 服务提供方式

Host 可以通过以下方式提供服务：

1. **属性** - 简单的同步服务提供
2. **方法** - 灵活的带参数服务创建
3. **异步方法** - 支持异步初始化

```csharp
[Host]
public partial class ServiceHost : Node
{
    // 属性提供者
    [Provide(ExposedTypes = [typeof(IConfig)])]
    public IConfig Config => new ConfigService();
    
    // 方法提供者
    [Provide(ExposedTypes = [typeof(IDatabase)])]
    public IDatabase CreateDatabase()
    {
        return new DatabaseService("connection-string");
    }
    
    // 异步提供者
    [Provide(ExposedTypes = [typeof(IAsyncService)])]
    public async Task<IAsyncService> InitializeAsync()
    {
        var service = new AsyncService();
        await service.InitializeAsync();
        return service;
    }
    
    // Godot 生命周期集成所需
    public override partial void _Notification(int what);
}
```

#### Host 作为服务消费者

Host 也可以通过添加 `[Inject]` 成员来作为服务消费者：

```csharp
[Host]
public partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // 消费服务
    [Inject] private IConfig _config;
    
    // 提供服务
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
    
    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        if (isAllDependenciesReady)
        {
            // 使用注入的依赖进行初始化
            InitializeGame();
        }
    }
    
    public override partial void _Notification(int what);
}
```

---

### User (消费者)

#### 职责

User 是服务消费者，接收注入的依赖。

#### 依赖注入

```csharp
[User]
public partial class PlayerController : Node, IDependenciesResolved
{
    [Inject] private IPlayerStats _stats;
    [Inject] private IInputService _input;
    [Inject] private IPhysicsService _physics;
    
    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        if (isAllDependenciesReady)
        {
            // 所有依赖就绪
            InitializeController();
        }
        else
        {
            GD.PrintErr("部分依赖注入失败");
        }
    }
    
    public override partial void _Notification(int what);
}
```

---

### Scope (容器)

#### 职责

Scope 是 DI 容器，管理服务生命周期并协调依赖注入。

#### 声明

```csharp
[Modules(Hosts = [typeof(GameManager), typeof(ServiceHost)])]
public partial class GameScope : Node, IScope
{
    // 框架生成所有实现
    public override partial void _Notification(int what);
}
```

#### Scope 层级

```
RootScope (IScope)
├── Host1 (Host)
├── User1 (User)
└── SubScope (IScope)
    ├── Host2 (Host)
    └── User2 (User)
```

父 Scope 提供的服务可以被子 Scope 访问。

---

## 使用 [Provide] 提供服务

### 属性提供者

最简单的服务提供方式：

```csharp
[Host]
public partial class ConfigHost : Node
{
    [Provide(ExposedTypes = [typeof(IConfig)])]
    public IConfig Config => new ConfigService();
    
    // 可以暴露多个类型
    [Provide(ExposedTypes = [typeof(IReader), typeof(IWriter)])]
    public FileService FileService => new FileService();
    
    public override partial void _Notification(int what);
}
```

### 方法提供者

更灵活的服务创建：

```csharp
[Host]
public partial class FactoryHost : Node
{
    [Inject] private IConfig _config;
    
    [Provide(ExposedTypes = [typeof(IDatabase)])]
    public IDatabase CreateDatabase()
    {
        // 可以使用注入的依赖
        var connectionString = _config.GetConnectionString();
        return new DatabaseService(connectionString);
    }
    
    [Provide(ExposedTypes = [typeof(ICache)])]
    public ICache CreateCache()
    {
        // 可以实现复杂的初始化逻辑
        var cache = new CacheService();
        cache.Initialize();
        return cache;
    }
    
    public override partial void _Notification(int what);
}
```

### 异步提供者

支持异步初始化，并通过 `CallDeferred` 自动确保线程安全：

```csharp
[Host]
public partial class AsyncHost : Node
{
    [Provide(ExposedTypes = [typeof(IResourceLoader)])]
    public async Task<IResourceLoader> LoadResourcesAsync()
    {
        var loader = new ResourceLoader();
        await loader.LoadAsync();
        return loader;
    }
    
    [Provide(ExposedTypes = [typeof(INetworkService)])]
    public async Task<INetworkService> ConnectAsync()
    {
        var service = new NetworkService();
        await service.ConnectAsync();
        return service;
    }
    
    public override partial void _Notification(int what);
}
```

**线程安全**：当异步提供者完成时（可能在后台线程上），框架会自动使用 Godot 的 `CallDeferred` 机制将结果编组回主线程。这确保所有服务注册都在 Godot 的主线程上进行，防止崩溃并确保线程安全。

**内部实现机制**：
```csharp
// 你编写这样的代码：
[Provide(ExposedTypes = [typeof(IDatabase)])]
public async Task<IDatabase> ConnectAsync() { ... }

// 框架生成这样的代码：
private static async Task ProvideAsync_ConnectAsync_IDatabase(Task<IDatabase> task, IScope scope)
{
    try
    {
        var result = await task; // 可能在后台线程上完成
        
        // 自动使用 CallDeferred 返回主线程
        Callable.From(() =>
        {
            scope.ProvideService<IDatabase>(result);
        }).CallDeferred();
    }
    catch (Exception ex)
    {
        Callable.From(() =>
        {
            scope.ProvideService<IDatabase>(null, ex.Message);
        }).CallDeferred();
    }
}
```

### WaitFor 机制

**1.1.0 新功能**：服务可以等待其他服务就绪后再提供。

```csharp
[Host]
public partial class DependentHost : Node, IDependenciesResolved
{
    [Inject] private IConfig _config;
    
    // 此服务立即提供
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger CreateLogger()
    {
        return new Logger();
    }
    
    // 此服务等待 CreateLogger 完成
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(CreateLogger)])]
    public IDatabase CreateDatabase()
    {
        // Logger 保证可用
        return new DatabaseService();
    }
    
    // 此服务等待 _config 注入和 CreateDatabase
    [Provide(ExposedTypes = [typeof(IRepository)], WaitFor = [nameof(_config), nameof(CreateDatabase)])]
    public IRepository CreateRepository()
    {
        // config 和 database 都保证就绪
        return new Repository(_config, /* database 将被注入 */);
    }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        GD.Print("所有依赖已解析");
    }
    
    public override partial void _Notification(int what);
}
```

**WaitFor 规则**：
- 可以等待 `[Inject]` 成员（例如：`nameof(_config)`）
- 可以等待其他 `[Provide]` 成员（例如：`nameof(CreateLogger)`）
- 循环等待在编译时检测
- 支持复杂的依赖链

---

## 生命周期管理

### 服务生命周期

1. **创建**：服务在以下情况创建：
   - Scope 进入场景树
   - Host 注册其提供者
   - User 请求注入

2. **销毁**：服务在以下情况销毁：
   - 提供服务的 Scope 退出场景树
   - 所有服务自动销毁

### Scope 层级

```
RootScope
├── 服务 A（来自 RootScope）
└── ChildScope
    ├── 服务 A（从父级继承）
    └── 服务 B（仅在子级）
```

子 Scope 从父 Scope 继承服务，但也可以覆盖它们。

### 依赖注入时序

1. **Scope 进入场景树**（`NotificationEnterTree`）
2. **Host 注册提供者**
3. **服务被创建**（遵守 WaitFor 链）
4. **User 接收注入**
5. **调用 `OnDependenciesResolved`**（在 `_Ready` 之后）

### Host + User 与循环依赖

Host 也可以是 User：

```csharp
[Host]
public partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // 作为 User：注入依赖
    [Inject] private IConfig _config;
    [Inject] private ISaveSystem _saveSystem;
    
    // 作为 Host：提供服务
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
    
    public GameState CurrentState { get; set; }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        if (isAllDependenciesReady)
        {
            // 依赖就绪，可以初始化
            LoadLastSave();
        }
    }
    
    public override partial void _Notification(int what);
}
```

这个模式**不是**循环依赖，因为：
1. Host 注册首先发生
2. 服务提供发生
3. User 注入随后发生
4. 不形成构造函数循环

---

## 类型约束

### 角色类型约束

| 角色 | 允许的基类型 | 禁止 |
|------|-------------|------|
| **Host** | Node 及其子类 | 泛型类型 |
| **User** | Node 及其子类 | 泛型类型 |
| **Scope** | 必须实现 IScope | 泛型类型 |

### 注入类型约束

- **推荐**：注入接口（例如：`IService`）
- **警告**：注入具体的 Host 类型
- **错误**：注入 User 类型、Scope 类型或常规 Node 类型

### 暴露类型约束

- **推荐**：暴露接口
- **允许**：暴露 Host 类型本身
- **必须实现**：提供者必须实现或返回暴露的类型

### 其他约束

- 每个 `[Provide]` 成员必须至少有一个暴露类型
- WaitFor 目标必须是有效的 `[Inject]` 或 `[Provide]` 成员
- 循环 WaitFor 依赖是编译时错误

---

## API 参考

### 特性

#### `[Host]`
标记 Node 为服务提供者。

```csharp
[Host]
public partial class ServiceHost : Node
{
    public override partial void _Notification(int what);
}
```

#### `[User]`
标记 Node 为服务消费者。

```csharp
[User]
public partial class ServiceUser : Node
{
    [Inject] private IService _service;
    public override partial void _Notification(int what);
}
```

#### `[Provide(ExposedTypes = [...], WaitFor = [...])]`
标记属性或方法为服务提供者。

**参数**：
- `ExposedTypes`：此服务将注册为的类型数组
- `WaitFor`：（可选）提供前要等待的成员名称数组

```csharp
[Provide(ExposedTypes = [typeof(IService)])]
public IService Service => new ServiceImpl();

[Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
public IDatabase CreateDatabase()
{
    return new DatabaseService(_config.ConnectionString);
}
```

#### `[Inject]`
标记字段或属性以进行依赖注入。

```csharp
[Inject] private IService _service;
[Inject] private IConfig Config { get; set; }
```

#### `[Modules(Hosts = [...])]`
定义哪些 Host 属于 Scope。

```csharp
[Modules(Hosts = [typeof(Host1), typeof(Host2)])]
public partial class GameScope : Node, IScope { }
```

### 接口

#### `IScope`
必须由 Scope 类型实现。框架生成实现。

```csharp
public partial class GameScope : Node, IScope
{
    // 框架生成实现
}
```

#### `IDependenciesResolved`
可选接口，用于接收依赖解析通知。

```csharp
public interface IDependenciesResolved
{
    void OnDependenciesResolved(bool isAllDependenciesReady);
}
```

### 生成的代码

对于每个角色，框架生成：

- **Host**：提供者注册、服务创建逻辑
- **User**：注入逻辑、依赖解析
- **Scope**：服务容器、生命周期管理

所有生成的代码在 `*.DI.g.cs` 文件中。

### 场景树集成

框架通过 `_Notification` 与 Godot 的生命周期集成：

```csharp
public override partial void _Notification(int what)
{
    base._Notification(what);
    switch ((long)what)
    {
        case NotificationEnterTree:
            AttachToScope();
            break;
        case NotificationExitTree:
            DetachFromScope();
            break;
    }
}
```

---

## 最佳实践

### Scope 粒度设计

基于功能模块设计 Scope：

```
GameRoot (Scope)
├── GlobalServices (Host) - Config, SaveSystem
├── MainMenu (Scope)
│   └── MenuServices (Host) - UIManager
└── GameLevel (Scope)
    ├── LevelServices (Host) - PhysicsEngine
    └── PlayerServices (Host) - PlayerStats
```

### 服务释放

为需要清理的服务实现 `IDisposable`：

```csharp
public class DatabaseService : IDatabase, IDisposable
{
    public void Dispose()
    {
        // 清理资源
        Connection?.Close();
    }
}

[Host]
public partial class DataHost : Node
{
    [Provide(ExposedTypes = [typeof(IDatabase)])]
    public IDatabase CreateDatabase()
    {
        return new DatabaseService();
    }
    
    public override partial void _Notification(int what);
}
```

### 避免循环依赖

**编译时检测**：框架检测循环 WaitFor 链：

```csharp
// ❌ 循环依赖 - 编译错误
[Provide(ExposedTypes = [typeof(IServiceA)], WaitFor = [nameof(CreateB)])]
public IServiceA CreateA() => new ServiceA();

[Provide(ExposedTypes = [typeof(IServiceB)], WaitFor = [nameof(CreateA)])]
public IServiceB CreateB() => new ServiceB();
```

**解决方案**：重构依赖或使用事件：

```csharp
// ✅ 正确方法
[Provide(ExposedTypes = [typeof(IServiceA)])]
public IServiceA CreateA() => new ServiceA();

[Provide(ExposedTypes = [typeof(IServiceB)], WaitFor = [nameof(CreateA)])]
public IServiceB CreateB() => new ServiceB(/* 将通过注入接收 A */);
```

### 接口优先原则

始终暴露接口而不是具体类型：

```csharp
// ❌ 不推荐
[Provide(ExposedTypes = [typeof(DatabaseService)])]
public DatabaseService CreateDatabase() => new DatabaseService();

// ✅ 推荐
[Provide(ExposedTypes = [typeof(IDatabase)])]
public IDatabase CreateDatabase() => new DatabaseService();
```

### Host + User 组合使用

当 Node 需要同时提供和消费服务时，结合 Host 和 User：

```csharp
[Host]
public partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // 作为 User：注入依赖
    [Inject] private IConfig _config;
    [Inject] private ISaveSystem _saveSystem;
    
    // 作为 Host：提供服务
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
    
    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        if (isAllDependenciesReady)
        {
            InitializeWithDependencies();
        }
    }
    
    public override partial void _Notification(int what);
}
```

### 使用服务工厂

创建工厂服务来管理动态对象创建：

```csharp
public interface IEnemyFactory
{
    Enemy CreateEnemy(Vector3 position);
}

public class Enemy
{
    private readonly IPlayerStats _playerStats;
    
    public Enemy(IPlayerStats playerStats, Vector3 position)
    {
        _playerStats = playerStats;
        Position = position;
    }
    
    public Vector3 Position { get; }
}

[Host]
public partial class GameHost : Node, IDependenciesResolved
{
    [Inject] private IPlayerStats _playerStats;
    
    [Provide(ExposedTypes = [typeof(IEnemyFactory)], WaitFor = [nameof(_playerStats)])]
    public IEnemyFactory CreateEnemyFactory()
    {
        return new EnemyFactory(_playerStats);
    }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady) { }
    
    public override partial void _Notification(int what);
}

public class EnemyFactory : IEnemyFactory
{
    private readonly IPlayerStats _playerStats;
    
    public EnemyFactory(IPlayerStats playerStats)
    {
        _playerStats = playerStats;
    }
    
    public Enemy CreateEnemy(Vector3 position)
    {
        return new Enemy(_playerStats, position);
    }
}
```

---

## 从 1.0.0-rc.3 迁移指南

### 为什么是 1.1.0 而不是 1.0.0？

在发布 1.0.0-rc.3 后，我们发现了一个架构局限：`[Singleton]` 特性和独立的服务类虽然功能正常，但创造了不必要的复杂性并限制了灵活性。1.1.0 的新提供者架构提供了：

- **更大的灵活性**：服务与 Host 内联定义
- **更好的资源管理**：创建服务时直接访问 Node 资源
- **异步支持**：原生支持异步服务初始化
- **依赖排序**：WaitFor 机制用于复杂的初始化序列
- **简化的架构**：减少一个学习概念（不再需要单独的 Service 类）

鉴于这些变化的规模，我们决定增加到 1.1.0，而不是发布已知限制的 1.0.0。

### 迁移步骤

#### 1. 用 [Provide] 方法替换 [Singleton] 服务类

**之前（1.0.0-rc.3）**：
```csharp
// 独立的服务类
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;
}

[Singleton(typeof(IDatabase))]
public partial class DatabaseService : IDatabase
{
    [InjectConstructor]
    public DatabaseService(IConfig config)
    {
        ConnectionString = config.ConnectionString;
    }
    
    public string ConnectionString { get; }
}

[Modules(
    Services = [typeof(PlayerStatsService), typeof(DatabaseService)],
    Hosts = [typeof(GameManager)]
)]
public partial class GameScope : Node, IScope { }
```

**之后（1.1.0）**：
```csharp
// 服务由 Host 提供
[Host]
public partial class ServiceHost : Node, IDependenciesResolved
{
    [Inject] private IConfig _config;
    
    [Provide(ExposedTypes = [typeof(IPlayerStats)])]
    public IPlayerStats CreatePlayerStats()
    {
        return new PlayerStatsService { Health = 100, Mana = 50 };
    }
    
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public IDatabase CreateDatabase()
    {
        return new DatabaseService(_config.ConnectionString);
    }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady) { }
    
    public override partial void _Notification(int what);
}

// 服务实现（不需要特性）
public class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; }
    public int Mana { get; set; }
}

public class DatabaseService : IDatabase
{
    public DatabaseService(string connectionString)
    {
        ConnectionString = connectionString;
    }
    
    public string ConnectionString { get; }
}

// 简化的 Modules 特性
[Modules(Hosts = [typeof(ServiceHost), typeof(GameManager)])]
public partial class GameScope : Node, IScope
{
    public override partial void _Notification(int what);
}
```

#### 2. 移除 [InjectConstructor] 特性

不再需要 `[InjectConstructor]` 特性。服务由提供者方法创建，您可以完全控制构造。

#### 3. 更新 Modules 特性

从 `[Modules]` 中移除 `Services` 参数：

```csharp
// 之前
[Modules(
    Services = [typeof(Service1), typeof(Service2)],
    Hosts = [typeof(Host1)]
)]

// 之后
[Modules(Hosts = [typeof(Host1)])]
```

#### 4. 使用 WaitFor 处理服务依赖

如果您的服务依赖其他服务：

```csharp
[Host]
public partial class ServiceHost : Node, IDependenciesResolved
{
    [Inject] private IConfig _config;
    
    // Logger 首先创建
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger CreateLogger()
    {
        return new Logger();
    }
    
    // Database 等待 config 注入
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public IDatabase CreateDatabase()
    {
        return new DatabaseService(_config.ConnectionString);
    }
    
    // Repository 等待两者
    [Provide(ExposedTypes = [typeof(IRepository)], 
             WaitFor = [nameof(CreateLogger), nameof(CreateDatabase)])]
    public IRepository CreateRepository()
    {
        // logger 和 database 都已就绪
        return new Repository();
    }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady) { }
    public override partial void _Notification(int what);
}
```

### 重大变化总结

| 功能 | 1.0.0-rc.3 | 1.1.0-rc.1 |
|------|------------|------------|
| 服务声明 | 类上的 `[Singleton]` | Host 成员上的 `[Provide]` |
| 构造函数注入 | `[InjectConstructor]` | 使用提供者方法参数 |
| Modules 特性 | `Services = [...]` | 已移除，仅 `Hosts = [...]` |
| 服务依赖 | 构造函数参数 | `WaitFor` 机制 |
| 异步支持 | 不支持 | `Task<T>` 返回类型 |

---

## 诊断代码

框架提供全面的编译时错误检查。完整的诊断代码列表，请参考 [AnalyzerReleases.Shipped.md](./GodotSharpDI.SourceGenerator/AnalyzerReleases.Shipped.md)。

**诊断代码类别**：

| 前缀 | 类别 | 描述 |
|------|------|------|
| GDI_C | 类 | 类级错误 |
| GDI_M | 成员 | 成员级错误 |
| GDI_D | 依赖图 | 依赖图错误 |
| GDI_E | 内部错误 | 内部错误 |
| GDI_U | 用户行为 | 用户行为警告 |

---

## 许可证

MIT License

## 附录：需要显式声明 _Notification 方法

所有 Host、User 和 Scope 类型**必须**在附加到节点的 C# 脚本文件中显式定义 `_Notification` 方法：

```csharp
public override partial void _Notification(int what);
```

### 为什么需要这样做？

- 当您将 C# 脚本附加到 Godot 中的节点时，引擎会在节点和该特定脚本文件之间创建绑定
- Godot 的脚本绑定机制仅扫描附加的脚本文件以查找虚拟方法覆盖
- 源生成的文件（*.g.cs）通过 `partial` 编译到同一类中，但 Godot 不会扫描这些文件以查找生命周期方法
- 因此，像 `_Notification` 这样的生命周期钩子必须在用户的源文件中声明为 `partial` 方法

### IDE 支持

IDE（Visual Studio、Rider）将提供自动修复：

1. 如果您忘记添加此方法，您会看到 **GDI_C080** 错误
2. 在错误上按 `Ctrl+.`（VS）或 `Alt+Enter`（Rider）
3. 选择"添加 _Notification 方法声明"以自动生成正确的声明

### 示例：

```csharp
// 您的源文件：GameManager.cs（附加到节点）
[Host]
public partial class GameManager : Node
{
    // 必需：Godot 需要看到此声明
    public override partial void _Notification(int what);

    [Provide(ExposedTypes = [typeof(IGameState)])]
    public IGameState Self => this;
}

// 生成的文件：GameManager.DI.g.cs（Godot 不扫描）
partial class GameManager
{
    // 框架提供实现
    public override partial void _Notification(int what)
    {
        base._Notification(what);
        switch ((long)what)
        {
            case NotificationEnterTree:
                AttachToScope();
                break;
            case NotificationExitTree:
                DetachFromScope();
                break;
        }
    }
}
```

## Todo List

### 1. 文档和示例

- [ ] 完善双语（中英文）文档
- [ ] 添加完整的示例项目
- [ ] 创建视频教程
- [ ] 增强生成代码中的注释覆盖率

### 2. 测试

- [ ] 添加运行时集成测试
- [ ] 添加生成器、分析器、代码修复程序集成测试
- [ ] 添加 WaitFor 机制测试

### 3. 功能

- [x] 实现依赖 WaitFor 机制
- [x] 支持异步服务提供者
- [ ] 支持异步操作（使用 CallDeferred）
- [ ] 添加服务生命周期配置选项

### 4. 诊断

- [x] 诊断生成器内部错误（GDI_E）
- [ ] 添加更详细的 WaitFor 循环检测
- [ ] 改进带代码示例的错误消息
