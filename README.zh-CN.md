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
  - [Host 使用 Inject](#host-使用-inject)
- [类型约束](#类型约束)
  - [角色类型约束](#角色类型约束)
  - [注入类型约束](#注入类型约束)
  - [暴露类型约束](#暴露类型约束)
  - [其他约束](#其他约束)
- [API 参考](#api-参考)
  - [特性](#特性)
  - [注入回调](#注入回调)
  - [接口](#接口)
  - [生成的代码](#生成的代码)
  - [场景树集成](#场景树集成)
- [最佳实践](#最佳实践)
  - [Scope 粒度设计](#scope-粒度设计)
  - [服务释放](#服务释放)
  - [避免循环依赖](#避免循环依赖)
  - [接口优先原则](#接口优先原则)
  - [Host 注入和提供服务](#host-注入和提供服务)
  - [使用服务工厂](#使用服务工厂)
- [诊断代码](#诊断代码)
- [许可证](#许可证)
- [附录：需要显式声明 _Notification 方法](#附录需要显式声明-_notification-方法)

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
<PackageReference Include="GodotSharpDI" Version="1.3.3" />
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
    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
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
[Modules(typeof(GameManager))]
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
    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
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

**1.1.1 新功能**：Host 现在可以使用 `[Inject]` 成员，并完整支持回调（FailureCallback 和 ReadyCallback）。

Host 也可以通过添加 `[Inject]` 成员来作为服务消费者：

```csharp
[Host]
public partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // 使用回调消费服务（1.1.1 新功能）
    [Inject(ReadyCallback = true, FailureCallback = true)]
    private IConfig? _config;
    
    // 提供服务
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
    
    // 使用 WaitFor 确保在提供数据库服务前 _config 已注入
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public IDatabase CreateDatabase()
    {
        if (!IsConfigInjectionReady || _config == null)
        {
            return new InMemoryDatabase();
        }
        return new DatabaseService(_config.ConnectionString);
    }
    
    // 注入回调（1.1.1 新功能）
    partial void OnConfigInjectionReady(IConfigService config)
    {
        GD.Print("配置加载成功");
        ApplyConfiguration();
    }
    
    partial void OnConfigInjectionFailed()
    {
        GD.PrintErr("配置加载失败");
        UseDefaultConfiguration();
    }
    
    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
        {
            // 使用注入的依赖进行初始化
            InitializeGame();
        }
    }
    
    public override partial void _Notification(int what);
}
```

**优势**：
- Host 可以从同一 Scope 中的其他 Host 消费服务
- 完整的回调支持，实现更好的错误处理
- 与 `WaitFor` 机制无缝集成
- 支持复杂的服务依赖图

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
    
    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
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

#### 注入回调

**1.1.1 新功能**：`FailureCallback` 和 `ReadyCallback` 实现对注入的精细控制。

##### FailureCallback - 处理注入失败

```csharp
[User]
public partial class NetworkManager : Node, IDependenciesResolved
{
    [Inject(FailureCallback = true)]
    private INetworkService? _networkService;
    
    // 注入失败时自动调用
    partial void OnNetworkServiceInjectionFailed()
    {
        GD.PrintErr("网络服务不可用");
        EnableOfflineMode();  // 降级策略
    }
    
    public void OnDependenciesResolved() { }
    public override partial void _Notification(int what);
}
```

##### ReadyCallback - 注入成功后初始化

```csharp
[User]
public partial class GameUI : Control, IDependenciesResolved
{
    [Inject(ReadyCallback = true)]
    private IGameState? _gameState;
    
    // 注入成功时自动调用
    partial void OnGameStateInjectionReady(IGameState gameState)
    {
        GD.Print("游戏状态服务已就绪");
        _gameState!.Initialize();
        UpdateUI();
    }
    
    public void OnDependenciesResolved() { }
    public override partial void _Notification(int what);
}
```

##### 组合使用

```csharp
[User]
public partial class DatabaseManager : Node, IDependenciesResolved
{
    [Inject(FailureCallback = true, ReadyCallback = true)]
    private IDatabaseService? _database;
    
    partial void OnDatabaseInjectionReady(IDatabaseService database)
    {
        _database!.MigrateSchema();
        LoadInitialData();
    }
    
    partial void OnDatabaseInjectionFailed()
    {
        GD.PrintErr("数据库连接失败");
        UseFallbackDataSource();
    }
    
    public void OnDependenciesResolved() { }
    public override partial void _Notification(int what);
}
```

**关键特性**：
- **FailureCallback**：无参数 — 注入失败时调用（通过 `IsXxxInjectionReady` 检查状态）
- **ReadyCallback**：无参数，在注入成功后立即调用
- **可选实现**：Partial 方法 - 仅在需要时实现
- **IDE 支持**：智能分析器检测缺失的实现并提供一键修复（GDI_U004，GDI_U006）

---

### Scope (容器)

#### 职责

Scope 是 DI 容器，管理服务生命周期并协调依赖注入。

#### 声明

```csharp
[Modules(typeof(GameManager), typeof(ServiceHost))]
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

### 字段（v1.3.0 新增）或属性提供者


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

结合 Godot 的 `[Export]` 将子节点作为服务暴露时特别有用，无需将这些子节点标记为 `[Host]`。

```csharp
[Host]
public sealed partial class GuiHost : Node
{
    [Export]
    [Provide(ExposedTypes = [typeof(IAlertBox)])]
    private AlertBox _alertBox;

    public override partial void _Notification(int what);
}
```

对应的场景树结构：

```
Root (Scope)
  |- GuiHost  [Host]
  |    |- AlertBox        ← 普通 Node，通过 GuiHost 暴露
  |- MapLoader  [User]    ← 注入 IAlertBox
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

#### 核心概念

在使用 `WaitFor` 时，理解以下两个重要概念的区别：

| 概念 | 说明 | 对应状态 |
|-----|------|---------|
| **依赖解析完成** | 框架已尝试解析依赖并调用了回调 | `OnDependencyResolved<T>()` 被调用 |
| **依赖真正就绪** | 依赖成功解析且实例可用 | `IsXxxInjectionReady = true` |

⚠️ **重要**：`WaitFor` 只保证依赖解析已尝试，不保证依赖一定成功注入！

#### 基础示例

```csharp
[Host]
public partial class DependentHost : Node, IDependenciesResolved
{
    [Inject] private IConfig? _config;
    [Inject] private ILogger? _logger;
    
    // 框架会自动生成以下属性（在生成的代码中）：
    // private bool IsConfigInjectionReady { get; set; } = false;
    // private bool IsLoggerInjectionReady { get; set; } = false;
    
    // 立即提供的服务（无需等待任何依赖）
    [Provide(ExposedTypes = [typeof(IMetrics)])]
    public IMetrics CreateMetrics()
    {
        return new MetricsService();
    }
    
    // 等待 _config 注入后才提供
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public IDatabase CreateDatabase()
    {
        // ⚠️ WaitFor 只保证解析已尝试，需要检查是否真正成功
        if (!IsConfigInjectionReady || _config == null)
        {
            GD.PrintErr("Config 依赖未就绪，使用内存数据库");
            return new InMemoryDatabase();
        }
        
        // 安全：此时 _config 保证不为 null
        return new DatabaseService(_config.ConnectionString);
    }
    
    // 等待 _logger 和 _config 注入后才提供
    [Provide(ExposedTypes = [typeof(IRepository)], WaitFor = [nameof(_config), nameof(_logger)])]
    public IRepository CreateRepository()
    {
        // 检查两个依赖的状态
        if (!IsConfigInjectionReady || _config == null)
        {
            GD.PrintErr("Config 依赖未就绪，使用默认配置");
            return new Repository(new DefaultConfig(), _logger);
        }
        
        if (!IsLoggerInjectionReady || _logger == null)
        {
            GD.PrintErr("Logger 依赖未就绪，使用 null logger");
            return new Repository(_config, new NullLogger());
        }
        
        // 安全：两个依赖都已就绪
        return new Repository(_config, _logger);
    }
    
    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
        {
            GD.Print("所有依赖都成功注入");
        }
        else
        {
            GD.PrintErr("部分依赖注入失败");
            
            // 检查具体哪个依赖失败
            if (!IsConfigInjectionReady)
            {
                GD.PrintErr("Config 注入失败");
            }
            if (!IsLoggerInjectionReady)
            {
                GD.PrintErr("Logger 注入失败");
            }
        }
    }
    
    public override partial void _Notification(int what);
}
```

#### 复杂依赖链示例

```csharp
[Host]
public partial class ServiceHost : Node, IDependenciesResolved
{
    [Inject] private IConfig? _config;
    [Inject] private ILogger? _logger;
    [Inject] private IAuthService? _authService;
    
    // 生成的就绪标志（可在代码中使用）：
    // private bool IsConfigInjectionReady { get; set; } = false;
    // private bool IsLoggerInjectionReady { get; set; } = false;
    // private bool IsAuthServiceInjectionReady { get; set; } = false;
    // private bool IsAllDependenciesReady => 
    //     IsConfigInjectionReady && IsLoggerInjectionReady && IsAuthServiceInjectionReady;
    
    // 第一层：基础服务（无依赖）
    [Provide(ExposedTypes = [typeof(IMetrics)])]
    public IMetrics CreateMetrics()
    {
        // 不依赖任何注入，立即提供
        return new MetricsService();
    }
    
    // 第二层：等待单个依赖
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public async Task<IDatabase> CreateDatabaseAsync()
    {
        // 虽然 WaitFor 了 _config，仍需检查是否成功
        if (!IsConfigInjectionReady || _config == null)
        {
            GD.PrintErr("Config 未就绪，使用内存数据库");
            return new InMemoryDatabase();
        }
        
        var connectionString = _config.DatabaseConnectionString;
        var db = new DatabaseService(connectionString);
        await db.InitializeAsync();
        return db;
    }
    
    // 第三层：等待多个依赖
    [Provide(
        ExposedTypes = [typeof(IUserRepository)], 
        WaitFor = [nameof(_logger), nameof(_config)]
    )]
    public async Task<IUserRepository> CreateUserRepositoryAsync()
    {
        // 所有 WaitFor 的依赖都已尝试解析
        // 注意：仍需处理依赖可能失败的情况
        
        var hasLogger = IsLoggerInjectionReady && _logger != null;
        if (!hasLogger)
        {
            GD.PrintErr("Logger 未就绪，将使用 null logger");
        }
        
        var hasConfig = IsConfigInjectionReady && _config != null;
        if (!hasConfig)
        {
            GD.PrintErr("Config 未就绪，使用默认配置");
        }
        
        // 通过依赖注入获取其他服务（如 IDatabase）
        // 或直接创建降级版本
        return await UserRepository.CreateAsync(
            config: hasConfig ? _config : new DefaultConfig(),
            logger: hasLogger ? _logger : new NullLogger()
        );
    }
    
    // 第四层：等待所有依赖
    [Provide(
        ExposedTypes = [typeof(ISecureRepository)],
        WaitFor = [nameof(_authService), nameof(_logger), nameof(_config)]
    )]
    public ISecureRepository CreateSecureRepository()
    {
        // 检查所有依赖的就绪状态
        if (!IsAllDependenciesReady)
        {
            // 有依赖失败，记录详情
            if (!IsAuthServiceInjectionReady)
                GD.PrintErr("AuthService 未就绪");
            if (!IsLoggerInjectionReady)
                GD.PrintErr("Logger 未就绪");
            if (!IsConfigInjectionReady)
                GD.PrintErr("Config 未就绪");
                
            // 返回降级版本或抛出异常
            throw new InvalidOperationException("无法创建 SecureRepository：关键依赖未就绪");
        }
        
        // 所有依赖都已就绪，安全创建
        return new SecureRepository(_authService!, _logger!, _config!);
    }
    
    public void OnDependenciesResolved()
    {
        if (!IsAllDependenciesReady)
        {
            GD.PrintErr("部分依赖注入失败，某些服务可能降级运行");
        }
        else
        {
            GD.Print("所有依赖成功注入");
        }
    }
    
    public override partial void _Notification(int what);
}
```

#### WaitFor 规则

1. **等待目标**：
   - ✅ 只能等待 `[Inject]` 成员（例如：`nameof(_config)`）
   - ❌ 不能等待 `[Provide]` 成员（编译时错误）
   - ❌ 不能等待不存在的成员（编译时错误）

2. **执行顺序**：
   - WaitFor 创建依赖拓扑排序
   - 无 WaitFor 的服务立即开始提供
   - 有 WaitFor 的服务在依赖解析完成后才提供

3. **失败处理**：
   - 即使依赖失败，WaitFor 也会继续
   - 使用 `IsXxxInjectionReady` 检查依赖状态
   - 在 `OnDependenciesResolved` 中处理失败情况

4. **循环检测**：
   - 循环 WaitFor 依赖在编译时检测
   - 例如：A WaitFor B, B WaitFor A（编译错误）

5. **异步支持**：
   - WaitFor 同时支持同步和异步提供者
   - 异步提供者的完成会通知后续依赖

#### 最佳实践

1. **始终检查依赖状态**
   ```csharp
   [Provide(WaitFor = [nameof(_config)])]
   public IService CreateService()
   {
       if (!IsConfigInjectionReady || _config == null)
       {
           // 处理失败情况：使用默认值、抛出异常或返回降级版本
           return new ServiceWithDefaults();
       }
       return new Service(_config);
   }
   ```

2. **实现 IDependenciesResolved**
   ```csharp
   public void OnDependenciesResolved()
   {
       if (!IsAllDependenciesReady)
       {
           // 记录或处理依赖失败
           LogDependencyStatus();
       }
   }
   
   private void LogDependencyStatus()
   {
       if (!IsConfigInjectionReady)
           GD.PrintErr("Config injection failed");
       if (!IsLoggerInjectionReady)
           GD.PrintErr("Logger injection failed");
   }
   ```

3. **避免过长的依赖链**
   - 保持依赖层级在 2-3 层
   - 过长的链会增加失败风险和调试难度

4. **考虑使用可空类型**
   ```csharp
   [Inject] private IConfig? _config;  // 使用可空类型
   
   [Provide(WaitFor = [nameof(_config)])]
   public IService CreateService()
   {
       // 编译器会提醒检查 null
       return new Service(_config ?? new DefaultConfig());
   }
   ```

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

#### 标准注入流程（无 WaitFor）

```
1. Node.EnterTree
   ↓
2. 查找父 Scope
   ↓
3. 并发解析所有 [Inject] 依赖
   │
   ├─ 依赖 A: 成功 → IsAInjectionReady = true
   ├─ 依赖 B: 成功 → IsBInjectionReady = true
   └─ 依赖 C: 失败 → IsCInjectionReady = false
   ↓
4. 所有依赖解析完成后调用 OnDependenciesResolved(false)
   ↓
5. 并发提供所有 [Provide] 服务
```

#### WaitFor 注入流程（1.1.0 新增）

```
1. Node.EnterTree
   ↓
2. 查找父 Scope
   ↓
3. 阶段 1: 并发解析所有 [Inject] 依赖（不阻塞服务提供）
   │
   ├─ 依赖 A: 成功 → IsAInjectionReady = true
   ├─ 依赖 B: 失败 → IsBInjectionReady = false
   └─ 依赖 C: 成功 → IsCInjectionReady = true
   ↓
4. 阶段 2: 提供服务（独立于依赖注入）
   │
   ├─ 服务 X (无 WaitFor): 立即提供
   │
   ├─ 服务 Y (WaitFor = [A, X]): 
   │  ├─ 监听 A 的解析
   │  ├─ 监听 X 的提供
   │  └─ 全部完成后 → 提供服务 Y
   │
   └─ 服务 Z (WaitFor = [B, Y]):
      ├─ 监听 B 的解析（失败但继续）
      ├─ 监听 Y 的提供
      └─ 全部完成后 → 提供服务 Z
         （需检查 IsBInjectionReady）
   ↓
5. 所有依赖解析完成后调用 OnDependenciesResolved(false)
```

#### 关键概念

1. **依赖解析完成 vs 依赖就绪**
   - **解析完成**：框架尝试获取依赖并调用了回调（可能成功或失败）
   - **依赖就绪**：`IsXxxInjectionReady = true` 且实例不为 null

2. **WaitFor 的行为**
   - WaitFor 等待依赖**解析完成**，不等待**解析成功**
   - 即使依赖失败，WaitFor 也会继续执行
   - 使用 `IsXxxInjectionReady` 检查依赖是否真正可用

3. **并发 vs 串行**
   - 无 WaitFor：所有操作并发执行
   - 有 WaitFor：创建依赖图，按拓扑顺序执行

#### 示例时序

```csharp
[Host]
public partial class ExampleHost : Node, IDependenciesResolved
{
    [Inject] private IConfig? _config;    // T1: 开始解析
    [Inject] private ILogger? _logger;    // T1: 开始解析（并发）
    
    [Provide(ExposedTypes = [typeof(IMetrics)])]  
    public IMetrics CreateMetrics()       // T1: 立即开始提供
    {
        return new Metrics();
    }
    
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public IDatabase CreateDatabase()     // T2: 等待 _config 解析完成
    {
        // T2 执行时机：_config 解析完成（无论成功或失败）
        if (!IsConfigInjectionReady)
        {
            return new InMemoryDatabase();
        }
        return new Database(_config!);
    }
    
    [Provide(
        ExposedTypes = [typeof(IRepository)], 
        WaitFor = [nameof(_logger), nameof(_config)]
    )]
    public IRepository CreateRepository() // T3: 等待 _logger 和 _config
    {
        // T3 执行时机：_logger 和 _config 都解析完成
        var hasLogger = IsLoggerInjectionReady && _logger != null;
        var hasConfig = IsConfigInjectionReady && _config != null;
        
        return new Repository(
            config: hasConfig ? _config : new DefaultConfig(),
            logger: hasLogger ? _logger : new NullLogger()
        );
    }
    
    public void OnDependenciesResolved()
    {
        // 在 T4 调用：所有 Inject 依赖都已解析完成
        // 此时可能部分 Provide 服务仍在异步执行
    }
}

// 时间线：
// T1: _config 开始解析, _logger 开始解析, CreateMetrics 开始提供
// T2: _config 解析完成 → CreateDatabase 开始提供
// T3: _logger 和 _config 都解析完成 → CreateRepository 开始提供  
// T4: _config 和 _logger 都解析完成 → OnDependenciesResolved 被调用
```

### Host 使用 Inject

**1.1.0 新特性**：Host 可以直接使用 `[Inject]` 注入依赖，无需同时标记为 `[User]`。

⚠️ **重要**：Host、User、Scope 三个角色**不能共存**在同一个类上。

```csharp
[Host]
public partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // Host 可以直接注入依赖（无需 [User] 特性）
    [Inject] private IConfig? _config;
    [Inject] private ISaveSystem? _saveSystem;
    
    // Host 同时提供服务
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
    
    public GameState CurrentState { get; set; }
    
    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
        {
            // 所有依赖就绪，可以安全初始化
            // IsConfigInjectionReady 和 IsSaveSystemInjectionReady 都为 true
            LoadLastSave();
        }
        else
        {
            // 部分依赖失败，使用降级模式
            if (!IsConfigInjectionReady)
                GD.PrintErr("Config 未就绪，使用默认配置");
            if (!IsSaveSystemInjectionReady)
                GD.PrintErr("SaveSystem 未就绪，无法加载存档");
        }
    }
    
    private void LoadLastSave()
    {
        // 此时可以安全使用 _config 和 _saveSystem
        var config = _config!;
        var saveSystem = _saveSystem!;
        // ...
    }
    
    public override partial void _Notification(int what);
}
```

**特点**：
- Host 可以注入依赖用于提供者方法
- Host 可以使用 WaitFor 等待注入完成
- Host 可以实现 IDependenciesResolved 接收通知
- 不需要额外的 `[User]` 特性

**在提供者中使用注入依赖**：

```csharp
[Host]
public partial class ServiceFactory : Node
{
    [Inject] private IConfig? _config;
    [Inject] private ILogger? _logger;
    
    // 等待依赖注入后再提供服务
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public async Task<IDatabase> CreateDatabaseAsync()
    {
        if (!IsConfigInjectionReady || _config == null)
        {
            GD.PrintErr("Config 未就绪，使用内存数据库");
            return new InMemoryDatabase();
        }
        
        // 安全使用注入的配置
        var db = new DatabaseService(_config.ConnectionString);
        await db.InitializeAsync();
        return db;
    }
    
    [Provide(
        ExposedTypes = [typeof(IRepository)],
        WaitFor = [nameof(_config), nameof(_logger)]
    )]
    public IRepository CreateRepository()
    {
        // 检查多个依赖
        if (!IsAllDependenciesReady)
        {
            return new RepositoryWithDefaults();
        }
        
        // 所有依赖都就绪
        return new Repository(_config!, _logger!);
    }
    
    public override partial void _Notification(int what);
}
```

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

#### `[Inject(FailureCallback = ..., ReadyCallback = ...)]`
标记字段或属性以进行依赖注入。

**参数**：
- `FailureCallback`（可选，默认: `false`）：为注入失败生成回调方法
- `ReadyCallback`（可选，默认: `false`）：为注入成功生成回调方法

**基本用法**：
```csharp
[Inject] private IService _service;
[Inject] private IConfig Config { get; set; }
```

**使用回调**：
```csharp
// 注入失败回调
[Inject(FailureCallback = true)]
private INetworkService _network;

partial void OnNetworkInjectionFailed()
{
    GD.PrintErr("网络服务不可用");
    EnableOfflineMode();
}

// 注入就绪回调
[Inject(ReadyCallback = true)]
private IGameState _gameState;

partial void OnGameStateInjectionReady(IGameState gameState)
{
    GD.Print("游戏状态就绪");
    _gameState.Initialize();
}

// 同时使用两种回调
[Inject(FailureCallback = true, ReadyCallback = true)]
private IDatabaseService _database;

partial void OnDatabaseInjectionReady(IDatabaseService database)
{
    _database.MigrateSchema();
}

partial void OnDatabaseInjectionFailed()
{
    GD.PrintErr("数据库连接失败");
    UseFallbackDataSource();
}
```

**参见**: [注入回调](#注入回调) 了解详细文档。

#### `[Modules(...)]`
定义哪些 Host 属于 Scope。

```csharp
// 构造函数参数（自 v1.3.3 起）
[Modules(typeof(Host1), typeof(Host2))]
public partial class GameScope : Node, IScope { }
```

---

### 注入回调

**1.1.1 新功能**：GodotSharpDI 提供回调机制来处理注入成功和失败事件。

#### 概述

注入回调允许你：
- 使用 `FailureCallback` **优雅处理注入失败**
- 使用 `ReadyCallback` **在注入成功后执行初始化**
- **实现降级策略**，当关键服务不可用时
- **基于注入就绪状态控制初始化顺序**

#### FailureCallback（失败回调）

当 `[Inject]` 成员标记为 `FailureCallback = true` 时，框架会生成一个 partial 方法，你可以实现它来处理注入失败：

```csharp
[User]
public partial class PlayerController : Node
{
    [Inject(FailureCallback = true)]
    private INetworkService _networkService;
    
    // 框架生成此声明：
    // partial void OnNetworkServiceInjectionFailed();
    
    // 你来实现它：
    partial void OnNetworkServiceInjectionFailed()
    {
        GD.PrintErr("网络服务注入失败");
        
        // 实现降级策略
        EnableOfflineMode();
        ShowOfflineNotification();
    }
}
```

**生成的方法签名**：
```csharp
partial void On{成员名}InjectionFailed()
```

**使用场景**：
- 需要优雅降级的关键服务
- 可能失败的网络或外部依赖
- 带有降级实现的可选服务

#### ReadyCallback（就绪回调）

当 `[Inject]` 成员标记为 `ReadyCallback = true` 时，框架会生成一个 partial 方法，在注入成功时调用：

```csharp
[User]
public partial class GameUI : Control
{
    [Inject(ReadyCallback = true)]
    private IGameState _gameState;
    
    // 框架生成此声明：
    // partial void OnGameStateInjectionReady(IGameState gameState);
    
    // 你来实现它：
    partial void OnGameStateInjectionReady(IGameState gameState)
    {
        GD.Print("游戏状态服务就绪");
        
        // 可以立即安全使用
        _gameState.Initialize();
        UpdateUI();
    }
}
```

**生成的方法签名**：
```csharp
partial void On{成员名}InjectionReady(TService value)
```

> **说明：** 参数类型与注入成员的声明类型一致（不带 `?`），因此编译器会自动在回调内部强制非空约束。

**使用场景**：
- 注入后需要立即初始化的服务
- 协调多个服务间的初始化
- 服务可用时触发 UI 更新

#### 组合使用

两种回调可以一起使用以实现全面的错误处理：

```csharp
[Host]
public partial class GameManager : Node
{
    [Inject(FailureCallback = true, ReadyCallback = true)]
    private IDatabaseService _database;
    
    partial void OnDatabaseInjectionReady(IDatabaseService database)
    {
        // 成功路径
        _database.MigrateSchema();
        LoadInitialData();
    }
    
    partial void OnDatabaseInjectionFailed()
    {
        // 失败路径
        GD.PrintErr("数据库不可用");
        UseFallbackDataSource();
    }
}
```

#### 回调执行顺序

对于单个 `[Inject]` 成员，回调执行遵循以下顺序：

1. **框架尝试注入**
2. **成功时**：调用 `OnXxxInjectionReady()`（如果 `ReadyCallback = true`）
3. **失败时**：调用 `OnXxxInjectionFailed(error)`（如果 `FailureCallback = true`）
4. **最后**：所有注入完成后调用 `IDependenciesResolved.OnDependenciesResolved(bool)`

#### IDE 支持

框架提供编译时分析和自动代码生成：

**分析器**：
- 检测 `FailureCallback = true` 但未实现回调方法的情况
- 检测 `ReadyCallback = true` 但未实现回调方法的情况
- 显示清晰的错误消息和所需的精确方法签名

**代码修复**（快速操作）：
- 在错误上按 `Ctrl+.`（VS）或 `Alt+Enter`（Rider）
- 选择"实现 {方法名} 方法"
- 框架自动生成正确的方法签名

**示例**：
```csharp
// 1. 你编写：
[Inject(ReadyCallback = true)]
private IService _service;

// 2. 分析器显示错误：
// "成员 '_service' 标记了 [Inject(ReadyCallback = true)]，
//  但未实现所需的回调方法 'OnServiceInjectionReady'"

// 3. 你按 Ctrl+. 并选择"实现 OnServiceInjectionReady 方法"

// 4. 框架生成：
partial void OnServiceInjectionReady()
{
    GD.Print("依赖注入就绪");
}

// 5. 你自定义实现
```

#### 最佳实践

**1. 对关键服务使用 FailureCallback**：
```csharp
[Inject(FailureCallback = true)]
private INetworkService _network;

partial void OnNetworkInjectionFailed()
{
    // 总是为关键服务提供降级方案
    EnableOfflineMode();
}
```

**2. 对需要初始化的服务使用 ReadyCallback**：
```csharp
[Inject(ReadyCallback = true)]
private IConfigService _config;

partial void OnConfigInjectionReady(IConfigService config)
{
    // 注入后立即初始化 — config 在此处保证非空
    ApplyConfiguration();
}
```

**3. 对重要服务同时使用两种回调**：
```csharp
[Inject(FailureCallback = true, ReadyCallback = true)]
private IDatabaseService _db;

partial void OnDbInjectionReady(IDatabaseService db)
{
    db.MigrateSchema();
}

partial void OnDbInjectionFailed()
{
    UseInMemoryDatabase();
}
```

**4. 协调多个服务**：
```csharp
[User]
public partial class GameBootstrap : Node
{
    [Inject(ReadyCallback = true)] private IConfig _config;
    [Inject(ReadyCallback = true)] private IDatabase _db;
    [Inject(ReadyCallback = true)] private IAssets _assets;
    
    private int _readyCount = 0;
    
    partial void OnConfigInjectionReady(IConfig config) => CheckAllReady();
    partial void OnDbInjectionReady(IDatabase db) => CheckAllReady();
    partial void OnAssetsInjectionReady(IAssets assets) => CheckAllReady();
    
    private void CheckAllReady()
    {
        if (++_readyCount == 3)
        {
            GD.Print("所有服务就绪，启动游戏");
            StartGame();
        }
    }
}
```

---

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
    void OnDependenciesResolved();
}
```

#### 参数说明

- 使用 **`IsAllDependenciesReady`**（生成的带 `[MemberNotNull]` 属性）检查所有注入是否成功：
  - `true`：所有 `[Inject]` 成员都成功注入
  - `false`：至少有一个 `[Inject]` 成员注入失败

#### 生成的辅助属性

框架为每个 `[Inject]` 成员自动生成就绪标志，这些属性在生成的 `*.DI.g.cs` 文件中：

```csharp
// 用户代码
[Host]
public partial class MyHost : Node, IDependenciesResolved
{
    [Inject] private IConfig? _config;
    [Inject] private ILogger? _logger;
    
    // ... 其他代码
}

// 生成的代码（在 MyHost.DI.Host.g.cs 中）
partial class MyHost
{
    // 为每个 Inject 成员生成的就绪标志
    [MemberNotNullWhen(true, nameof(_config))]
    private bool IsConfigInjectionReady { get; set; } = false;
    
    [MemberNotNullWhen(true, nameof(_logger))]
    private bool IsLoggerInjectionReady { get; set; } = false;
    
    // 综合就绪标志
    [MemberNotNullWhen(true, nameof(_config))]
    [MemberNotNullWhen(true, nameof(_logger))]
    private bool IsAllDependenciesReady => 
        IsConfigInjectionReady == true && IsLoggerInjectionReady == true;
    
    // 未解析依赖追踪
    private readonly HashSet<Type> __unresolvedDependencies = new()
    {
        typeof(IConfig),
        typeof(ILogger),
    };
    
    // 依赖解析回调
    private void OnDependencyResolved<T>()
    {
        __unresolvedDependencies.Remove(typeof(T));
        if (__unresolvedDependencies.Count == 0)
        {
            ((IDependenciesResolved)this).OnDependenciesResolved(IsAllDependenciesReady);
        }
    }
}
```

#### 使用示例

##### 基础用法

```csharp
[Host]
public partial class GameManager : Node, IGameState, IDependenciesResolved
{
    [Inject] private IPlayerStats? _playerStats;
    [Inject] private IGameConfig? _config;
    
    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
        {
            GD.Print("所有依赖就绪，游戏可以开始");
            StartGame();
        }
        else
        {
            GD.PrintErr("依赖注入失败，无法启动游戏");
            ShowErrorScreen();
        }
    }
    
    public override partial void _Notification(int what);
}
```

##### 细粒度状态检查

```csharp
[User]
public partial class PlayerUI : Control, IDependenciesResolved
{
    [Inject] private IPlayerStats? _stats;
    [Inject] private IInventory? _inventory;
    [Inject] private IAchievements? _achievements;
    
    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
        {
            // 所有依赖都成功，启用完整功能
            EnableAllFeatures();
        }
        else
        {
            // 部分依赖失败，启用降级模式
            EnableDegradedMode();
            
            // 检查具体哪些依赖可用
            if (IsStatsInjectionReady)
            {
                UpdateStatsDisplay(_stats!);  // ! 操作符安全，因为 IsStatsInjectionReady = true
            }
            else
            {
                GD.PrintErr("Stats 服务不可用");
            }
            
            if (IsInventoryInjectionReady)
            {
                UpdateInventoryDisplay(_inventory!);
            }
            else
            {
                HideInventoryPanel();
            }
            
            if (IsAchievementsInjectionReady)
            {
                ShowAchievements(_achievements!);
            }
            else
            {
                DisableAchievementsButton();
            }
        }
    }
    
    private void EnableAllFeatures()
    {
        // 所有功能都可用
        UpdateStatsDisplay(_stats!);
        UpdateInventoryDisplay(_inventory!);
        ShowAchievements(_achievements!);
    }
    
    private void EnableDegradedMode()
    {
        // 部分功能降级运行
        GD.Print("UI 运行在降级模式");
    }
    
    public override partial void _Notification(int what);
}
```

##### 结合 WaitFor 使用

```csharp
[Host]
public partial class DataManager : Node, IDependenciesResolved
{
    [Inject] private IConfig? _config;
    [Inject] private ILogger? _logger;
    
    // 生成的属性可用于检查：
    // private bool IsConfigInjectionReady { get; set; }
    // private bool IsLoggerInjectionReady { get; set; }
    
    // 等待 _config 注入后才提供数据库服务
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public async Task<IDatabase> CreateDatabaseAsync()
    {
        // WaitFor 保证 _config 的解析已尝试，但需要检查是否成功
        if (!IsConfigInjectionReady || _config == null)
        {
            // Config 注入失败，使用内存数据库
            GD.PrintErr("Config 未就绪，使用内存数据库");
            return new InMemoryDatabase();
        }
        
        // Config 成功注入，使用配置的数据库
        var db = new DatabaseService(_config.ConnectionString);
        await db.InitializeAsync();
        return db;
    }
    
    public void OnDependenciesResolved()
    {
        if (!IsAllDependenciesReady)
        {
            GD.PrintErr("部分依赖注入失败：");
            
            if (!IsConfigInjectionReady)
                GD.PrintErr("  - Config 注入失败，将使用默认配置");
                
            if (!IsLoggerInjectionReady)
                GD.PrintErr("  - Logger 注入失败，日志功能将被禁用");
        }
        else
        {
            GD.Print("所有依赖成功注入");
        }
    }
    
    public override partial void _Notification(int what);
}
```

#### 调用时机

`OnDependenciesResolved` 在以下时机被调用：

1. **所有 `[Inject]` 依赖都已尝试解析**（成功或失败）
2. **在节点的 `_Notification(NotificationEnterTree)` 之后**
3. **在任何 `[Provide]` 服务被实际使用之前**

#### 最佳实践

1. **总是检查 `IsAllDependenciesReady`**
   ```csharp
   public void OnDependenciesResolved()
   {
       if (IsAllDependenciesReady)
       {
           // 正常流程
       }
       else
       {
           // 降级或错误处理
       }
   }
   ```

2. **使用生成的 `IsXxxInjectionReady` 进行细粒度检查**
   ```csharp
   if (!IsConfigInjectionReady)
   {
       GD.PrintErr("Config 注入失败");
       // 使用默认配置
   }
   ```

3. **结合空值检查提高安全性**
   ```csharp
   if (IsStatsInjectionReady && _stats != null)
   {
       // 安全使用 _stats
       DisplayStats(_stats);
   }
   ```

4. **记录依赖状态用于调试**
   ```csharp
   public void OnDependenciesResolved()
   {
       GD.Print($"Dependencies ready: {IsAllDependenciesReady}");
       GD.Print($"  Config: {IsConfigInjectionReady}");
       GD.Print($"  Logger: {IsLoggerInjectionReady}");
   }
   ```

#### 注意事项

⚠️ **重要**：
- `IsXxxInjectionReady` 属性和 `IsAllDependenciesReady` 属性会在有 `[Inject]` 成员时生成，无论是否实现了 `IDependenciesResolved` 接口
- 这些属性是私有的，只能在类内部使用
- **`[MemberNotNullWhen(true, ...)]` 特性的作用**：当 `IsXxxInjectionReady` 为 `true` 时，编译器会确保对应的可空成员不为 `null`，这意味着在检查 `IsXxxInjectionReady` 后，可以安全地使用非空断言运算符（`!`）或直接访问成员，无需额外的 null 检查
- 即使依赖注入失败，`OnDependenciesResolved` 也会被调用（参数为 `false`）

**使用 `IsXxxInjectionReady` 的好处**：

```csharp
[Host]
public partial class MyHost : Node
{
    [Inject] private IConfig? _config;
    
    // 生成：
    // [MemberNotNullWhen(true, nameof(_config))]
    // private bool IsConfigInjectionReady { get; set; }
    
    [Provide(ExposedTypes = [typeof(IService)], WaitFor = [nameof(_config)])]
    public IService CreateService()
    {
        if (IsConfigInjectionReady)
        {
            // ✅ 编译器知道 _config 不为 null
            // 可以直接使用，无需 null 检查
            return new Service(_config.ConnectionString);
            
            // 或者使用非空断言
            return new Service(_config!.ConnectionString);
        }
        
        // _config 可能为 null 的处理
        return new ServiceWithDefaults();
    }
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

**编译时检测**：框架检测两类 WaitFor 循环：

- **GDI_D010**——同一 Host 内部的 WaitFor 循环
- **GDI_D011**——跨不同 Host 的 WaitFor 循环（*1.2.0 新增*）

**跨 Host 死锁示例（GDI_D011）**：

```csharp
// ❌ HostA 提供 IServiceA，但 WaitFor 等待 IServiceB 注入
[Host]
public partial class HostA : Node
{
    [Inject] private IServiceB? _serviceB;
    
    [Provide(ExposedTypes = [typeof(IServiceA)], WaitFor = [nameof(_serviceB)])]
    public IServiceA CreateA() => new ServiceA(_serviceB);
    
    public override partial void _Notification(int what);
}

// ❌ HostB 提供 IServiceB，但 WaitFor 等待 IServiceA → 跨 Host 死锁 → GDI_D011
[Host]
public partial class HostB : Node
{
    [Inject] private IServiceA? _serviceA;
    
    [Provide(ExposedTypes = [typeof(IServiceB)], WaitFor = [nameof(_serviceA)])]
    public IServiceB CreateB() => new ServiceB(_serviceA);
    
    public override partial void _Notification(int what);
}
```

**解决方案**：只保留单向等待：

```csharp
// ✅ 正确做法 - 只有一个方向等待
[Host]
public partial class HostA : Node
{
    [Provide(ExposedTypes = [typeof(IServiceA)])]
    public IServiceA CreateA() => new ServiceA();
    
    public override partial void _Notification(int what);
}

[Host]
public partial class HostB : Node
{
    [Inject] private IServiceA? _serviceA;
    
    [Provide(ExposedTypes = [typeof(IServiceB)], WaitFor = [nameof(_serviceA)])]
    public IServiceB CreateB()
    {
        if (_serviceA == null) return new ServiceB(new NullServiceA());
        return new ServiceB(_serviceA);
    }
    
    public override partial void _Notification(int what);
}
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

### Host 注入和提供服务

Host 可以同时注入依赖和提供服务，无需 `[User]` 特性：

```csharp
[Host]
public partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // Host 直接注入依赖
    [Inject] private IConfig? _config;
    [Inject] private ISaveSystem? _saveSystem;
    
    // Host 提供服务
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
    
    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
        {
            InitializeWithDependencies();
        }
    }
    
    private void InitializeWithDependencies()
    {
        // IsConfigInjectionReady 和 IsSaveSystemInjectionReady 都为 true
        // 可以安全使用 _config 和 _saveSystem
        var config = _config!;
        var saveSystem = _saveSystem!;
        // ...
    }
    
    public override partial void _Notification(int what);
}
```

**角色独占规则**：

| 角色 | 可以组合 | 不可以组合 |
|------|---------|-----------|
| **Host** | 可单独使用 | 不能与 User 或 Scope 共存 |
| **User** | 可单独使用 | 不能与 Host 或 Scope 共存 |
| **Scope** | 必须单独使用 | 不能与任何其他角色共存 |

**Host 的能力**：
- ✅ 使用 `[Provide]` 提供服务
- ✅ 使用 `[Inject]` 注入依赖
- ✅ 使用 `WaitFor` 等待依赖
- ✅ 实现 `IDependenciesResolved`
- ❌ 不能同时标记 `[User]`
- ❌ 不能同时标记 `[Scope]`

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
    
    public void OnDependenciesResolved() { }
    
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

1. 如果您忘记添加此方法，您会看到 **GDI_C060** 错误
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
