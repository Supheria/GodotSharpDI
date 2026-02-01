# GodotSharpDI

<img src="icon.png" style="zoom:50%;" />

<p align="left"> <a href="README.zh-CN.md">中文版</a> </p>

A compile-time dependency injection framework designed specifically for Godot Engine, providing zero-reflection, high-performance DI support through C# Source Generator.

[![NuGet Version](https://img.shields.io/nuget/v/GodotSharpDI.svg?style=flat)](https://www.nuget.org/packages/GodotSharpDI/)

## Table of Contents

- [Design Philosophy](#design-philosophy)
- [Installation](#installation)
- [Quick Start](#quick-start)
  - [1. Define Services](#1-define-services)
  - [2. Define Scope](#2-define-scope)
  - [3. Define Host](#3-define-host)
  - [4. Define User](#4-define-user)
  - [5. Scene Tree Structure](#5-scene-tree-structure)
- [Core Concepts](#core-concepts)
  - [Four Role Types](#four-role-types)
  - [Service Lifecycle](#service-lifecycle)
- [Role Details](#role-details)
  - [Singleton Services](#singleton-services)
  - [Host](#host)
  - [User](#user)
  - [Scope](#scope)
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
  - [Host + User Combination](#host--user-combination)
- [Diagnostic Codes](#diagnostic-codes)
- [License](#license)
- [Todo List](#todo-list)

---

## Design Philosophy

The core design philosophy of GodotSharpDI is **merging Godot's scene tree lifecycle with traditional DI container patterns**:

- **Scene Tree as Container Hierarchy**: Utilize Godot's scene tree structure to implement Scope hierarchy
- **Node Lifecycle Integration**: Bind service creation and destruction to Node's enter/exit scene tree events
- **Compile-Time Safety**: Complete dependency analysis and code generation during compilation through Source Generator, providing comprehensive compile-time error checking

---

## Installation

```xml
<PackageReference Include="GodotSharpDI" Version="x.x.x" />
```
⚠️ **Ensure GodotSharp package is also added to your project**: The generated code depends on Godot.Node and Godot.GD.

---

## Quick Start

### 1. Define Services

```csharp
// Define service interface
public interface IPlayerStats
{
    int Health { get; set; }
    int Mana { get; set; }
}

// Implement service (Singleton lifecycle)
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;
}
```

### 2. Define Scope

```csharp
[Modules(
    Services = [typeof(PlayerStatsService)],
    Hosts = [typeof(GameManager)]
)]
public partial class GameScope : Node, IScope
{
    // Framework auto-generates IScope implementation
}
```

### 3. Define Host

```csharp
[Host]
public partial class GameManager : Node, IGameState
{
    // Expose self as IGameState service
    [Singleton(typeof(IGameState))]
    private IGameState Self => this;
    
    public GameState CurrentState { get; set; }
}
```

### 4. Define User

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

### 5. Scene Tree Structure

```
GameScope (IScope)
├── GameManager (Host)
├── Player
│   └── PlayerUI (User) ← Automatically receives injection
└── Enemies
```

---

## Core Concepts

### Four Role Types

| Role | Description | Constraints |
|------|-------------|-------------|
| **Singleton Service** | Pure logic service, unique within Scope, created and managed by Scope, released when Scope is destroyed | Must be a non-Node class |
| **Host** | Scene-level resource provider, bridging Node resources to the DI world | Must be a Node |
| **User** | Dependency consumer, receives injection | Must be a Node |
| **Scope** | DI container, manages service lifecycle | Must be a Node implementing IScope |

---

## Role Details

### Singleton Services

#### Responsibilities

Types marked with [Singleton] are pure logic services that encapsulate business logic and data processing, **independent of Godot's Node system**.

#### Constraints

| Constraint | Requirement | Reason |
|------------|-------------|---------|
| Type | Must be a class | Needs instantiation |
| Inheritance | Cannot be a Node | Node lifecycle is controlled by Godot, conflicts with DI container |
| Modifiers | Cannot be abstract or static | Needs instantiation |
| Generics | Cannot be open generic | Requires concrete type for instantiation |
| Declaration | Must be partial | Source generator needs to extend the class |

#### Lifecycle Annotation

```csharp
// Singleton: Single instance within Scope
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
2. When there's only one constructor, it's used as the default constructor, regardless of `[InjectConstructor]` annotation
3. If multiple constructors exist, must specify a unique `[InjectConstructor]`

```csharp
[Singleton(typeof(IService))]
public partial class MyService : IService
{
    // Must specify when multiple constructors exist
    [InjectConstructor]
    public MyService(IDep1 dep1) { }
    
    public MyService(IDep1 dep1, IDep2 dep2) { }
}
```

#### Exposed Types

Specify exposed service types through [Singleton] parameters:

```csharp
// Expose single interface
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats { }

// Expose multiple interfaces
[Singleton(typeof(IReader), typeof(IWriter))]
public partial class FileService : IReader, IWriter { }

// Without parameters, expose the class itself (not recommended)
[Singleton]  // Exposes ConfigService type
public partial class ConfigService { }
```

> ⚠️ **Best Practice**: Always expose interfaces rather than concrete classes to maintain loose coupling and testability.

---

### Host

#### Responsibilities

Host is the bridge between Godot's Node system and the DI system, exposing Node-managed resources as injectable services.

#### Constraints

| Constraint | Requirement | Reason |
|------------|-------------|---------|
| Type | Must be a class | Needs instantiation |
| Inheritance | Must be a Node | Needs integration with scene tree lifecycle |
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
    
    // Interface implementation
    public Chunk GetChunk(Vector3I pos) => _chunks.GetValueOrDefault(pos);
    public void LoadChunk(Vector3I pos) { /* ... */ }
}
```

This is the most typical Host usage: the Node implements service interfaces and exposes itself to the DI system.

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

Host can hold and manage other objects, exposing them as services. **The lifecycle of these objects is controlled by the Host.**

#### Host Member Constraints

| Constraint | Requirement | Reason |
|------------|-------------|---------|
| Member Type | Cannot be a type already marked as Service | Avoid lifecycle conflicts |
| static Members | Not allowed | Need instance-level services |
| Properties | Must have getter | Need to read value to register service |

```csharp
// ❌ Error: Types marked as [Singleton] can only be held by Scope
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

### User

#### Responsibilities

User is the dependency consumer, receiving service dependencies through field or property injection.

#### Constraints

| Constraint | Requirement | Reason |
|------------|-------------|---------|
| Type | Must be a class | Needs instantiation |
| Inheritance | Must be a Node | Needs integration with scene tree lifecycle |
| Declaration | Must be partial | Source generator needs to extend the class |

#### User Auto Dependency Injection

```csharp
[User]
public partial class PlayerController : CharacterBody3D, IServicesReady
{
    [Inject] private IPlayerStats _stats;
    [Inject] private ICombatSystem _combat;
    
    // Automatically called when all dependencies are injected
    public void OnServicesReady()
    {
        GD.Print("All services ready, can start game logic");
    }
}
```

Node-type Users automatically trigger injection when entering the scene tree, no manual operation required.

#### Inject Member Constraints

| Constraint | Requirement | Reason |
|------------|-------------|---------|
| Member Type | interface or regular class | Must be injectable type |
| Member Type | Cannot be Node/Host/User/Scope | These are not service types |
| static Members | Not allowed | Need instance-level injection |
| Properties | Must have setter | Need to write injection value |

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

Implementing the `IServicesReady` interface allows you to receive notification after all dependencies are injected:

```csharp
[User]
public partial class MyComponent : Node, IServicesReady
{
    [Inject] private IServiceA _a;
    [Inject] private IServiceB _b;
    [Inject] private IServiceC _c;
    
    // Called when _a, _b, _c are all injected
    public void OnServicesReady()
    {
        // Safely use all dependencies
        Initialize();
    }
}
```

---

### Scope

#### Responsibilities

Scope is the DI container, responsible for:

1. Creating and managing Singleton service instances
2. Collecting service instances provided by Hosts
3. Processing dependency resolution requests
4. Managing the lifecycle of service instances it creates

#### Constraints

| Constraint | Requirement | Reason |
|------------|-------------|---------|
| Type | Must be a class | Needs instantiation |
| Inheritance | Must be a Node | Utilize scene tree for Scope hierarchy |
| Interface | Must implement IScope | Framework identification marker |
| Attribute | Must have [Modules] or [AutoModules] | Declare managed services |
| Declaration | Must be partial | Source generator needs to extend the class |

#### Defining Scope

```csharp
[Modules(
    Services = [typeof(PlayerStatsService), typeof(CombatSystem)],
    Hosts = [typeof(GameManager)]
)]
public partial class GameScope : Node, IScope
{
    // Framework auto-generates complete container implementation
}
```

#### Service Registration Process

When Scope enters the scene tree:

1. **Ready phase**: Create all Singleton services
2. **Host registration**: Collect services provided by Hosts in the scene tree
3. **User injection**: Inject dependencies into Users

```
GameScope._Notification(NotificationReady)
    ↓
InstantiateScopeSingletons()  // Create Singleton services
    ↓
(Host enters tree)
    ↓
AttachHostServices()  // Register Host services
    ↓
(User enters tree)
    ↓
ResolveUserDependencies()  // Inject dependencies
```

---

## Lifecycle Management

### Singleton Lifecycle

```
Scope.NotificationReady
    ↓
Create Singleton Instance
    ↓
Register to Scope
    ↓
(Service used by other services/Users)
    ↓
Scope.NotificationPredelete
    ↓
Call IDisposable.Dispose() (if implemented)
    ↓
Release Instance
```

### Scope Hierarchy

```
RootScope (Global services)
├── MenuScope (Menu services)
└── GameScope (Game services)
    ├── LevelScope (Level services)
    │   └── AreaScope (Area services)
    └── UIScope (UI services)
```

**Dependency Resolution**:

1. Search in current Scope
2. If not found, search in parent Scope
3. Repeat until found or reach root

### Dependency Injection Timing

```
Scope Ready
    ↓
Create Singleton A (depends on: none)
    ↓
Create Singleton B (depends on: A) ← A already registered
    ↓
Host enters tree
    ↓
Register Host services
    ↓
User enters tree
    ↓
Resolve dependencies (Singleton + Host services)
    ↓
Inject into User
    ↓
IServicesReady.OnServicesReady() called
```

### Host + User and Circular Dependencies

When a Node is both Host and User:

```csharp
[Host, User]
public partial class GameManager : Node, IGameState
{
    [Singleton(typeof(IGameState))]
    private IGameState Self => this;
    
    [Inject] private IConfig _config;
}
```

**Injection Timing**:

1. NotificationEnterTree: Register Host services first
2. Then inject User dependencies
3. This breaks potential circular dependencies

---

## Type Constraints

### Role Type Constraints

| Role | Type | Inheritance | Modifiers | Declaration |
|------|------|-------------|-----------|-------------|
| Service | Must be class | Cannot be Node | Cannot be abstract/static | Must be partial |
| Host | Must be class | Must be Node | - | Must be partial |
| User | Must be class | Must be Node | - | Must be partial |
| Scope | Must be class | Must be Node | - | Must be partial |

### Injectable Type Constraints

Dependencies that can be injected (constructor parameters or [Inject] members):

| Allowed | Not Allowed |
|---------|-------------|
| interface | Node or its subclasses |
| class (not Node) | Types marked as [Host] |
| | Types marked as [User] |
| | Types implementing IScope |
| | abstract class |
| | static class |
| | Open generic types |

### Service Implementation Type Constraints

For types marked as [Singleton]:

| Allowed | Not Allowed |
|---------|-------------|
| Regular class | Node or its subclasses |
| Implementing interface | abstract class |
| | static class |
| | Open generic types |

### Exposed Type Constraints

Types specified in [Singleton(...)] parameters:

| Recommended | Allowed but Warned | Not Allowed |
|-------------|-------------------|-------------|
| interface | concrete class | Not implemented by the class |
| | | Non-inheritance relationship |

### Other Constraints

| Scenario | Constraint | Reason |
|----------|-----------|---------|
| [Inject] member | Cannot be static | Need instance-level injection |
| [Inject] property | Must have setter | Need to write injection value |
| [Singleton] member | Cannot be static | Need instance-level service |
| [Singleton] property | Must have getter | Need to read value to register |
| Host [Singleton] member | Cannot be a Service type | Avoid lifecycle conflicts |

---

## API Reference

### Attributes

#### SingletonAttribute

Marks a class as a Singleton service with specified exposed types.

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property, 
                Inherited = false, AllowMultiple = false)]
public sealed class SingletonAttribute : Attribute
{
    public Type[] ServiceTypes { get; }
    public SingletonAttribute(params Type[] serviceTypes);
}
```

**Parameters**:

- `ServiceTypes`: Service types to expose (interfaces or classes)

**Usage**:

```csharp
// On class
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats { }

// On Host member
[Host]
public partial class GameManager : Node, IGameState
{
    [Singleton(typeof(IGameState))]
    private IGameState Self => this;
}
```

---

#### HostAttribute

Marks a class as a Host (resource provider).

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class HostAttribute : Attribute { }
```

---

#### UserAttribute

Marks a class as a User (service consumer).

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class UserAttribute : Attribute { }
```

---

#### InjectAttribute

Marks a field or property as an injection target.

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class InjectAttribute : Attribute { }
```

**Usage**:

```csharp
[User]
public partial class MyComponent : Node
{
    [Inject] private IService _service;           // Field
    [Inject] public IConfig Config { get; set; }  // Property (needs setter)
}
```

---

#### InjectConstructorAttribute

Specifies the constructor to use for a Service.

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Constructor)]
public sealed class InjectConstructorAttribute : Attribute { }
```

**Usage**:

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

Declares services and expected Hosts managed by a Scope.

```csharp
namespace GodotSharpDI.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ModulesAttribute : Attribute
{
    public Type[] Services { get; set; }
    public Type[] Hosts { get; set; }
}
```

**Parameters**:

| Parameter | Description |
|-----------|-------------|
| `Services` | List of Service types created and managed by the Scope |
| `Hosts` | List of Host types expected by the Scope |

**Usage**:

```csharp
[Modules(
    Services = [typeof(PlayerStatsService), typeof(CombatSystem)],
    Hosts = [typeof(GameManager), typeof(WorldManager)]
)]
public partial class GameScope : Node, IScope { }
```

---

### Interfaces

#### IScope

DI container interface.

```csharp
namespace GodotSharpDI.Abstractions;

public interface IScope
{
    void RegisterService<T>(T instance) where T : notnull;
    void UnregisterService<T>() where T : notnull;
    void ResolveDependency<T>(Action<T> onResolved) where T : notnull;
}
```

**Methods**:

- `RegisterService<T>`: Register service instance (automatically called by framework, manual calls trigger GDI_U001)
- `UnregisterService<T>`: Unregister service (automatically called by framework, manual calls trigger GDI_U001)
- `ResolveDependency<T>`: Resolve dependency: callback immediately if service is registered; otherwise add to wait queue (automatically called by framework, manual calls trigger GDI_U001)

---

#### IServicesReady

Service ready notification interface.

```csharp
namespace GodotSharpDI.Abstractions;

public interface IServicesReady
{
    void OnServicesReady();
}
```

**Usage**:

```csharp
[User]
public partial class MyComponent : Node, IServicesReady
{
    [Inject] private IServiceA _a;
    [Inject] private IServiceB _b;
    
    public void OnServicesReady()
    {
        // All dependencies injected, safe to use
        _a.Initialize();
        _b.Connect(_a);
    }
}
```

---

### Generated Code

#### Node User Generated Methods

For Node types marked as `[User]`, the framework generates:

```csharp
// Service Scope reference
private IScope? _serviceScope;

// Get nearest Scope
private IScope? GetServiceScope();

// Attach to Scope (inject dependencies)
private void AttachToScope();

// Detach from Scope (for Host)
private void UnattachToScope();

// Lifecycle notification handler
public override void _Notification(int what);

// Resolve user dependencies
private void ResolveUserDependencies(IScope scope);
```

#### Host Generated Methods

For types marked as `[Host]`, the framework generates:

```csharp
// Register Host services to Scope
private void AttachHostServices(IScope scope);

// Unregister Host services from Scope
private void UnattachHostServices(IScope scope);
```

#### Service Generated Methods

For services marked as `[Singleton]`, the framework generates factory methods:

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
|--------------|------------|
| `NotificationEnterTree` | User: Attach to Scope, trigger injection<br>Host: Register services<br>Scope: Clear parent Scope cache |
| `NotificationExitTree` | User: Clear Scope reference<br>Host: Unregister services<br>Scope: Clear parent Scope cache |
| `NotificationReady` | Scope: Create Singletons, check wait queue |
| `NotificationPredelete` | Scope: Release all services |

#### Scene Tree Search

Scope retrieval logic:

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
    
    GD.PushError("No Service Scope found");
    return null;
}
```

---

## Best Practices

### Scope Granularity Design

```csharp
// ✅ Good design: Divide Scopes by function/lifecycle
RootScope          // Global services
├── MainMenuScope  // Main menu services
└── GameScope      // Game services
    └── LevelScope // Level services

// ❌ Avoid: Too many or too few Scopes
// Too many: One Scope per Node (over-engineering)
// Too few: One Scope for entire game (no isolation)
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

// ⚠️ Not recommended: Expose concrete classes
[Singleton(typeof(ConfigService))]
public partial class ConfigService { }
```

**Reasons**:

- Interfaces provide better loose coupling
- Easier to unit test (using mocks)
- Easier to replace implementations

---

### Host + User Combination

A Node can be both Host and User simultaneously:

```csharp
[Host, User]
public partial class GameManager : Node, IGameState, IServicesReady
{
    // Host part: Expose services
    [Singleton(typeof(IGameState))]
    private IGameState Self => this;
    
    // User part: Inject dependencies
    [Inject] private IConfig _config;
    [Inject] private ISaveSystem _saveSystem;
    
    public void OnServicesReady()
    {
        // Dependencies ready, can initialize
        LoadLastSave();
    }
}
```

This is very useful for Nodes that need to both provide and consume services.

---

## Diagnostic Codes

The framework provides comprehensive compile-time error checking. For a complete list of diagnostic codes, see [DIAGNOSTICS.md](./DIAGNOSTICS.md).

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

- [ ] Complete bilingual Chinese-English support
- [ ] Add sample projects (running GodotSharpDI.Sample from actual Godot)
- [ ] Add runtime integration tests
- [ ] Implement waiting timing and timeout handling for dependency callbacks
- [ ] Enhance code comment coverage for generated code
- [ ] Diagnose generator internal errors (GDI_E)
