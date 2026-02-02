# GodotSharpDI

<img src="icon.png" style="zoom:50%;" />

<p align="left"> <a href="README.zh-CN.md">中文</a> </p>

A compile-time dependency injection framework specifically designed for the Godot Engine, implementing zero-reflection, high-performance DI support through C# Source Generator.

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
  - [Singleton Lifecycle](#singleton-lifecycle)
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
    IPlayerStats _playerStats;
    
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
}
```

### 4. Define a Host

```csharp
[Host]
public partial class GameManager : Node, IGameState
{
    // Expose itself as IGameState service
    [Singleton(typeof(IGameState))]
    private IGameState Self => this;
    
    public GameState CurrentState { get; set; }
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
}
```

### 6. Scene Tree Structure

```
GameScope (IScope)
├── PlayerStatsService (Singleton)
├── EnemyFactory (Singleton)
├── GameManager (Host)
└── Player
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

#### Constraints

| Constraint | Requirement | Reason |
|------------|-------------|---------|
| Type | Must be class | Needs instantiation |
| Inheritance | Cannot be Node | Node lifecycle is controlled by Godot, conflicts with DI container |
| Modifiers | Cannot be abstract or static | Needs instantiation |
| Generics | Cannot be open generic | Needs concrete type for instantiation |
| Declaration | Must be partial | Source generator needs to extend the class |

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

#### Constraints

| Constraint | Requirement | Reason |
|------------|-------------|---------|
| Type | Must be class | Needs instantiation |
| Inheritance | Must be Node | Needs to integrate with scene tree lifecycle |
| Declaration | Must be partial | Source generator needs to extend the class |

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
}

public class WorldConfig : IWorldConfig { /* ... */ }
public class WorldState : IWorldState { /* ... */ }
```

Host can hold and manage other objects and expose them as services. **The lifecycle of these objects is controlled by the Host.**

#### Host Member Constraints

| Constraint | Requirement | Reason |
|------------|-------------|---------|
| Member Type | Cannot be a type already marked as Service | Avoids lifecycle conflicts |
| static Members | Not allowed | Needs instance-level services |
| Properties | Must have getter | Needs to read value to register service |

```csharp
// ❌ Error: Types marked with [Singleton] can only be held by Scope
[Singleton(typeof(IConfig))]
public partial class ConfigService : IConfig { }

[Host]
public partial class BadHost : Node
{
    [Singleton(typeof(IConfig))]
    private ConfigService _config = new();  // Compile error GDI_M050
}

// ✅ Correct: Use injection instead of holding
[Host, User]
public partial class GoodHost : Node
{
    [Singleton(typeof(ISelf))]
    private ISelf Self => this;
    
    [Inject]
    private IConfig _config;  // Get Service through injection
}
```

---

### User (Consumer)

#### Responsibilities

User is the dependency consumer, receiving service dependencies through field or property injection.

#### Constraints

| Constraint | Requirement | Reason |
|------------|-------------|---------|
| Type | Must be class | Needs instantiation |
| Inheritance | Must be Node | Needs to integrate with scene tree lifecycle |
| Declaration | Must be partial | Source generator needs to extend the class |

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
}
```

Node type Users automatically trigger injection when entering the scene tree, no manual operation required.

#### Inject Member Constraints

| Constraint | Requirement | Reason |
|------------|-------------|---------|
| Member Type | interface or regular class | Must be injectable type |
| Member Type | Cannot be Node/Host/User/Scope | These are not service types |
| static Members | Not allowed | Needs instance-level injection |
| Properties | Must have setter | Needs to write injected value |

```csharp
[User]
public partial class MyUser : Node
{
    [Inject] private IService _service;           // ✅ Correct
    [Inject] private MyConcreteClass _concrete;   // ✅ Allowed but not recommended
    [Inject] private Node _node;                  // ❌ Error
    [Inject] private MyHost _host;                // ❌ Error
    [Inject] private static IService _static;     // ❌ Error
}
```

#### IServicesReady Interface

User types can implement the `IServicesReady` interface to receive notifications when all dependencies are ready:

```csharp
public interface IServicesReady
{
    void OnServicesReady();
}
```

**Timing**: This method is called immediately after all `[Inject]` members are resolved.

**Use Cases**:
- Initialize state that depends on injected services
- Subscribe to service events
- Start game logic

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

#### Constraints

| Constraint | Requirement | Reason |
|------------|-------------|---------|
| Type | Must be class | Needs instantiation |
| Inheritance | Must be Node | Needs to integrate with scene tree lifecycle |
| Interface | Must implement IScope | Container interface requirements |
| Declaration | Must be partial | Source generator needs to extend the class |
| Attributes | Must have [Modules] | Must specify managed services |

#### Defining a Scope

```csharp
[Modules(
    Services = [typeof(ServiceA), typeof(ServiceB)],
    Hosts = [typeof(HostA), typeof(HostB)]
)]
public partial class GameScope : Node, IScope
{
    // Framework automatically generates IScope implementation
}
```

**Modules Attribute Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| Services | Type[] | List of Singleton service types, must be [Singleton] marked classes |
| Hosts | Type[] | List of Host types, must be [Host] marked classes |

**Parameter Constraints**:

1. **Services**:
   - Must not be empty
   - Each type must be marked with [Singleton]
   - Service implementation types, not interface types

2. **Hosts**:
   - Can be empty (will generate Info level diagnostic)
   - Each type must be marked with [Host]
   - Host class types, not interface types

#### Scope Hierarchy

Scopes form a parent-child hierarchy through the scene tree:

```
RootScope
├── MenuScope
│   └── SettingsMenuScope
└── GameScope
    ├── Level1Scope
    └── Level2Scope
```

**Hierarchy Rules**:
- A Scope searches upward in the scene tree to find its parent Scope
- If a service is not found in the current Scope, it searches in the parent Scope
- Service lifecycle is bound to its defining Scope

#### Service Resolution

When a User or Service requests a dependency:

1. Framework searches for service in the current Scope
2. If not found, continues searching in parent Scope
3. If found, triggers service creation (if not yet created)
4. After service is created, resolves its constructor dependencies
5. After all dependencies are resolved, calls the requester's callback

**Deferred Resolution**:

If a dependency is not yet ready, the request is added to a waiting queue. When the service is registered (Host enters tree or Singleton is created), the waiting queue is automatically notified.

#### Lifecycle Events

Scope listens to these Godot notifications:

| Notification | Behavior |
|--------------|----------|
| `NotificationReady` | Create all Singletons in the Scope |
| `NotificationPredelete` | Release all Singleton instances |
| `NotificationEnterTree/ExitTree` | Clear parent Scope cache |

---

## Lifecycle Management

### Singleton Lifecycle

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

#### Parent-Child Relationship

Scopes form a hierarchy through the scene tree:

```
RootScope (Global)
├── Singleton A (Globally shared)
├── Singleton B (Globally shared)
└── GameScope (Game)
    ├── Singleton C (Only within Game)
    ├── Singleton D (Only within Game)
    └── LevelScope (Level)
        ├── Singleton E (Only within Level)
        └── Singleton F (Only within Level)
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

---

### Dependency Injection Timing

#### User Injection Timing

```
User Node enters scene tree
    ↓
GetServiceScope() finds nearest parent Scope
    ↓
For each [Inject] member:
    ↓
    Scope.ResolveDependency<T>()
    ↓
    Service found? ──Yes→ Immediate callback
    │
    No
    ↓
Add to waiting queue
    ↓
Service created later
    ↓
Notify waiting queue
    ↓
All dependencies resolved
    ↓
Call IServicesReady.OnServicesReady() (if implemented)
```

#### Service Creation Timing

```
Scope.NotificationReady event
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
Create service instance
    ↓
Register to Scope
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

### Role Type Constraints

#### Service Constraints

| Constraint | Requirement | Diagnostic Code |
|------------|-------------|-----------------|
| Type | Must be class | GDI_C060 |
| Inheritance | Cannot be Node | GDI_C060 |
| Modifiers | Cannot be abstract or static | GDI_C060 |
| Declaration | Must be partial | GDI_C050 |

#### Host Constraints

| Constraint | Requirement | Diagnostic Code |
|------------|-------------|-----------------|
| Type | Must be class | (Implicit) |
| Inheritance | Must be Node | GDI_C020 |
| Declaration | Must be partial | GDI_C050 |
| Incompatible Attributes | Cannot have [Singleton] | GDI_C010 |

#### User Constraints

| Constraint | Requirement | Diagnostic Code |
|------------|-------------|-----------------|
| Type | Must be class | (Implicit) |
| Inheritance | Must be Node | GDI_C021 |
| Declaration | Must be partial | GDI_C050 |
| Incompatible Attributes | Cannot have [Singleton] | GDI_C011 |

#### Scope Constraints

| Constraint | Requirement | Diagnostic Code |
|------------|-------------|-----------------|
| Type | Must be class | (Implicit) |
| Inheritance | Must be Node | GDI_C022 |
| Interface | Must implement IScope | (Implicit) |
| Declaration | Must be partial | GDI_C050 |
| Required Attributes | Must have [Modules] | GDI_C040 |
| Incompatible Attributes | Cannot have [Singleton]/[Host]/[User] | GDI_C012 |

---

### Injectable Type Constraints

Types that can be injected (appear in constructor parameters or [Inject] members):

| Type Category | Allowed | Examples |
|---------------|---------|----------|
| Interface | ✅ | IService, IRepository |
| Regular Class | ✅ | ConcreteService (not recommended) |
| Node | ❌ | Node, Control, CharacterBody3D |
| Host Type | ❌ | Types marked with [Host] |
| User Type | ❌ | Types marked with [User] |
| Scope Type | ❌ | Types implementing IScope |
| Abstract Class | ❌ | abstract class Base |
| Static Class | ❌ | static class Util |
| Open Generic | ❌ | Service<T> |

**Diagnostic Codes**:
- Constructor parameters: `GDI_S020`
- [Inject] members: `GDI_M040`, `GDI_M041`, `GDI_M042`, `GDI_M043`

---

### Service Implementation Type Constraints

Types that can be marked with [Singleton]:

| Constraint | Allowed | Diagnostic Code |
|------------|---------|-----------------|
| Must be class | ✅ | - |
| Cannot be Node | ❌ | GDI_C060 |
| Cannot be abstract | ❌ | GDI_C060 |
| Cannot be static | ❌ | GDI_C060 |
| Cannot be open generic | ❌ | GDI_C060 |

```csharp
// ✅ Allowed
[Singleton(typeof(IService))]
public partial class MyService : IService { }

// ❌ Not allowed
[Singleton(typeof(IService))]
public partial class MyNode : Node, IService { }  // GDI_C060

[Singleton(typeof(IService))]
public abstract partial class MyAbstractService : IService { }  // GDI_C060

[Singleton(typeof(IService))]
public static partial class MyStaticService { }  // GDI_C060
```

---

### Exposed Type Constraints

Types specified in [Singleton] parameters (service exposed types):

| Constraint | Recommendation | Diagnostic Code |
|------------|----------------|-----------------|
| Should be interface | ⚠️ Recommended | GDI_M060 (Warning) |
| Must be implemented by service | ✅ Required | GDI_C070 (Service), GDI_M070 (Host) |

```csharp
// ✅ Recommended: Expose interface
[Singleton(typeof(IService))]
public partial class MyService : IService { }

// ⚠️ Warning: Expose concrete class (generates warning GDI_M060)
[Singleton(typeof(MyService))]
public partial class MyService { }

// ❌ Error: Service does not implement exposed type
[Singleton(typeof(IOtherInterface))]
public partial class MyService : IService { }  // GDI_C070
```

---

### Other Constraints

#### Constructor Constraints

| Constraint | Requirement | Diagnostic Code |
|------------|-------------|-----------------|
| Service must have constructor | At least one non-static constructor | GDI_S010 |
| Multiple constructors | Must specify unique [InjectConstructor] | GDI_S011 |
| [InjectConstructor] usage | Can only be used on Service | GDI_S012 |

#### Member Constraints

| Member Type | Constraint | Diagnostic Code |
|-------------|------------|-----------------|
| [Inject] field | Non-readonly | GDI_M020 |
| [Inject] property | Must have setter | GDI_M020 |
| [Singleton] property | Must have getter | GDI_M030 |
| [Inject] / [Singleton] | Cannot coexist | GDI_M012 |
| [Inject] / [Singleton] | Cannot be static | GDI_M044, GDI_M045 |

#### Scope Module Constraints

| Constraint | Requirement | Diagnostic Code |
|------------|-------------|-----------------|
| Services | Cannot be empty | GDI_D001 |
| Services elements | Must be Service types | GDI_D003 |
| Hosts | Can be empty (Info) | GDI_D002 |
| Hosts elements | Must be Host types | GDI_D004 |

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

**Constraints**:
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
- Can only be used on Service types
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
- `Services`: List of Singleton service types (must not be empty)
- `Hosts`: List of Host types (can be empty)

---

### Interfaces

#### IScope

Container interface, manages service registration and resolution.

```csharp
namespace GodotSharpDI.Abstractions;

public interface IScope
{
    void ResolveDependency<T>(Action<T> onResolved);
    void RegisterService<T>(T instance);
    void UnregisterService<T>();
}
```

**Methods**:

| Method | Description | When to Use |
|--------|-------------|-------------|
| `ResolveDependency<T>` | Request a dependency | Called automatically by framework, no manual call needed |
| `RegisterService<T>` | Register a service | Called automatically by framework, no manual call needed |
| `UnregisterService<T>` | Unregister a service | Called automatically by framework, no manual call needed |

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

**Usage**:
- Can only be implemented by User or Host+User types
- Called immediately after all [Inject] members are resolved
- Suitable for initialization logic that depends on injected services

---

### Generated Code

The framework generates different code for different role types through Source Generator.

#### Common Generated Methods for Node Types

All Node types (Host, User, Scope) generate:

```csharp
// Find nearest parent Scope
private IScope? GetServiceScope();

// Attach to Scope when entering scene tree
private void AttachToScope();

// Detach from Scope when exiting scene tree
private void UnattachToScope();

// Override Godot's notification method
public override void _Notification(int what);
```

#### User Generated Methods

For types marked with `[User]`, the framework generates:

```csharp
// Resolve User dependencies
private void ResolveUserDependencies(IScope scope);
```

#### Host Generated Methods

For types marked with `[Host]`, the framework generates:

```csharp
// Register Host services to Scope
private void AttachHostServices(IScope scope);

// Unregister Host services from Scope
private void UnattachHostServices(IScope scope);
```

#### Service Generated Methods

For services marked with `[Singleton]`, the framework generates factory methods:

```csharp
// Create service instance
public static void CreateService(
    IScope scope,
    Action<object, IScope> onCreated
);
```

#### Scope Generated Methods

For types implementing `IScope`, the framework generates complete container implementation:

```csharp
// Static collections
private static readonly HashSet<Type> ServiceTypes;

// Instance fields
private readonly Dictionary<Type, object> _services;
private readonly Dictionary<Type, List<Action<object>>> _waiters;
private readonly HashSet<IDisposable> _disposableSingletons;
private IScope? _parentScope;

// Lifecycle methods
private IScope? GetParentScope();
private void InstantiateScopeSingletons();
private void DisposeScopeSingletons();
private void CheckWaitList();
public override void _Notification(int what);

// IScope implementation
void IScope.ResolveDependency<T>(Action<T> onResolved);
void IScope.RegisterService<T>(T instance);
void IScope.UnregisterService<T>();
```

---

### Scene Tree Integration

#### Lifecycle Events

The framework listens to the following Godot notifications:

| Notification | Processing |
|--------------|-----------|
| `NotificationEnterTree` | User: Attach to Scope, trigger injection<br>Host: Register services<br>Scope: Clear parent Scope cache |
| `NotificationExitTree` | User: Clear Scope reference<br>Host: Unregister services<br>Scope: Clear parent Scope cache |
| `NotificationReady` | Scope: Create Singletons, check waiting queue |
| `NotificationPredelete` | Scope: Release all services |

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
