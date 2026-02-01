# GodotSharpDI

<img src="icon.png" style="zoom:50%;" />

<p align="left"> <a href="README.md">English</a> </p>

一个专为 Godot 引擎设计的编译时依赖注入框架，通过 C# Source Generator 实现零反射、高性能的 DI 支持。

[![NuGet Version](https://img.shields.io/nuget/v/GodotSharpDI.svg?style=flat)](https://www.nuget.org/packages/GodotSharpDI/)

## 目录

- [设计理念](#设计理念)
- [安装](#安装)
- [快速开始](#快速开始)
  - [1. 定义服务](#1-定义服务)
  - [2. 定义 Scope](#2-定义-scope)
  - [3. 定义 Host](#3-定义-host)
  - [4. 定义 User](#4-定义-user)
  - [5. 场景树结构](#5-场景树结构)
- [核心概念](#核心概念)
  - [四种角色类型](#四种角色类型)
  - [服务生命周期](#服务生命周期)
- [角色详解](#角色详解)
  - [Singleton 服务](#singleton-服务)
  - [Host (宿主)](#host-宿主)
  - [User (消费者)](#user-消费者)
  - [Scope (容器)](#scope-容器)
- [生命周期管理](#生命周期管理)
  - [Singleton 生命周期](#singleton-生命周期)
  - [Scope 层级](#scope-层级)
  - [依赖注入时序](#依赖注入时序)
  - [Host + User 与循环依赖](#host--user-与循环依赖)
- [类型约束](#类型约束)
  - [角色类型约束](#角色类型约束-1)
  - [注入类型约束](#注入类型约束)
  - [服务实现类型约束](#服务实现类型约束)
  - [暴露类型约束](#暴露类型约束)
  - [其他约束](#其他约束)
- [API 参考](#api-参考)
  - [特性 (Attributes)](#特性-attributes)
  - [接口 (Interfaces)](#接口-interfaces)
  - [生成的代码](#生成的代码)
  - [场景树集成](#场景树集成)
- [最佳实践](#最佳实践)
  - [Scope 粒度设计](#scope-粒度设计)
  - [服务释放](#服务释放)
  - [避免循环依赖](#避免循环依赖)
  - [接口优先原则](#接口优先原则)
  - [Host + User 组合使用](#host--user-组合使用)
- [诊断代码](#诊断代码)
- [许可证](#许可证)
- [Todo List](#todo-list)

---

## 设计理念

GodotSharpDI 的核心设计理念是**将 Godot 的场景树生命周期与传统 DI 容器模式融合**：

- **场景树即容器层级**：利用 Godot 的场景树结构实现作用域 (Scope) 层级
- **Node 生命周期集成**：服务的创建和销毁与 Node 的进入/退出场景树事件绑定
- **编译时安全**：通过 Source Generator 在编译期完成依赖分析和代码生成，提供完整的编译时错误检查

---

## 安装软件包

```xml
<PackageReference Include="GodotSharpDI" Version="x.x.x" />
```
⚠️ **确保项目中同时添加了 GodotSharp 软件包** ：生成的代码依赖 Godot.Node 和 Godot.GD 。

---

## 快速开始

### 1. 定义服务

```csharp
// 定义服务接口
public interface IPlayerStats
{
    int Health { get; set; }
    int Mana { get; set; }
}

// 实现服务 (Singleton 生命周期)
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;
}
```

### 2. 定义 Scope

```csharp
[Modules(
    Services = [typeof(PlayerStatsService)],
    Hosts = [typeof(GameManager)]
)]
public partial class GameScope : Node, IScope
{
    // 框架自动生成 IScope 实现
}
```

### 3. 定义 Host

```csharp
[Host]
public partial class GameManager : Node, IGameState
{
    // 将自己暴露为 IGameState 服务
    [Singleton(typeof(IGameState))]
    private IGameState Self => this;
    
    public GameState CurrentState { get; set; }
}
```

### 4. 定义 User

```csharp
[User]
public partial class PlayerUI : Control, IServicesReady
{
    [Inject] private IPlayerStats _stats;
    [Inject] private IGameState _gameState;
    
    // 所有依赖注入完成后调用
    public void OnServicesReady()
    {
        UpdateUI();
    }
}
```

### 5. 场景树结构

```
GameScope (IScope)
├── GameManager (Host)
├── Player
│   └── PlayerUI (User) ← 自动接收注入
└── Enemies
```

---

## 核心概念

### 四种角色类型

| 角色 | 说明 | 约束 |
|------|------|------|
| **Singleton 服务** | 纯逻辑服务，在 Scope 内唯一，由 Scope 创建和管理，Scope 销毁时释放 | 必须是非 Node 的 class |
| **Host** | 场景级资源提供者，将 Node 资源桥接到 DI 世界 | 必须是 Node |
| **User** | 依赖消费者，接收注入 | 必须是 Node |
| **Scope** | DI 容器，管理服务生命周期 | 必须是 Node，实现 IScope |

---

## 角色详解

### Singleton 服务

#### 职责

标记为 [Singleton] 的类型是纯逻辑服务，封装业务逻辑和数据处理，**不依赖 Godot Node 系统**。

#### 约束

| 约束项 | 要求 | 原因 |
|--------|------|------|
| 类型 | 必须是 class | 需要实例化 |
| 继承 | 不能是 Node | Node 生命周期由 Godot 控制,与 DI 容器冲突 |
| 修饰符 | 不能是 abstract 或 static | 需要实例化 |
| 泛型 | 不能是开放泛型 | 需要具体类型来实例化 |
| 声明 | 必须是 partial | 源生成器需要扩展类 |

#### 生命周期标记

```csharp
// Singleton: Scope 内唯一实例
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats { }
```

#### 构造函数注入

Singleton 服务通过构造函数注入依赖：

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

**构造函数选择规则**:

1. 使用标记为 `[InjectConstructor]` 的构造函数
2. 只有一个构造函数时则作为默认构造函数，无论是否标记为 `[InjectConstructor]` 
3. 如果有多个构造函数，必须指定唯一的 `[InjectConstructor]`

```csharp
[Singleton(typeof(IService))]
public partial class MyService : IService
{
    // 多个构造函数时,必须指定
    [InjectConstructor]
    public MyService(IDep1 dep1) { }
    
    public MyService(IDep1 dep1, IDep2 dep2) { }
}
```

#### 暴露类型

通过 [Singleton] 参数指定暴露的服务类型：

```csharp
// 暴露单个接口
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats { }

// 暴露多个接口
[Singleton(typeof(IReader), typeof(IWriter))]
public partial class FileService : IReader, IWriter { }

// 不指定参数时,暴露类本身 (不推荐)
[Singleton]  // 暴露 ConfigService 类型
public partial class ConfigService { }
```

> ⚠️ **最佳实践**: 始终暴露接口而非具体类，以保持松耦合和可测试性。

---

### Host (宿主)

#### 职责

Host 是 Godot Node 系统与 DI 系统之间的桥梁，它将 Node 管理的资源暴露为可注入的服务。

#### 约束

| 约束项 | 要求 | 原因 |
|--------|------|------|
| 类型 | 必须是 class | 需要实例化 |
| 继承 | 必须是 Node | 需要接入场景树生命周期 |
| 声明 | 必须是 partial | 源生成器需要扩展类 |

#### 典型使用模式

**模式 1： Host 暴露自身**

```csharp
[Host]
public partial class ChunkManager : Node3D, IChunkGetter, IChunkLoader
{
    [Singleton(typeof(IChunkGetter), typeof(IChunkLoader))]
    private ChunkManager Self => this;
    
    // Node 管理的资源
    private Dictionary<Vector3I, Chunk> _chunks = new();
    
    // 实现接口
    public Chunk GetChunk(Vector3I pos) => _chunks.GetValueOrDefault(pos);
    public void LoadChunk(Vector3I pos) { /* ... */ }
}
```

这是 Host 最典型的使用方式：Node 自身实现服务接口，并将自己暴露给 DI 系统。

**模式 2： Host 持有并暴露其他对象**

```csharp
[Host]
public partial class WorldManager : Node
{
    [Singleton(typeof(IWorldConfig))]
    private WorldConfig _config = new();
    
    [Singleton(typeof(IWorldState))]
    private WorldState _state = new();
}

public class WorldConfig : IWorldConfig { /* ... */ }
public class WorldState : IWorldState { /* ... */ }
```

Host 可以持有和管理其他对象，并将它们暴露为服务。**这些对象的生命周期由 Host 控制。**

#### Host 成员约束

| 约束项 | 要求 | 原因 |
|--------|------|------|
| 成员类型 | 不能是已标记为 Service 的类型 | 避免生命周期冲突 |
| static 成员 | 不允许 | 需要实例级别的服务 |
| 属性 | 必须有 getter | 需要读取值来注册服务 |

```csharp
// ❌ 错误: 标记为 [Singleton]的类型只能由 Scope 持有
[Singleton(typeof(IConfig))]
public partial class ConfigService : IConfig { }

[Host]
public partial class BadHost : Node
{
    [Singleton(typeof(IConfig))]
    private ConfigService _config = new();  // 编译错误 GDI_M050
}

// ✅ 正确: 使用注入而非持有
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

### User (消费者)

#### 职责

User 是依赖消费者，通过字段或属性注入接收服务依赖。

#### 约束

| 约束项 | 要求 | 原因 |
|--------|------|------|
| 类型 | 必须是 class | 需要实例化 |
| 继承 | 必须是 Node | 需要接入场景树生命周期 |
| 声明 | 必须是 partial | 源生成器需要扩展类 |

#### User 自动注入依赖

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

#### Inject 成员约束

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
    [Inject] private static IService _static;     // ❌ 错误
}
```

#### IServicesReady 接口

实现 `IServicesReady` 接口可以在所有依赖注入完成后收到通知:

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

### Scope (容器)

#### 职责

Scope 是 DI 容器，负责：

1. 创建和管理 Singleton 服务实例
2. 收集 Host 所提供的服务实例
3. 处理依赖解析请求
4. 管理自己创建的服务实例的生命周期

#### 约束

| 约束项 | 要求 | 原因 |
|--------|------|------|
| 类型 | 必须是 class | 需要实例化 |
| 继承 | 必须是 Node | 利用场景树实现 Scope 层级 |
| 接口 | 必须实现 IScope | 框架识别标志 |
| 特性 | 必须有 [Modules] | 声明管理的服务 |
| 声明 | 必须是 partial | 源生成器需要扩展类 |

#### 定义 Scope

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

**Modules 参数说明**:

| 参数 | 说明 | 约束 |
|------|------|------|
| `Services` | Scope 创建和管理的 服务类型列表 | 必须是服务（有 [Singleton]） |
| `Hosts` | Scope 期望接收的 Host 类型列表 | 必须是 Host（有 [Host]） |

#### Scope 层级

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

**依赖解析规则**:

1. 首先在当前 Scope 查找
2. 如果未找到且服务类型不属于当前 Scope，向父 Scope 查找
3. 递归直到根 Scope 或找到服务

#### Scope 生命周期事件

| 事件 | 触发时机 | 行为 |
|------|----------|------|
| `NotificationReady` | Node 准备就绪 | 创建所有 Singleton Service |
| `NotificationPredelete` | Node 即将删除 | 释放所有 Service (调用 IDisposable.Dispose) |

---

## 生命周期管理

### Singleton 生命周期

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

**生命周期时序**:

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
 ↓ 调用 IDisposable.Dispose() (如果实现)
 ↓ 从容器移除
 ↓
Scope 删除完成
```

---

### Scope 层级

#### 层级结构

Scope 通过 Godot 场景树形成自然的层级结构：

```
Application
└── RootScope                 ← 根 Scope
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

#### 服务解析规则

当 User 请求依赖时:

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

#### 服务可见性

| 服务位置 | 可见范围 |
|----------|----------|
| 根 Scope | 所有子 Scope |
| 父 Scope | 所有子孙 Scope |
| 子 Scope | 仅该 Scope 及其子孙 |

```
RootScope
├── IGlobalConfig    ← 对所有 Scope 可见
│
└── LevelScope
 ├── ILevelConfig    ← 对 LevelScope 及其子 Scope 可见，
 │                     对 RootScope 不可见
 │
 └── PlayerScope
     └── IInventory  ← 仅对 PlayerScope 可见
```

---

### 依赖注入时序

#### User 的注入时序

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

#### Host 的服务注册时序

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

#### 完整时序示例

假设场景结构:

```
GameScope
├── GameManager (Host， 提供 IGameState)
└── PlayerUI (User， 需要 IGameState)
```

执行时序:

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

### Host + User 与循环依赖

在 GodotSharpDI 中，一个类型可以同时标记为 `[Host, User]`,即既提供服务又消费服务。为了避免误判循环依赖,需要明确 Host 与 User 在生命周期和注入时序上的区别。

#### Host 与 User 的注入时序差异

**Host (服务提供者)**

- 在 **EnterTree** 阶段注册其 `[Singleton]` 成员提供的服务
- 注册服务时**不会触发任何依赖注入**
- 不会触发自身的 User 注入
- 不会触发其他 User 的注入

**User (服务消费者)**

- 在 **EnterTree** 阶段附着到最近的 Scope
- 立即对所有 `[Inject]` 成员发起依赖解析
- 如果服务尚未注册,则加入等待队列
- 在服务注册或 Scope Ready 时被回调注入
- 所有依赖注入完成后触发 `OnServicesReady()`

**结论**

> **Host 的服务注册阶段不参与依赖注入链。**
> **User 的依赖注入只在 Node 进入场景树后、或服务注册完成后触发。**

这条规则保证了 Host+User 不会因为"自提供、自消费"而形成循环依赖。

#### 示例 1: Host+User 自注入不是循环依赖

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

**为什么不是循环依赖?**

1. Host 注册 `Self` 时**不会触发** `_self` **的注入**
2. `_self` 的注入发生在 User 注入阶段 (EnterTree → AttachToScope)
3. 此时 `IMyService` 已经注册,因此注入成功
4. 整个过程没有构造函数链路,也没有形成依赖闭环

**结论**

> **Host+User 自注入是合法的,不属于循环依赖。**

#### 示例 2: Host 提供服务 + 自身消费另一个 Service 也不是循环依赖

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

- `HostUser` (Host) 提供 `IServiceB`
- `ServiceA` 构造函数依赖 `IServiceB` → 注入 `HostUser`
- `HostUser` (User) 依赖 `IServiceA`

**为什么不是循环依赖?**

1. HostUser 注册 `IServiceB` 时**不会触发** `_serviceA` **的注入**
2. ServiceA 构造函数解析 `IServiceB` → 得到 HostUser
3. ServiceA 构造完成后,HostUser 的 `_serviceA` 在 User 注入阶段被赋值
4. 整个链路中没有构造函数环路

**依赖图如下**:

```
ServiceA → IServiceB (HostUser)
HostUser(User) → IServiceA
```

这是一个"菱形依赖",不是循环。

**结论**

> **Host 提供服务 + 自身作为 User 消费其他服务是合法的,不属于循环依赖。**

#### 循环依赖检测的适用范围

GodotSharpDI 的循环依赖检测仅针对:

- **Service → Service 的构造函数依赖链**

不包括:

- User 的 `[Inject]` 成员
- Host 的 `[Singleton]` 成员
- Host+User 的自注入
- Host 与 User 之间的交叉依赖

原因:

> **User 注入发生在所有 Service 构造完成之后,不参与构造时的依赖闭环。**

因此,只有以下情况会被判定为循环依赖:

```csharp
[Singleton(typeof(IA))]
class A : IA { public A(IB b) {} }

[Singleton(typeof(IB))]
class B : IB { public B(IA a) {} }
```

#### 总结

| 情况 | 是否循环依赖 | 原因 |
|------|-------------|------|
| Host+User 自注入 | ❌ | Host 注册不触发注入,User 注入在之后 |
| Host 提供服务 + 自身作为 User 注入 | ❌ | 注入时序分离,不形成构造函数环 |
| Service ↔ Service 构造函数互相依赖 | ✔️ | 构造函数闭环 |

最终规则:

> **只要依赖链不在 Service 构造函数之间形成闭环，就不是循环依赖。Host+User 的注入时序天然避免构造函数循环。**

---

## 类型约束

### 角色类型约束

| 角色 | 必须是 class | 是否 Node | 生命周期标记 | 可作为 Service | 可被注入 | 可暴露类型 |
|------|-------------|-----------|--------------|----------------|----------|------------|
| **Service** | ✅ | ❌ 禁止 | ✅ 必须 | ✅ 是 | ✅ 是 | ✅ 必须 |
| **Host** | ✅ | ✅ 必须 | ❌ 禁止 | ❌ 否 | ❌ 否 | ✅ 通过成员 |
| **User** | ✅ | ✅ 必须 | ❌ 禁止 | ❌ 否 | ❌ 否 | ❌ 否 |
| **Scope** | ✅ | ✅ 必须 | ❌ 禁止 | ❌ 否 | ❌ 否 | ❌ 否 |

#### Service 详细约束

| 约束 | 要求 | 原因 |
|------|------|------|
| 类型 | class | 需要实例化 |
| 继承 | 不能是 Node | Node 生命周期由 Godot 控制 |
| 修饰符 | 不能是 abstract | 需要实例化 |
| 修饰符 | 不能是 static | 需要实例化 |
| 泛型 | 不能是开放泛型 | 需要具体类型 |
| 声明 | 必须是 partial | 源生成器需要扩展 |

#### Host 详细约束

| 约束 | 要求 | 原因 |
|------|------|------|
| 类型 | class | 需要实例化 |
| 继承 | 必须是 Node | 需要场景树生命周期 |
| 声明 | 必须是 partial | 源生成器需要扩展 |

#### User 详细约束

| 约束 | 要求 | 原因 |
|------|------|------|
| 类型 | class | 需要实例化 |
| 继承 | 必须是 Node | 需要场景树生命周期 |
| 声明 | 必须是 partial | 源生成器需要扩展 |

#### Scope 详细约束

| 约束 | 要求 | 原因 |
|------|------|------|
| 类型 | class | 需要实例化 |
| 继承 | 必须是 Node | 利用场景树实现层级 |
| 接口 | 必须实现 IScope | 框架识别标志 |
| 特性 | 必须有 [Modules] | 声明管理的服务 |
| 声明 | 必须是 partial | 源生成器需要扩展 |

---

### 注入类型约束

可以作为 `[Inject]` 成员类型或 Service 构造函数参数类型的类型。

| 类型 | 是否允许 | 说明 |
|------|----------|------|
| interface | ✅ | **推荐方式** |
| class (普通) | ✅ | 允许但不如接口灵活 |
| Node | ❌ | 生命周期由 Godot 控制 |
| Host | ❌ | Host 不是服务 |
| User | ❌ | User 不是服务 |
| Scope | ❌ | Scope 不是服务 |
| abstract class | ❌ | 无法实例化 |
| static class | ❌ | 无法实例化 |
| 开放泛型 | ❌ | 无法实例化 |
| array | ❌ | 不支持 |
| pointer | ❌ | 不支持 |
| delegate | ❌ | 不支持 |
| dynamic | ❌ | 无法静态分析 |

**代码示例**:

```csharp
[User]
public partial class MyComponent : Node
{
    [Inject] private IService _service;           // ✅ 接口
    [Inject] private ConcreteClass _concrete;     // ✅ 普通类 (不推荐)
    [Inject] private Node _node;                  // ❌ Node
    [Inject] private MyHost _host;                // ❌ Host 类型
    [Inject] private MyUser _user;                // ❌ User 类型
    [Inject] private MyScope _scope;              // ❌ Scope 类型
    [Inject] private AbstractClass _abstract;     // ❌ 抽象类
}
```

---

### 服务实现类型约束

标记为 `[Singleton]` 的类型。

| 类型 | 是否允许 | 说明 |
|------|----------|------|
| class | ✅ | 必须是 class |
| sealed class | ✅ | 推荐 |
| abstract class | ❌ | 无法实例化 |
| static class | ❌ | 无法实例化 |
| Node | ❌ | 生命周期冲突 |
| Host | ❌ | Host 不是 Service |
| User | ❌ | User 不是 Service |
| Scope | ❌ | Scope 不是 Service |
| interface | ❌ | 不能作为实现类型 |
| 开放泛型 | ❌ | 无法实例化 |
| struct | ❌ | 不支持 |

---

### 暴露类型约束

可以在 `[Singleton(typeof(...))]` 中指定的类型。

| 类型 | 是否允许 | 说明 |
|------|----------|------|
| interface | ✅ | **强烈推荐** |
| concrete class | ✅ | 允许 (会产生 Warning) |
| sealed class | ✅ | 允许 |
| abstract class | ❌ | 无意义 |
| Node | ❌ | 不允许 |
| Host/User/Scope | ❌ | 不允许 |
| 开放泛型 | ❌ | 不允许 |

**最佳实践**:

```csharp
// ✅ 推荐: 暴露接口
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats { }

// ⚠️ 允许但产生 Warning: 暴露具体类
[Singleton(typeof(GameConfig))]
public partial class GameConfig
{
    public string GameName { get; set; }
}

// ✅ 暴露多个接口
[Singleton(typeof(IReader), typeof(IWriter))]
public partial class FileService : IReader, IWriter { }
```

---

### 其他约束

#### User Inject 成员约束

| 约束 | 要求 | 诊断代码 |
|------|------|----------|
| 成员类型 | 必须是有效的 Inject Type | GDI_M040 |
| 成员类型 | 不能是 Host 类型 | GDI_M041 |
| 成员类型 | 不能是 User 类型 | GDI_M042 |
| 成员类型 | 不能是 Scope 类型 | GDI_M043 |
| static | 不允许 | GDI_M044 |
| 字段 | 允许 (不能是 readonly) | GDI_M020 |
| 属性 | 必须有 setter | GDI_M020 |

#### Host Singleton 成员约束

| 约束 | 要求 | 诊断代码 |
|------|------|----------|
| 成员类型 | 可以是任意类型 (包括 Host 自身) | - |
| 成员类型 | 不能是标记为 Service 的类型 | GDI_M050 |
| 暴露类型 | 必须是有效的 Exposed Type | - |
| 暴露类型 | 推荐使用 interface | GDI_M060 (Warning) |
| static | 不允许 | GDI_M045 |
| 字段 | 允许 | - |
| 属性 | 必须有 getter | GDI_M030 |

#### 构造函数约束

| 约束 | 要求 | 诊断代码 |
|------|------|----------|
| 可见性 | 至少有一个 public 构造函数 | GDI_S010 |
| 多构造函数 | 必须用 [InjectConstructor] 指定 | GDI_S011 |
| 参数类型 | 必须是有效的 Inject Type | GDI_S020 |

---

## API 参考

### 特性 (Attributes)

#### SingletonAttribute

标记一个类为 Singleton 生命周期的服务，或标记 Host 成员为暴露的服务。

```csharp
namespace GodotSharpDI.Abstractions;

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

**用法**:

```csharp
// 在类上 (Service)
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats { }

// 在成员上 (Host)
[Host]
public partial class GameManager : Node, IGameState
{
    [Singleton(typeof(IGameState))]
    private IGameState Self => this;
}
```

---

#### HostAttribute

标记一个类为 Host (服务提供者)。

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class HostAttribute : Attribute { }
```

---

#### UserAttribute

标记一个类为 User (服务消费者)。

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class UserAttribute : Attribute { }
```

---

#### InjectAttribute

标记一个字段或属性为注入目标。

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class InjectAttribute : Attribute { }
```

**用法**:

```csharp
[User]
public partial class MyComponent : Node
{
    [Inject] private IService _service;           // 字段
    [Inject] public IConfig Config { get; set; }  // 属性 (需要 setter)
}
```

---

#### InjectConstructorAttribute

指定 Service 使用的构造函数。

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Constructor)]
public sealed class InjectConstructorAttribute : Attribute { }
```

**用法**:

```csharp
[Singleton(typeof(IService))]
public partial class MyService : IService
{
    [InjectConstructor]
    public MyService(IDep1 dep1) { }
    
    public MyService(IDep1 dep1, IDep2 dep2) { }
}
```

---

#### ModulesAttribute

声明 Scope 管理的服务和期望的 Host。

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ModulesAttribute : Attribute
{
    public Type[] Services { get; set; }
    public Type[] Hosts { get; set; }
}
```

**参数**:

| 参数 | 说明 |
|------|------|
| `Services` | Scope 创建和管理的 Service 类型列表 |
| `Hosts` | Scope 期望接收的 Host 类型列表 |

**用法**:

```csharp
[Modules(
    Services = [typeof(PlayerStatsService), typeof(CombatSystem)],
    Hosts = [typeof(GameManager), typeof(WorldManager)]
)]
public partial class GameScope : Node, IScope { }
```

---

### 接口 (Interfaces)

#### IScope

DI 容器接口。

```csharp
namespace GodotSharpDI.Abstractions;

public interface IScope
{
    void RegisterService<T>(T instance) where T : notnull;
    void UnregisterService<T>() where T : notnull;
    void ResolveDependency<T>(Action<T> onResolved) where T : notnull;
}
```

**方法**:

- `RegisterService<T>`: 注册服务实例（由框架自动调用，手动调用会触发 GDI_U001）
- `UnregisterService<T>`: 注销服务（由框架自动调用，手动调用会触发 GDI_U001）
- `ResolveDependency<T>`: 解析依赖：如果服务已注册，立即回调；否则加入等待队列（由框架自动调用，手动调用会触发 GDI_U001）

---

#### IServicesReady

服务就绪通知接口。

```csharp
namespace GodotSharpDI.Abstractions;

public interface IServicesReady
{
    void OnServicesReady();
}
```

**用法**:

```csharp
[User]
public partial class MyComponent : Node, IServicesReady
{
    [Inject] private IServiceA _a;
    [Inject] private IServiceB _b;
    
    public void OnServicesReady()
    {
        // 所有依赖已注入,安全使用
        _a.Initialize();
        _b.Connect(_a);
    }
}
```

---

### 生成的代码

#### Node User 生成的方法

对于标记为 `[User]` 的 Node 类型，框架生成：

```csharp
// 服务 Scope 引用
private IScope? _serviceScope;

// 获取最近的 Scope
private IScope? GetServiceScope();

// 附加到 Scope (注入依赖)
private void AttachToScope();

// 从 Scope 分离 (Host 用)
private void UnattachToScope();

// 生命周期通知处理
public override void _Notification(int what);

// 解析用户依赖
private void ResolveUserDependencies(IScope scope);
```

#### Host 生成的方法

对于标记为 `[Host]` 的类型，框架生成：

```csharp
// 注册 Host 服务到 Scope
private void AttachHostServices(IScope scope);

// 从 Scope 注销 Host 服务
private void UnattachHostServices(IScope scope);
```

#### Service 生成的方法

对于标记为 `[Singleton]` 的服务,框架生成工厂方法：

```csharp
// 创建服务实例
public static void CreateService(
    IScope scope,
    Action<object, IScope> onCreated
);
```

#### Scope 生成的方法

对于实现 `IScope` 的类型,框架生成完整的容器实现：

```csharp
// 静态集合
private static readonly HashSet<Type> ServiceTypes;

// 实例字段
private readonly Dictionary<Type, object> _services;
private readonly Dictionary<Type, List<Action<object>>> _waiters;
private readonly HashSet<IDisposable> _disposableSingletons;
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

---

### 场景树集成

#### 生命周期事件

框架监听以下 Godot 通知：

| 通知 | 处理 |
|------|------|
| `NotificationEnterTree` | User：附加到 Scope，触发注入<br>Host：注册服务<br>Scope： 清除父 Scope 缓存 |
| `NotificationExitTree` | User： 清除 Scope 引用<br>Host： 注销服务<br>Scope： 清除父 Scope 缓存 |
| `NotificationReady` | Scope： 创建 Singleton，检查等待队列 |
| `NotificationPredelete` | Scope： 释放所有服务 |

#### 场景树查找

获取 Scope 的逻辑:

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

---

## 最佳实践

### Scope 粒度设计

```csharp
// ✅ 好的设计: 按功能/生命周期划分 Scope
RootScope          // 全局服务
├── MainMenuScope  // 主菜单服务
└── GameScope      // 游戏服务
    └── LevelScope // 关卡服务

// ❌ 避免: 过多或过少的 Scope
// 过多: 每个 Node 一个 Scope (过度设计)
// 过少: 整个游戏一个 Scope (无法隔离)
```

---

### 服务释放

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

---

### 避免循环依赖

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
    public B(IA a) { }  // B 依赖 A → 循环!
}

// ✅ 打破循环: 使用事件或回调
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

---

### 接口优先原则

```csharp
// ✅ 推荐: 暴露接口
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats { }

// ⚠️ 不推荐: 暴露具体类
[Singleton(typeof(ConfigService))]
public partial class ConfigService { }
```

**原因**:

- 接口提供更好的松耦合
- 便于单元测试 (使用 mock)
- 更容易替换实现

---

### Host + User 组合使用

一个 Node 可以同时是 Host 和 User:

```csharp
[Host, User]
public partial class GameManager : Node, IGameState, IServicesReady
{
    // Host 部分: 暴露服务
    [Singleton(typeof(IGameState))]
    private IGameState Self => this;
    
    // User 部分: 注入依赖
    [Inject] private IConfig _config;
    [Inject] private ISaveSystem _saveSystem;
    
    public void OnServicesReady()
    {
        // 依赖已就绪,可以初始化
        LoadLastSave();
    }
}
```

这在需要同时提供服务和消费服务的 Node 上非常有用。

---

## 诊断代码

框架提供完整的编译时错误检查。完整诊断代码列表请参阅 [DIAGNOSTICS.md](./DIAGNOSTICS.zh-CN.md)。

**诊断代码分类**:

| 前缀 | 类别 | 说明 |
|------|------|------|
| GDI_C | Class | 类级别错误 |
| GDI_M | Member | 成员级别错误 |
| GDI_S | Constructor | 构造函数级别错误 |
| GDI_D | Dependency Graph | 依赖图错误 |
| GDI_E | Internal Error | 内部错误 |
| GDI_U | User Behavior | 用户行为警告 |

---

## 许可证

MIT License

## Todo List

- [ ] 完善中英文双语支持
- [ ] 添加示例项目（从 Godot 实际运行 GodotSharpDI.Sample）
- [ ] 添加运行时集成测试
- [ ] 实现依赖回调的等待计时和超时处理
- [ ] 增强生成代码的注释覆盖率
- [ ] 诊断生成器内部错误（GDI_E）

