# GodotSharpDI

<img src="icon.png" style="zoom:50%;" />

<p align="left"> <a href="README.zh-CN.md">中文</a> </p>

A compile-time dependency injection framework specifically designed for the Godot Engine 4, implementing zero-reflection, high-performance DI support through C# Source Generator.

[![NuGet Version](https://img.shields.io/nuget/v/GodotSharpDI.svg?style=flat)](https://www.nuget.org/packages/GodotSharpDI/)

## Table of Contents

- [Design Philosophy](#design-philosophy)
- [Installation](#installation)
- [Quick Start](#quick-start)
  - [1. Define a Service](#1-define-a-service)
  - [2. Define a Service Factory](#2-define-a-service-factory)
  - [3. Define a Scope](#3-define-a-scope)
  - [4. Define a Host](#4-define-a-host)
  - [5. Define a User](#5-define-a-user)
  - [6. Scene Tree Structure](#6-scene-tree-structure)
- [Core Concepts](#core-concepts)
  - [Four Role Types](#four-role-types)
  - [Service Lifecycle](#service-lifecycle)
- [Role Details](#role-details)
  - [Singleton Service](#singleton-service)
  - [Host](#host)
  - [User (Consumer)](#user-consumer)
  - [Scope (Container)](#scope-container)
- [Lifecycle Management](#lifecycle-management)
  - [Singleton Service Lifecycle](#singleton-service-lifecycle)
  - [Scope Hierarchy](#scope-hierarchy)
  - [Dependency Injection Timing](#dependency-injection-timing)
  - [Host + User and Circular Dependencies](#host--user-and-circular-dependencies)
- [Type Constraints](#type-constraints)
  - [Role Type Constraints](#role-type-constraints-1)
  - [Injectable Type Constraints](#injectable-type-constraints)
  - [Service Implementation Type Constraints](#service-implementation-type-constraints)
  - [Exposed Type Constraints](#exposed-type-constraints)
  - [Other Constraints](#other-constraints)
- [API Reference](#api-reference)
  - [Attributes](#attributes)
  - [Interfaces](#interfaces)
  - [Generated Code](#generated-code)
  - [Scene Tree Integration](#scene-tree-integration)
- [Best Practices](#best-practices)
  - [Scope Granularity Design](#scope-granularity-design)
  - [Service Disposal](#service-disposal)
  - [Avoiding Circular Dependencies](#avoiding-circular-dependencies)
  - [Interface-First Principle](#interface-first-principle)
  - [Host + User Combination Usage](#host--user-combination-usage)
  - [Using Service Factories](#using-service-factories)
- [Diagnostic Codes](#diagnostic-codes)
- [License](#license)
- [Todo List](#todo-list)

---

## Design Philosophy

The core design philosophy of GodotSharpDI is to **merge Godot's scene tree lifecycle with traditional DI container patterns**:

- **Scene Tree as Container Hierarchy**: Leverages Godot's scene tree structure to implement Scope hierarchy
- **Node Lifecycle Integration**: Service creation and destruction are bound to Node's enter/exit scene tree events
- **Compile-Time Safety**: Completes dependency analysis and code generation at compile time through Source Generator, providing comprehensive compile-time error checking

---

## Installation

```xml
<PackageReference Include="GodotSharpDI" Version="x.x.x" />
```
⚠️ **Make sure to also add the GodotSharp package to your project**: The generated code depends on Godot.Node and Godot.GD.


⚠️ **Important: _Notification method explicitly definition requirement**

> **Starting from version 1.0.0-rc.1**, all Host, User, and Scope types **must** explicitly define the `_Notification` method in C# script file attached to the node:
>
> ```csharp
> public override partial void _Notification(int what);
> ```
>
> ### Why is this required?
>
> - When you attach a C# script to a node in Godot, the engine creates a binding between the node and that specific script file
> - Godot's script binding mechanism scans only the attached script file for virtual method overrides
> - Source-generated files (*.g.cs) are compiled into the same class via `partial`, but Godot doesn't scan these files for lifecycle methods
> - Therefore, lifecycle hooks like `_Notification` must be declared in the user's source file as a `partial` method
>
> ### IDE Support
>
> IDE (Visual Studio, Rider) will provide automatic fixes:
>
> 1. If you forget to add this method, you'll see a **GDI_C080** error
> 2. Press `Ctrl+.` (VS) or `Alt+Enter` (Rider) on the error
> 3. Select "Add _Notification method declaration" to auto-generate the correct declaration
>
> ### Example:
>
> ```csharp
> // Your source file: GameManager.cs (attached to node)
> [Host]
> public partial class GameManager : Node
> {
>     // Required: Godot needs to see this declaration
>     public override partial void _Notification(int what);
>     
>     [Singleton(typeof(IGameState))]
>     private IGameState Self => this;
> }
> 
> // Generated file: GameManager.DI.g.cs (not scanned by Godot)
> partial class GameManager
> {
>     // Framework provides the implementation
>     public override partial void _Notification(int what)
>     {
>         base._Notification(what);
>         switch ((long)what)
>         {
>             case NotificationEnterTree:
>                 AttachToScope();
>                 break;
>             case NotificationExitTree:
>                 UnattachToScope();
>                 break;
>         }
>     }
> }
> ```
>

---

## Quick Start

### 1. Define a Service

```csharp
// Define service interface
public interface IPlayerStats
{
    int Health { get; set; }
    int Mana { get; set; }
}

// Implement service
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;
}
```

### 2. Define a Service Factory

```csharp
// Define service interface
public interface IEnemySpawner
{
    Enemy SpawnEnemy();
}

// Implement service factory
[Singleton(typeof(IEnemySpawner))]
public partial class EnemyFactory : IEnemySpawner
{
    private IPlayerStats _playerStats;
    
    // Inject dependencies through constructor
    [InjectConstructor]
    public EnemyFactory(IPlayerStats playerStats)
    {
        _playerStats = playerStats;
    }
    
    public Enemy SpawnEnemy()
    {
        // Pass dependencies to dynamic objects
        return new Enemy(_playerStats);
    }
}
```

### 3. Define a Scope

```csharp
[Modules(
    Services = [typeof(PlayerStatsService), typeof(EnemyFactory)],
    Hosts = [typeof(GameManager)]
)]
public partial class GameScope : Node, IScope
{
    // Framework automatically generates IScope implementation
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}
```

### 4. Define a Host

```csharp
[Host]
public partial class GameManager : Node, IGameState
{
    // Expose itself as IGameState service
    [Singleton(typeof(IGameState))]
    private GameManager Self => this;
    
    public GameState CurrentState { get; set; }
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}
```

### 5. Define a User

```csharp
[User]
public partial class PlayerUI : Control, IServicesReady
{
    [Inject] private IPlayerStats _stats;
    [Inject] private IGameState _gameState;
    
    // Called after all dependencies are injected
    public void OnServicesReady()
    {
        UpdateUI();
    }
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}
```

### 6. Scene Tree Structure

```
GameScope (IScope)
├── GameManager (Host)
└── PlayerUI (User) ← Automatically receives injection
```

---

## Core Concepts

### Four Role Types

| Role | Description | Constraints |
|------|-------------|-------------|
| **Singleton Service** | Pure logic service, unique within Scope, created and managed by Scope, released when Scope is destroyed | Must be non-Node class |
| **Host** | Scene-level resource provider, bridges Node resources to the DI world | Must be Node |
| **User** | Dependency consumer, receives injection | Must be Node |
| **Scope** | DI container, manages service lifecycle | Must be Node, implements IScope |

---

## Role Details

### Singleton Service

#### Responsibilities

Types marked with [Singleton] are pure logic services that encapsulate business logic and data processing, **not dependent on the Godot Node system**.

#### Lifecycle Marking

```csharp
// Singleton: Unique instance within Scope
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats { }
```

#### Constructor Injection

Singleton services inject dependencies through constructors:

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

**Constructor Selection Rules**:

1. Use the constructor marked with `[InjectConstructor]`
2. When there is only one constructor, it serves as the default constructor, regardless of whether it is marked with `[InjectConstructor]`
3. If there are multiple constructors, a unique `[InjectConstructor]` must be specified

```csharp
[Singleton(typeof(IService))]
public partial class MyService : IService
{
    // When there are multiple constructors, must specify
    [InjectConstructor]
    public MyService(IDep1 dep1) { }
    
    public MyService(IDep1 dep1, IDep2 dep2) { }
}
```

#### Exposed Types

Specify the exposed service types through [Singleton] parameters:

```csharp
// Expose single interface
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats { }

// Expose multiple interfaces
[Singleton(typeof(IReader), typeof(IWriter))]
public partial class FileService : IReader, IWriter { }

// When no parameter is specified, expose the class itself (not recommended)
[Singleton]  // Exposes ConfigService type
public partial class ConfigService { }
```

> ⚠️ **Best Practice**: Always expose interfaces rather than concrete classes to maintain loose coupling and testability.

---

### Host

#### Responsibilities

Host is the bridge between the Godot Node system and the DI system, exposing Node-managed resources as injectable services.

#### Static Constraints

Host is a **static** component of Scope, not a dynamic service provider.

**❌ Don't:**

* Reparent at runtime
* Dynamically add/remove Hosts
* Expect Hosts to migrate between different Scopes

**✅ Do:**

* Treat Host as a fixed part of the Scope node tree
* Determine Host's position during scene design
* Make Host a scene tree child of Scope, cleaned up when Scope is destroyed
* Use service factory pattern for dynamic services (see **[Using Service Factories](#using-service-factories)** section)

> These constraints give Scope singleton characteristics within its scope. Compared to traditional global singletons, Scope can actively limit its influence—naturally partitioning service boundaries through the scene tree hierarchy. This makes the scene structure more flexible and controllable, allowing Users to conveniently access required dependencies at various levels of the scene tree without global pollution.
>
> In short, treat Scope and Host as stable "anchor points" in the scene tree, where Host serves as a "functional module" for Scope to partition logic and manage resources.

#### Typical Usage Patterns

**Pattern 1: Host Exposes Itself**

```csharp
[Host]
public partial class ChunkManager : Node3D, IChunkGetter, IChunkLoader
{
    [Singleton(typeof(IChunkGetter), typeof(IChunkLoader))]
    private ChunkManager Self => this;
    
    // Node-managed resources
    private Dictionary<Vector3I, Chunk> _chunks = new();
    
    // Implement interfaces
    public Chunk GetChunk(Vector3I pos) => _chunks.GetValueOrDefault(pos);
    public void LoadChunk(Vector3I pos) { /* ... */ }
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}
```

This is the most typical usage of Host: the Node itself implements service interfaces and exposes itself to the DI system.

**Pattern 2: Host Holds and Exposes Other Objects**

```csharp
[Host]
public partial class WorldManager : Node
{
    [Singleton(typeof(IWorldConfig))]
    private WorldConfig _config = new();
    
    [Singleton(typeof(IWorldState))]
    private WorldState _state = new();
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}

public class WorldConfig : IWorldConfig { /* ... */ }
public class WorldState : IWorldState { /* ... */ }
```

Host can hold and manage other objects and expose them as services. **The lifecycle of these objects is controlled by the Host.**

```csharp
// ❌ Error: Types marked with [Singleton] can only be held by Scope
[Singleton(typeof(IConfig))]
public partial class ConfigService : IConfig { }

[Host]
public partial class BadHost : Node
{
    [Singleton(typeof(IConfig))]
    private ConfigService _config = new();  // Compile error GDI_M050
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}

// ✅ Correct: Use injection instead of holding
[Host, User]
public partial class GoodHost : Node
{
    [Singleton(typeof(ISelf))]
    private ISelf Self => this;
    
    [Inject]
    private IConfig _config;  // Get Service through injection
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}
```

---

### User (Consumer)

#### Responsibilities

User is the dependency consumer, receiving service dependencies through field or property injection.

#### User Automatic Dependency Injection

```csharp
[User]
public partial class PlayerController : CharacterBody3D, IServicesReady
{
    [Inject] private IPlayerStats _stats;
    [Inject] private ICombatSystem _combat;
    
    // Automatically called when all dependencies are injected
    public void OnServicesReady()
    {
        GD.Print("All services are ready, can start game logic");
    }
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}
```

> Users automatically trigger injection when entering the scene tree, no manual operation required.
>

#### IServicesReady Interface

User types can implement the `IServicesReady` interface, `OnServicesReady()` is called immediately after all `[Inject]` members are resolved.

```csharp
public interface IServicesReady
{
    void OnServicesReady();
}
```

> ⚠️ **`OnServicesReady()` is always called after `_Ready()`**, because User starts dependency resolution at NotificationReady.

**Example**:

```csharp
[User]
public partial class UIManager : Control, IServicesReady
{
    [Inject] private IPlayerStats _stats;
    [Inject] private IGameState _gameState;
    
    public void OnServicesReady()
    {
        // All dependencies are ready, can safely access them
        _stats.OnHealthChanged += UpdateHealthBar;
        _gameState.OnStateChanged += UpdateGameState;
        
        // Initial UI update
        UpdateHealthBar(_stats.Health);
        UpdateGameState(_gameState.CurrentState);
    }
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}
```

---

### Scope (Container)

#### Responsibilities

Scope is the DI container responsible for:
- Creating and managing Singleton service instances
- Resolving dependency requests
- Managing service lifecycle (creation and disposal)
- Providing parent-child Scope hierarchy

#### Static Constraints

Scope is a **static service container** in the scene tree with well-defined scope boundaries.

**❌ Don't:**

- Dynamically create/destroy Scopes at runtime (unless the entire subscene needs to be unloaded)
- Expect Scopes to frequently move positions in the scene tree
- Use Scope as a temporary service cache or dynamic service pool
- Change Scope's parent-child relationships at runtime to "switch" service scopes

**✅ Do:**

- Treat Scope as a **fixed structural node** of the scene tree
- Determine Scope's hierarchy and position during scene design phase
- Synchronize Scope's lifecycle with its corresponding scene region
- Create and destroy Scope together with its scene node
- Register service factories within Scope for dynamic services (see **[Using Service Factories](#using-service-factories)** section)

> These constraints give Scope singleton characteristics within its scope. Compared to traditional global singletons, Scope can actively limit its influence—naturally partitioning service boundaries through the scene tree hierarchy. This makes the scene structure more flexible and controllable, allowing Users to conveniently access required dependencies at various levels of the scene tree without global pollution.
>
> In short, treat Scope as a stable "anchor point" in the scene tree that defines the visibility range of services, while Host serves as a "functional module" for Scope to partition logic and manage resources.

> **Static Nature: Host vs Scope**
>
> | Aspect             | Host                          | Scope                                  |
> | ------------------ | ----------------------------- | -------------------------------------- |
> | **Nature**         | Component of Scope            | Structural node of scene tree          |
> | **Scope**          | No independent scope          | Defines service scope boundaries       |
> | **Static Meaning** | Cannot migrate between Scopes | Cannot be frequently created/destroyed |
> | **Lifecycle**      | Destroyed with Scope          | Follows scene region lifecycle         |

#### Defining a Scope

```csharp
[Modules(
    Services = [typeof(ServiceA), typeof(ServiceB)],
    Hosts = [typeof(HostA), typeof(HostB)]
)]
public partial class GameScope : Node, IScope
{
    // Framework automatically generates IScope implementation
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}
```

**Modules Attribute Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| Services | Type[] | List of Singleton service types, must be [Singleton] marked classes |
| Hosts | Type[] | List of Host types, must be [Host] marked classes |

---

## Lifecycle Management

### Singleton Service Lifecycle

#### Creation Timing

Singleton services are created at these times:
1. **Scope Ready**: When Scope's `NotificationReady` event triggers, all Services specified in [Modules] are created
2. **On Demand**: When a dependency request comes in and the service hasn't been created yet

#### Destruction Timing

Singleton services are destroyed when:
1. **Scope Destruction**: When Scope's `NotificationPredelete` event triggers
2. **IDisposable Support**: Services implementing `IDisposable` will have `Dispose()` called

```csharp
[Singleton(typeof(IResourceManager))]
public partial class ResourceManager : IResourceManager, IDisposable
{
    public void Dispose()
    {
        // Release resources
    }
}
```

---

### Scope Hierarchy

Scope forms a hierarchical relationship through a scene tree structure:

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

#### Service Visibility Rules

| Service Location | Accessible From |
|-----------------|----------------|
| RootScope | All descendant Scopes |
| GameScope | GameScope and LevelScope |
| LevelScope | Only LevelScope |

**Example**:

```csharp
// RootScope
[Modules(Services = [typeof(ConfigService)])]
public partial class RootScope : Node, IScope { }

// GameScope can access ConfigService
[Modules(Services = [typeof(PlayerService)])]
public partial class GameScope : Node, IScope { }

// LevelScope can access both ConfigService and PlayerService
[Modules(Services = [typeof(EnemyService)])]
public partial class LevelScope : Node, IScope { }
```

> **Hierarchy Rules**:
>
> - A Scope searches upward in the scene tree to find its parent Scope
> - If a service is not found in the current Scope, it searches in the parent Scope
> - Service lifecycle is bound to its defining Scope
>

---

### Dependency Injection Timing

#### Singleton Service Creation Timeline
```
Scope Node Ready (NotificationReady)
    ↓
Scope.InstantiateScopeSingletons()
    ↓
For each Service in Modules.Services:
    ↓
    Service.CreateService()
    ↓
    For each constructor parameter:
        ↓
        Scope.ResolveDependency<T>()
    ↓
    All dependencies resolved?
    ↓
    Yes
    ↓
    scope.ProvideService<T>(service)
    ↓
    Notify waiting queue
```

#### User Injection Timeline
```
User Node Ready (NotificationReady)
 ↓
GetServiceScope() ← Search upward for nearest IScope
 ↓
ResolveUserDependencies(scope)
 ↓
scope.ResolveDependency<T>(callback) ← For each [Inject] member
 ↓
Wait for service ready or immediate callback
 ↓
OnServicesReady() ← All dependencies injected (if implements IServicesReady)
```

#### Host Service Registration Timeline
```
Host Node Ready (NotificationReady)
 ↓
GetServiceScope() ← Search upward for nearest IScope
 ↓
ProvideHostServices(scope)
 ↓
scope.ProvideService<T>(this.Member) ← For each [Singleton] member
 ↓
Notify waiting queue
```

---

### Host + User and Circular Dependencies

In GodotSharpDI, a type can be marked as both `[Host, User]`, meaning it both provides and consumes services. To avoid false positives for circular dependencies, it's important to understand the difference between Host and User in terms of lifecycle and injection timing.

#### Injection Timing Differences Between Host and User

**Host (Service Provider)**

- Registers services from its `[Singleton]` members during the **EnterTree** phase
- Service registration **does not trigger any dependency injection**
- Does not trigger its own User injection
- Does not trigger injection for other Users

**User (Service Consumer)**

- Attaches to the nearest Scope during the **EnterTree** phase
- Immediately initiates dependency resolution for all `[Inject]` members
- If a service is not yet registered, joins the waiting queue
- Gets injected via callback when the service is registered or when Scope is Ready
- Triggers `OnServicesReady()` after all dependencies are injected

**Conclusion**

> **Host's service registration phase does not participate in the dependency injection chain.**
> **User's dependency injection only triggers after the Node enters the scene tree or after service registration completes.**

This rule ensures that Host+User combinations don't create circular dependencies due to "self-providing and self-consuming".

#### Example 1: Host+User Self-Injection Is Not a Circular Dependency

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

**Dependency Relationship**

- Host part provides `IMyService`
- User part consumes `IMyService`

**Why is this not a circular dependency?**

1. When Host registers `Self`, it **does not trigger** injection of `_self`
2. Injection of `_self` happens during the User injection phase (EnterTree → AttachToScope)
3. At that point, `IMyService` is already registered, so injection succeeds
4. The entire process has no constructor chain and does not form a dependency loop

**Conclusion**

> **Host+User self-injection is legal and not a circular dependency.**

#### Example 2: Host Providing Service + Consuming Another Service Is Not a Circular Dependency

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
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}
```

**Dependency Relationship**

- `HostUser` (Host) provides `IServiceB`
- `ServiceA` constructor depends on `IServiceB` → injects `HostUser`
- `HostUser` (User) depends on `IServiceA`

**Why is this not a circular dependency?**

1. When HostUser registers `IServiceB`, it **does not trigger** injection of `_serviceA`
2. ServiceA's constructor resolves `IServiceB` → gets HostUser
3. After ServiceA construction completes, HostUser's `_serviceA` is assigned during the User injection phase
4. There is no constructor loop in the entire chain

**Dependency graph**:

```
ServiceA → IServiceB (HostUser)
HostUser(User) → IServiceA
```

This is a "diamond dependency", not a cycle.

**Conclusion**

> **Host providing services + consuming other services as User is legal and not a circular dependency.**

#### Scope of Circular Dependency Detection

GodotSharpDI's circular dependency detection only applies to:

- **Service → Service constructor dependency chains**

It does not include:

- User's `[Inject]` members
- Host's `[Singleton]` members
- Host+User self-injection
- Cross-dependencies between Host and User

Reason:

> **User injection occurs after all Service construction is complete and does not participate in construction-time dependency loops.**

Therefore, only the following situation is considered a circular dependency:

```csharp
[Singleton(typeof(IA))]
class A : IA { public A(IB b) {} }

[Singleton(typeof(IB))]
class B : IB { public B(IA a) {} }
```

#### Summary

| Situation | Circular Dependency? | Reason |
|-----------|---------------------|---------|
| Host+User self-injection | ❌ | Host registration doesn't trigger injection, User injection comes after |
| Host provides service + consumes as User | ❌ | Injection timing is separated, no constructor loop formed |
| Service ↔ Service mutual constructor dependency | ✔️ | Constructor loop |

Final rule:

> **As long as the dependency chain doesn't form a loop in Service constructors, it's not a circular dependency. Host+User injection timing naturally avoids constructor cycles.**

---

## Type Constraints

> **Terminology**:
> - **Host + User**: A node marked with both Host and User attributes
> - **Non-Node class**: Regular C# class that does not inherit from Godot.Node
> - **Regular Node**: Node that inherits from Node but is not marked with special roles

### Singleton Service Detailed Constraints

**Basic Constraints**

| Constraint | Requirement | Reason |
|------------|-------------|---------|
| Type | Must be class | Needs instantiation |
| Inheritance | Cannot be Node | Node lifecycle is controlled by Godot, conflicts with DI container |
| Modifiers | Cannot be abstract or static | Needs instantiation |
| Generics | Cannot be open generic | Needs concrete type for instantiation |
| Declaration | Must be partial | Source generator needs to extend the class |

**Type Constraints**

| Type | Allowed | Description |
|------|---------|-------------|
| Non-Node class | ✅ | **Recommended** |
| Host / Host + User | ❌ | Should provide services through members |
| Regular Node | ❌ | No static constraints, cannot guarantee lifecycle |
| User | ❌ | No static constraints, cannot guarantee lifecycle |
| Scope | ❌ | Container cannot be a service |
| Other types | ❌ | Not supported |

**Exposed Type Constraints**

| Type | Allowed | Description |
|------|---------|-------------|
| Implemented interface | ✅ | **Recommended** |
| Inherited class | ✅ | Allowed |
| Unimplemented interface | ❌ | Meaningless |
| Non-inherited class | ❌ | Meaningless |

**Constructor Constraints**

| Constraint | Requirement |
|------------|-------------|
| Visibility | At least one non-static constructor |
| Multiple constructors | Must specify with [InjectConstructor] |

**Constructor Parameter Type Constraints**

| Type | Allowed | Description |
|------|---------|-------------|
| interface | ✅ | **Recommended approach** |
| Non-Node class | ✅ | Allowed |
| Host / Host + User | ⚠️ | Allowed but not recommended, should depend on interfaces exposed by Host |
| Regular Node | ❌ | No static constraints, cannot guarantee lifecycle |
| User | ❌ | No static constraints, cannot guarantee lifecycle |
| Scope | ❌ | Container cannot be a service |
| Other types | ❌ | Not supported |

---

### Host Detailed Constraints

**Basic Constraints**

| Constraint    | Requirement                                                  | Reason                                                       |
| ------------- | ------------------------------------------------------------ | ------------------------------------------------------------ |
| Type          | Must be class                                                | Needs instantiation                                          |
| Inheritance   | Must be Node                                                 | Needs to integrate with scene tree lifecycle                 |
| Declaration   | Must be partial                                              | Source generator needs to extend the class                   |
| _Notification | Must declare `public override partial void _Notification(int what);` | Godot only recognizes lifecycle methods defined in the attached script file |

**Host Singleton Member Type Constraints**

| Type | Allowed | Description |
|------|---------|-------------|
| Non-Node class | ✅ | **Recommended** |
| Host / Host + User (self type) | ✅ | Can expose itself as a service |
| Host / Host + User (non-self type) | ❌ | Host nesting not allowed |
| Regular Node | ❌ | No static constraints, cannot guarantee lifecycle |
| User | ❌ | No static constraints, cannot guarantee lifecycle |
| Scope | ❌ | Container nesting not allowed |
| Other types | ❌ | Not supported |

**Host Singleton Member Exposed Type Constraints**

| Type | Allowed | Description |
|------|---------|-------------|
| Implemented interface | ✅ | **Recommended** |
| Inherited class | ✅ | Allowed |
| Unimplemented interface | ❌ | Meaningless |
| Non-inherited class | ❌ | Meaningless |

---

### User Detailed Constraints

**Basic Constraints**

| Constraint    | Requirement                                                  | Reason                                                       |
| ------------- | ------------------------------------------------------------ | ------------------------------------------------------------ |
| Type          | Must be class                                                | Needs instantiation                                          |
| Inheritance   | Must be Node                                                 | Needs to integrate with scene tree lifecycle                 |
| Declaration   | Must be partial                                              | Source generator needs to extend the class                   |
| _Notification | Must declare `public override partial void _Notification(int what);` | Godot only recognizes lifecycle methods defined in the attached script file |

**User Inject Member Type Constraints**

| Type | Allowed | Description |
|------|---------|-------------|
| interface | ✅ | **Recommended approach** |
| Non-Node class | ✅ | Allowed |
| Host / Host + User | ⚠️ | Allowed but not recommended, should depend on interfaces exposed by Host |
| Regular Node | ❌ | No static constraints, cannot guarantee lifecycle |
| User | ❌ | No static constraints, cannot guarantee lifecycle |
| Scope | ❌ | Container cannot be a service |
| Other types | ❌ | Not supported |

---

### Scope Detailed Constraints

| Constraint    | Requirement                                                  | Reason                                                       |
| ------------- | ------------------------------------------------------------ | ------------------------------------------------------------ |
| Type          | Must be class                                                | Needs instantiation                                          |
| Inheritance   | Must be Node                                                 | Needs to integrate with scene tree lifecycle                 |
| Interface     | Must implement IScope                                        | Provides service registration API                            |
| Modules       | Must specify [Modules]                                       | Defines service composition                                  |
| Declaration   | Must be partial                                              | Source generator needs to extend the class                   |
| _Notification | Must declare `public override partial void _Notification(int what);` | Godot only recognizes lifecycle methods defined in the attached script file |

---

## API Reference

### Attributes

#### [Singleton]

Marks a type as a Singleton service and specifies exposed service types.

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property)]
public class SingletonAttribute : Attribute
{
    public Type[] ServiceTypes { get; }
    
    public SingletonAttribute(params Type[] serviceTypes) { }
}
```

**Usage Scenarios**:

1. On Service classes: Mark the class as a Singleton service
2. On Host members: Expose the member as a service

**Parameters**:
- `serviceTypes`: Service types to expose (interfaces or base classes)
- When empty: Service class exposes itself; Host member exposes its own type

#### [Host]

Marks a Node as a Host.

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Class)]
public class HostAttribute : Attribute { }
```

#### [User]

Marks a Node as a User.

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Class)]
public class UserAttribute : Attribute { }
```

#### [Inject]

Marks a field or property for dependency injection.

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class InjectAttribute : Attribute { }
```

**Usage Rules**:

- Can only be used on User or Host+User types
- Member must be writable (field non-readonly, property must have setter)
- Cannot be static

#### [InjectConstructor]

Marks which constructor to use for dependency injection.

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Constructor)]
public class InjectConstructorAttribute : Attribute { }
```

**Usage Rules**:

- Can only be used on Singleton Service types
- Required when there are multiple constructors
- Must be unique (only one constructor can be marked)

#### [Modules]

Specifies the services and hosts managed by a Scope.

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Class)]
public class ModulesAttribute : Attribute
{
    public Type[] Services { get; set; } = Array.Empty<Type>();
    public Type[] Hosts { get; set; } = Array.Empty<Type>();
}
```

**Parameters**:

| Parameter  | Description                                         |
| ---------- | --------------------------------------------------- |
| `Services` | List of Service types created and managed by Scope  |
| `Hosts`    | List of Host types expected to be received by Scope |

---

### Interfaces

#### IScope

Container interface, manages service registration and resolution.

```csharp
namespace GodotSharpDI.Abstractions;

public interface IScope
{
    void ProvideService<T>(T instance) where T : notnull;
    void ResolveDependency<T>(Action<T> onResolved) where T : notnull;
}
```

**Methods**:

| Method | Description | When to Use |
|--------|-------------|-------------|
| `ProvideService<T>` | Provide a service | Called automatically by framework, no manual call needed |
| `ResolveDependency<T>` | Request a dependency | Called automatically by framework, no manual call needed |

> ⚠️ **Important**: These methods are managed by the framework and should not be called manually. Framework generates appropriate calls in User, Host, and Service code.

#### IServicesReady

Notification interface for when all dependencies are ready.

```csharp
namespace GodotSharpDI.Abstractions;

public interface IServicesReady
{
    void OnServicesReady();
}
```

**Usage Rules**:

- Can only be implemented by User or Host+User types
- Called immediately after all [Inject] members are resolved
- Suitable for initialization logic that depends on injected services

---

### Generated Code

#### Node Lifecycle Related Methods

For types marked with `[Host]`, `[User]`, or `[Scope]`, the framework generates:

```csharp
// Scope reference
private IScope? _serviceScope;

// Find nearest parent Scope
private IScope? GetServiceScope();

// Override Godot's notification method
public override partial void _Notification(int what);
```

#### User Generated Content

For types marked with `[User]`, the framework generates:

```csharp
// Resolve User dependencies
private void ResolveUserDependencies(IScope scope);
```

#### Host Generated Content

For types marked with `[Host]`, the framework generates:

```csharp
// Register Host services to Scope
private void ProvideHostServices(IScope scope);
```

#### Service Generated Content

For services marked with `[Singleton]`, the framework generates factory methods:

```csharp
// Create service instance
public static void CreateService(
    IScope scope,
    Action<object, IScope> onCreated
);
```

#### Scope Generated Content

For types implementing `IScope`, the framework generates complete container implementation:

```csharp
// Static collections
private static readonly HashSet<Type> ServiceTypes;

// Instance fields
private readonly Dictionary<Type, object> _services;
private readonly Dictionary<Type, List<Action<object>>> _waiters;
private readonly HashSet<IDisposable> _disposableSingletons;

// Lifecycle methods
private void InstantiateScopeSingletons();
private void DisposeScopeSingletons();
private void CheckWaitList();

// IScope implementation
void IScope.ProvideService<T>(T instance);
void IScope.ResolveDependency<T>(Action<T> onResolved);
```

---

### Scene Tree Integration

#### Lifecycle Events

**EnterTree (Top to Bottom)**
```
1. Scope EnterTree
   └→ Clear _parentScope cache
2. Host EnterTree
   └→ Clear _parentScope cache
3. User EnterTree
   └→ Clear _parentScope cache
```

**Ready (Bottom to Top)**
```
1. Host Ready
   └→ Provide Host Service ⭐
2. User Ready
   └→ Resolve dependencies ⭐
   └→ OnServicesReady() ⭐
3. Scope Ready
   └→ Create all Scope Services ⭐
   └→ Check if waiting queue is empty ⭐
```

**ExitTree (Bottom to Top)**
```
1. User ExitTree
   └→ Clear _parentScope cache
2. Host ExitTree
   └→ Clear _parentScope cache
3. Scope ExitTree
   └→ Clear _parentScope cache
```

**Predelete**
```
1. User Predelete
   └→ (No additional operations needed)
2. Host Predelete
   └→ (No additional operations needed)
3. Scope Predelete
   └→ Release all singletons ⭐
```

#### Scene Tree Search

Logic for getting Scope:

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
    
    GD.PushError("No nearest Service Scope found");
    return null;
}
```

---

## Best Practices

### Scope Granularity Design

```csharp
// ✅ Good design: Divide Scopes by functionality/lifecycle
RootScope          // Global services
├── MainMenuScope  // Main menu services
└── GameScope      // Game services
    └── LevelScope // Level services

// ❌ Avoid: Too many or too few Scopes
// Too many: One Scope per Node (over-engineering)
// Too few: Entire game in one Scope (cannot isolate)
```

---

### Service Disposal

```csharp
// ✅ Implement IDisposable for cleanup
[Singleton(typeof(IResourceLoader))]
public partial class ResourceLoader : IResourceLoader, IDisposable
{
    private List<Resource> _loadedResources = new();
    
    public void Dispose()
    {
        foreach (var res in _loadedResources)
        {
            res.Free();  // Release Godot resources
        }
        _loadedResources.Clear();
    }
}
```

---

### Avoiding Circular Dependencies

```csharp
// ❌ Circular dependency
[Singleton(typeof(IA))]
public partial class A : IA
{
    public A(IB b) { }  // A depends on B
}

[Singleton(typeof(IB))]
public partial class B : IB
{
    public B(IA a) { }  // B depends on A → Circular!
}

// ✅ Break the cycle: Use events or callbacks
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

### Interface-First Principle

```csharp
// ✅ Recommended: Expose interfaces
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats { }

// ⚠️ Not recommended: Expose concrete class
[Singleton(typeof(ConfigService))]
public partial class ConfigService { }
```

**Reasons**:

- Interfaces provide better loose coupling
- Easier for unit testing (using mocks)
- Easier to replace implementations

---

### Host + User Combination Usage

A Node can be both Host and User simultaneously:

```csharp
[Host, User]
public partial class GameManager : Node, IGameState, IServicesReady
{
    // Host part: expose service
    [Singleton(typeof(IGameState))]
    private IGameState Self => this;
    
    // User part: inject dependencies
    [Inject] private IConfig _config;
    [Inject] private ISaveSystem _saveSystem;
    
    public void OnServicesReady()
    {
        // Dependencies are ready, can initialize
        LoadLastSave();
    }
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}
```

This is very useful for Nodes that need to both provide and consume services.

---

## Using Service Factories

**Factory is a Singleton:**

```csharp
[Singleton(typeof(IFactory))]
public partial class MyFactory : IFactory
{
    private readonly IDep _dep;
    
    public MyFactory(IDep dep)
    {
        _dep = dep;
    }
    
    public Product Create(params...)
    {
        return new Product(_dep, params...);
    }
}
```

**Product is a regular class:**

```csharp
public class Product : IDisposable
{
    private readonly IDep _dep;
    
    public Product(IDep dep, ...)
    {
        _dep = dep;
    }
    
    public void Dispose() { }
}
```

**Usage:**
```csharp
[User]
public partial class MyUser : Node
{
    [Inject] private IFactory _factory;
    
    public void DoWork()
    {
        using var product = _factory.Create(...);
        product.Execute();
    }
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}
```

### Common Patterns

#### 1. Simple Factory
```csharp
[Singleton(typeof(IBulletFactory))]
public partial class BulletFactory : IBulletFactory
{
    public Bullet Create() => new Bullet();
}
```

#### 2. Object Pool
```csharp
[Singleton(typeof(IPooledFactory))]
public partial class PooledFactory : IPooledFactory
{
    private ObjectPool _pool = new();
    
    public Item Get() => _pool.Get();
    public void Return(Item item) => _pool.Return(item);
}
```

#### 3. Dependency Propagation
```csharp
[Singleton(typeof(IComplexFactory))]
public partial class ComplexFactory : IComplexFactory
{
    private readonly IPhysics _physics;
    private readonly IAudio _audio;
    
    public ComplexFactory(IPhysics physics, IAudio audio)
    {
        _physics = physics;
        _audio = audio;
    }
    
    public ComplexObject Create(params...)
    {
        return new ComplexObject(_physics, _audio, params...);
    }
}
```

#### 4. Extension: ECS Integration Example

```csharp
// System is a Singleton service

[Singleton(typeof(IMovementSystem))]
public partial class MovementSystem : IMovementSystem { ... }

[Singleton(typeof(IWorld))]
public partial class GameWorld : IWorld
{
    public GameWorld(IMovementSystem movement) { ... }
    public void Update(double delta) { ... }
}

[Singleton(typeof(IProjectileSystem))]
public partial class ProjectileSystem : IProjectileSystem
{
    private readonly IPhysics _physics;
    private readonly IWorld _world;
    
    public ProjectileSystem(IPhysics physics, IWorld world)
    {
        _physics = physics;
        _world = world;
    }
    
    // Create Entity (ECS approach)
    public void SpawnProjectile(Vector3 pos, Vector3 vel)
    {
        var entity = _world.CreateEntity();
        entity.Set(new Position { Value = pos });
        entity.Set(new Velocity { Value = vel });
    }
    
    // Or use factory to create regular objects
    public Projectile CreateProjectile(Vector3 pos, Vector3 vel)
    {
        return new Projectile(_physics, pos, vel);
    }
}

// Entity is pure data (ECS recommended)
public struct ProjectileEntity
{
    public Vector3 Position;
    public Vector3 Velocity;
}

// Or regular class object (traditional approach)
public class Projectile : IDisposable
{
    private readonly IPhysics _physics;
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    
    public Projectile(IPhysics physics, Vector3 pos, Vector3 vel)
    {
        _physics = physics;
        Position = pos;
        Velocity = vel;
    }
    
    public void Update(double delta) { }
    public void Dispose() { }
}
```

---

## Diagnostic Codes

The framework provides comprehensive compile-time error checking. For a complete list of diagnostic codes, please refer to [DIAGNOSTICS.md](./DIAGNOSTICS.md).

**Diagnostic Code Categories**:

| Prefix | Category | Description |
|--------|----------|-------------|
| GDI_C | Class | Class-level errors |
| GDI_M | Member | Member-level errors |
| GDI_S | Constructor | Constructor-level errors |
| GDI_D | Dependency Graph | Dependency graph errors |
| GDI_E | Internal Error | Internal errors |
| GDI_U | User Behavior | User behavior warnings |

---

## License

MIT License

## Todo List

### 1. Documentation and Examples

- [ ] Improve bilingual (Chinese-English) support
- [ ] Add sample projects (running GodotSharpDI.Sample from Godot)
- [ ] Enhance comment coverage in generated code

### 2. Testing

- [ ] Add runtime integration tests

### 3. Features

- [ ] Implement dependency callback waiting timing and timeout handling
- [ ] Support asynchronous operations (using CallDeferred)

### 4. Diagnostics

- [ ] Diagnose generator internal errors (GDI_E)
