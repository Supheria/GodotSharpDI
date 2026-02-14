# GodotSharpDI

<img src="icon.png" style="zoom:50%;" />

<p align="left"> <a href="README.zh-CN.md">中文</a> </p>

A compile-time dependency injection framework specifically designed for the Godot Engine 4, implementing zero-reflection, high-performance DI support through C# Source Generator.

[![NuGet Version](https://img.shields.io/nuget/v/GodotSharpDI.svg?style=flat)](https://www.nuget.org/packages/GodotSharpDI/)

## Table of Contents

- [Design Philosophy](#design-philosophy)
- [Installation](#installation)
- [Quick Start](#quick-start)
  - [1. Define Service Interfaces](#1-define-service-interfaces)
  - [2. Define a Host with Service Providers](#2-define-a-host-with-service-providers)
  - [3. Define a Scope](#3-define-a-scope)
  - [4. Define a User](#4-define-a-user)
  - [5. Scene Tree Structure](#5-scene-tree-structure)
- [Core Concepts](#core-concepts)
  - [Three Role Types](#three-role-types)
  - [Service Lifecycle](#service-lifecycle)
- [Role Details](#role-details)
  - [Host (Service Provider)](#host-service-provider)
  - [User (Consumer)](#user-consumer)
  - [Scope (Container)](#scope-container)
- [Service Provision with [Provide]](#service-provision-with-provide)
  - [Property Providers](#property-providers)
  - [Method Providers](#method-providers)
  - [Asynchronous Providers](#asynchronous-providers)
  - [WaitFor Mechanism](#waitfor-mechanism)
- [Lifecycle Management](#lifecycle-management)
  - [Service Lifecycle](#service-lifecycle-1)
  - [Scope Hierarchy](#scope-hierarchy)
  - [Dependency Injection Timing](#dependency-injection-timing)
  - [Host + User and Circular Dependencies](#host--user-and-circular-dependencies)
- [Type Constraints](#type-constraints)
  - [Role Type Constraints](#role-type-constraints)
  - [Injectable Type Constraints](#injectable-type-constraints)
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
- [Migration Guide from 1.0.0-rc.3](#migration-guide-from-100-rc3)
- [Diagnostic Codes](#diagnostic-codes)
- [License](#license)
- [Appendix: _Notification method explicitly definition requirement](#appendix-_notification-method-explicitly-definition-requirement)
- [Todo List](#todo-list)

---

## Design Philosophy

The core design philosophy of GodotSharpDI is to **merge Godot's scene tree lifecycle with traditional DI container patterns**:

- **Scene Tree as Container Hierarchy**: Leverages Godot's scene tree structure to implement Scope hierarchy
- **Node Lifecycle Integration**: Service creation and destruction are bound to Node's enter/exit scene tree events
- **Compile-Time Safety**: Completes dependency analysis and code generation at compile time through Source Generator, providing comprehensive compile-time error checking
- **Provider-Based Architecture**: Services are provided by Hosts through the `[Provide]` attribute, offering greater flexibility and control

---

## Installation

```xml
<PackageReference Include="GodotSharpDI" Version="1.1.0-rc.1" />
```
⚠️ **Make sure to also add the GodotSharp package to your project**: The generated code depends on Godot.Node and Godot.GD.

---

## Quick Start

### 1. Define Service Interfaces

```csharp
// Define service interface
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

### 2. Define a Host with Service Providers

```csharp
[Host]
public partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // Provide itself as IGameState service
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
    
    // Provide IPlayerStats service
    [Provide(ExposedTypes = [typeof(IPlayerStats)])]
    public IPlayerStats CreatePlayerStats()
    {
        return new PlayerStatsService { Health = 100, Mana = 50 };
    }
    
    public GameState CurrentState { get; set; }
    
    // Called after all dependencies are resolved
    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        if (isAllDependenciesReady)
        {
            GD.Print("GameManager ready with all dependencies");
        }
    }
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}

// Service implementation (doesn't need [Singleton] anymore)
public class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; }
    public int Mana { get; set; }
}
```

### 3. Define a Scope

```csharp
[Modules(Hosts = [typeof(GameManager)])]
public partial class GameScope : Node, IScope
{
    // Framework automatically generates IScope implementation
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}
```

### 4. Define a User

```csharp
[User]
public partial class PlayerUI : Control, IDependenciesResolved
{
    [Inject] private IPlayerStats _stats;
    [Inject] private IGameState _gameState;
    
    // Called after all dependencies are resolved
    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        if (isAllDependenciesReady)
        {
            UpdateUI();
        }
        else
        {
            GD.Print("Some dependencies failed to inject");
        }
    }
    
    private void UpdateUI()
    {
        GD.Print($"Health: {_stats.Health}, Mana: {_stats.Mana}");
        GD.Print($"Game State: {_gameState.CurrentState}");
    }
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}
```

### 5. Scene Tree Structure

```
GameScope (IScope)
├── GameManager (Host) ← Provides services
└── PlayerUI (User) ← Consumes services
```

---

## Core Concepts

### Three Role Types

| Role | Description | Constraints |
|------|-------------|-------------|
| **Host** | Service provider, bridges Node resources to the DI world, provides services through `[Provide]` members | Must be Node |
| **User** | Dependency consumer, receives injection | Must be Node |
| **Scope** | DI container, manages service lifecycle | Must be Node, implements IScope |

**Key Change in 1.1.0**: The `[Singleton]` attribute and standalone service classes have been removed. Services are now provided directly by Hosts through the `[Provide]` attribute, offering a more flexible and unified architecture.

---

## Role Details

### Host (Service Provider)

#### Responsibilities

Host is the bridge between the Godot Node system and the DI system, providing services through `[Provide]` members.

#### Service Provision Methods

Hosts can provide services through:

1. **Properties** - Simple, synchronous service provision
2. **Methods** - Flexible service creation with parameters
3. **Async Methods** - Support for asynchronous initialization

```csharp
[Host]
public partial class ServiceHost : Node
{
    // Property provider
    [Provide(ExposedTypes = [typeof(IConfig)])]
    public IConfig Config => new ConfigService();
    
    // Method provider
    [Provide(ExposedTypes = [typeof(IDatabase)])]
    public IDatabase CreateDatabase()
    {
        return new DatabaseService("connection-string");
    }
    
    // Async provider
    [Provide(ExposedTypes = [typeof(IAsyncService)])]
    public async Task<IAsyncService> InitializeAsync()
    {
        var service = new AsyncService();
        await service.InitializeAsync();
        return service;
    }
    
    // Required for Godot lifecycle integration
    public override partial void _Notification(int what);
}
```

#### Host as Service Consumer

Hosts can also be service consumers by adding `[Inject]` members:

```csharp
[Host]
public partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // Consume services
    [Inject] private IConfig _config;
    
    // Provide services
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
    
    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        if (isAllDependenciesReady)
        {
            // Initialize with injected dependencies
            InitializeGame();
        }
    }
    
    public override partial void _Notification(int what);
}
```

---

### User (Consumer)

#### Responsibilities

Users are service consumers that receive injected dependencies.

#### Dependency Injection

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
            // All dependencies are ready
            InitializeController();
        }
        else
        {
            GD.PrintErr("Failed to inject some dependencies");
        }
    }
    
    public override partial void _Notification(int what);
}
```

---

### Scope (Container)

#### Responsibilities

Scope is the DI container that manages service lifecycle and coordinates dependency injection.

#### Declaration

```csharp
[Modules(Hosts = [typeof(GameManager), typeof(ServiceHost)])]
public partial class GameScope : Node, IScope
{
    // Framework generates all implementation
    public override partial void _Notification(int what);
}
```

#### Scope Hierarchy

```
RootScope (IScope)
├── Host1 (Host)
├── User1 (User)
└── SubScope (IScope)
    ├── Host2 (Host)
    └── User2 (User)
```

Services provided by parent scopes are accessible to child scopes.

---

## Service Provision with [Provide]

### Property Providers

The simplest way to provide services:

```csharp
[Host]
public partial class ConfigHost : Node
{
    [Provide(ExposedTypes = [typeof(IConfig)])]
    public IConfig Config => new ConfigService();
    
    // Can expose multiple types
    [Provide(ExposedTypes = [typeof(IReader), typeof(IWriter)])]
    public FileService FileService => new FileService();
    
    public override partial void _Notification(int what);
}
```

### Method Providers

More flexible service creation:

```csharp
[Host]
public partial class FactoryHost : Node
{
    [Inject] private IConfig _config;
    
    [Provide(ExposedTypes = [typeof(IDatabase)])]
    public IDatabase CreateDatabase()
    {
        // Can use injected dependencies
        var connectionString = _config.GetConnectionString();
        return new DatabaseService(connectionString);
    }
    
    [Provide(ExposedTypes = [typeof(ICache)])]
    public ICache CreateCache()
    {
        // Can implement complex initialization logic
        var cache = new CacheService();
        cache.Initialize();
        return cache;
    }
    
    public override partial void _Notification(int what);
}
```

### Asynchronous Providers

Support for async initialization with automatic thread safety via `CallDeferred`:

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

**Thread Safety**: When async providers complete (potentially on background threads), the framework automatically uses Godot's `CallDeferred` mechanism to marshal results back to the main thread. This ensures all service registration happens on Godot's main thread, preventing crashes and ensuring thread safety.

**What happens internally**:
```csharp
// You write this:
[Provide(ExposedTypes = [typeof(IDatabase)])]
public async Task<IDatabase> ConnectAsync() { ... }

// Framework generates this:
private static async Task ProvideAsync_ConnectAsync_IDatabase(Task<IDatabase> task, IScope scope)
{
    try
    {
        var result = await task; // May complete on background thread
        
        // Automatically use CallDeferred to return to main thread
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

### WaitFor Mechanism

**New in 1.1.0**: Services can wait for other services to be ready before being provided.

```csharp
[Host]
public partial class DependentHost : Node, IDependenciesResolved
{
    [Inject] private IConfig _config;
    
    // This service will be provided immediately
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger CreateLogger()
    {
        return new Logger();
    }
    
    // This service waits for CreateLogger to complete
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(CreateLogger)])]
    public IDatabase CreateDatabase()
    {
        // Logger is guaranteed to be available
        return new DatabaseService();
    }
    
    // This service waits for both _config injection and CreateDatabase
    [Provide(ExposedTypes = [typeof(IRepository)], WaitFor = [nameof(_config), nameof(CreateDatabase)])]
    public IRepository CreateRepository()
    {
        // Both config and database are guaranteed to be ready
        return new Repository(_config, /* database will be injected */);
    }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        GD.Print("All dependencies resolved");
    }
    
    public override partial void _Notification(int what);
}
```

**WaitFor Rules**:
- Can wait for `[Inject]` members (e.g., `nameof(_config)`)
- Can wait for other `[Provide]` members (e.g., `nameof(CreateLogger)`)
- Circular waits are detected at compile time
- Supports complex dependency chains

---

## Lifecycle Management

### Service Lifecycle

1. **Creation**: Services are created when:
   - A Scope enters the scene tree
   - Hosts register their providers
   - Users request injection

2. **Destruction**: Services are destroyed when:
   - The providing Scope exits the scene tree
   - All services are automatically disposed

### Scope Hierarchy

```
RootScope
├── Service A (from RootScope)
└── ChildScope
    ├── Service A (inherited from parent)
    └── Service B (only in child)
```

Child scopes inherit services from parent scopes but can also override them.

### Dependency Injection Timing

1. **Scope enters scene tree** (`NotificationEnterTree`)
2. **Hosts register providers**
3. **Services are created (respecting WaitFor chains)**
4. **Users receive injection**
5. **`OnDependenciesResolved` is called** (after `_Ready`)

### Host + User and Circular Dependencies

A Host can also be a User:

```csharp
[Host]
public partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // As User: inject dependencies
    [Inject] private IConfig _config;
    [Inject] private ISaveSystem _saveSystem;
    
    // As Host: provide service
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
    
    public GameState CurrentState { get; set; }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        if (isAllDependenciesReady)
        {
            // Dependencies are ready, can initialize
            LoadLastSave();
        }
    }
    
    public override partial void _Notification(int what);
}
```

This pattern is **not** a circular dependency because:
1. Host registration happens first
2. Service provision occurs
3. User injection happens afterward
4. No constructor cycle is formed

---

## Type Constraints

### Role Type Constraints

| Role | Allowed Base Types | Forbidden |
|------|-------------------|-----------|
| **Host** | Node and its subclasses | Generic types |
| **User** | Node and its subclasses | Generic types |
| **Scope** | Must implement IScope | Generic types |

### Injectable Type Constraints

- **Recommended**: Inject interfaces (e.g., `IService`)
- **Warning**: Injecting concrete Host types
- **Error**: Injecting User types, Scope types, or regular Node types

### Exposed Type Constraints

- **Recommended**: Expose interfaces
- **Allowed**: Expose the Host type itself
- **Must Implement**: The provider must implement or return the exposed type

### Other Constraints

- Each `[Provide]` member must have at least one exposed type
- WaitFor targets must be valid `[Inject]` or `[Provide]` members
- Circular WaitFor dependencies are compile-time errors

---

## API Reference

### Attributes

#### `[Host]`
Marks a Node as a service provider.

```csharp
[Host]
public partial class ServiceHost : Node
{
    public override partial void _Notification(int what);
}
```

#### `[User]`
Marks a Node as a service consumer.

```csharp
[User]
public partial class ServiceUser : Node
{
    [Inject] private IService _service;
    public override partial void _Notification(int what);
}
```

#### `[Provide(ExposedTypes = [...], WaitFor = [...])]`
Marks a property or method as a service provider.

**Parameters**:
- `ExposedTypes`: Array of types that this service will be registered as
- `WaitFor`: (Optional) Array of member names to wait for before providing

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
Marks a field or property for dependency injection.

```csharp
[Inject] private IService _service;
[Inject] private IConfig Config { get; set; }
```

#### `[Modules(Hosts = [...])]`
Defines which Hosts belong to a Scope.

```csharp
[Modules(Hosts = [typeof(Host1), typeof(Host2)])]
public partial class GameScope : Node, IScope { }
```

### Interfaces

#### `IScope`
Must be implemented by Scope types. The framework generates the implementation.

```csharp
public partial class GameScope : Node, IScope
{
    // Framework generates implementation
}
```

#### `IDependenciesResolved`
Optional interface for receiving dependency resolution notification.

```csharp
public interface IDependenciesResolved
{
    void OnDependenciesResolved(bool isAllDependenciesReady);
}
```

### Generated Code

For each role, the framework generates:

- **Host**: Provider registration, service creation logic
- **User**: Injection logic, dependency resolution
- **Scope**: Service container, lifecycle management

All generated code is in `*.DI.g.cs` files.

### Scene Tree Integration

The framework integrates with Godot's lifecycle through `_Notification`:

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

## Best Practices

### Scope Granularity Design

Design scopes based on functional modules:

```
GameRoot (Scope)
├── GlobalServices (Host) - Config, SaveSystem
├── MainMenu (Scope)
│   └── MenuServices (Host) - UIManager
└── GameLevel (Scope)
    ├── LevelServices (Host) - PhysicsEngine
    └── PlayerServices (Host) - PlayerStats
```

### Service Disposal

Implement `IDisposable` for services that need cleanup:

```csharp
public class DatabaseService : IDatabase, IDisposable
{
    public void Dispose()
    {
        // Cleanup resources
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

### Avoiding Circular Dependencies

**Compile-time Detection**: The framework detects circular WaitFor chains:

```csharp
// ❌ Circular dependency - compile error
[Provide(ExposedTypes = [typeof(IServiceA)], WaitFor = [nameof(CreateB)])]
public IServiceA CreateA() => new ServiceA();

[Provide(ExposedTypes = [typeof(IServiceB)], WaitFor = [nameof(CreateA)])]
public IServiceB CreateB() => new ServiceB();
```

**Solution**: Refactor dependencies or use events:

```csharp
// ✅ Correct approach
[Provide(ExposedTypes = [typeof(IServiceA)])]
public IServiceA CreateA() => new ServiceA();

[Provide(ExposedTypes = [typeof(IServiceB)], WaitFor = [nameof(CreateA)])]
public IServiceB CreateB() => new ServiceB(/* will receive A through injection */);
```

### Interface-First Principle

Always expose interfaces rather than concrete types:

```csharp
// ❌ Not recommended
[Provide(ExposedTypes = [typeof(DatabaseService)])]
public DatabaseService CreateDatabase() => new DatabaseService();

// ✅ Recommended
[Provide(ExposedTypes = [typeof(IDatabase)])]
public IDatabase CreateDatabase() => new DatabaseService();
```

### Host + User Combination Usage

Combine Host and User when a Node needs to both provide and consume services:

```csharp
[Host]
public partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // As User: inject dependencies
    [Inject] private IConfig _config;
    [Inject] private ISaveSystem _saveSystem;
    
    // As Host: provide service
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

### Using Service Factories

Create factory services to manage dynamic object creation:

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

## Migration Guide from 1.0.0-rc.3

### Why 1.1.0 Instead of 1.0.0?

After releasing 1.0.0-rc.3, we identified an architectural limitation: the `[Singleton]` attribute and standalone service classes, while functional, created unnecessary complexity and limited flexibility. The new provider-based architecture in 1.1.0 offers:

- **Greater Flexibility**: Services defined inline with Hosts
- **Better Resource Management**: Direct access to Node resources when creating services
- **Asynchronous Support**: Native support for async service initialization
- **Dependency Ordering**: WaitFor mechanism for complex initialization sequences
- **Simplified Architecture**: One less concept to learn (no more separate Service classes)

Given the magnitude of these changes, we decided to increment to 1.1.0 rather than release 1.0.0 with known limitations.

### Migration Steps

#### 1. Replace [Singleton] Service Classes with [Provide] Methods

**Before (1.0.0-rc.3)**:
```csharp
// Separate service class
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

**After (1.1.0)**:
```csharp
// Service provided by Host
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

// Service implementation (no attributes needed)
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

// Simplified Modules attribute
[Modules(Hosts = [typeof(ServiceHost), typeof(GameManager)])]
public partial class GameScope : Node, IScope
{
    public override partial void _Notification(int what);
}
```

#### 2. Remove [InjectConstructor] Attributes

The `[InjectConstructor]` attribute is no longer needed. Services are created by provider methods, giving you full control over construction.

#### 3. Update Modules Attribute

Remove the `Services` parameter from `[Modules]`:

```csharp
// Before
[Modules(
    Services = [typeof(Service1), typeof(Service2)],
    Hosts = [typeof(Host1)]
)]

// After
[Modules(Hosts = [typeof(Host1)])]
```

#### 4. Use WaitFor for Service Dependencies

If your services have dependencies on other services:

```csharp
[Host]
public partial class ServiceHost : Node, IDependenciesResolved
{
    [Inject] private IConfig _config;
    
    // Logger created first
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger CreateLogger()
    {
        return new Logger();
    }
    
    // Database waits for config injection
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public IDatabase CreateDatabase()
    {
        return new DatabaseService(_config.ConnectionString);
    }
    
    // Repository waits for both
    [Provide(ExposedTypes = [typeof(IRepository)], 
             WaitFor = [nameof(CreateLogger), nameof(CreateDatabase)])]
    public IRepository CreateRepository()
    {
        // Both logger and database are ready
        return new Repository();
    }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady) { }
    public override partial void _Notification(int what);
}
```

### Breaking Changes Summary

| Feature | 1.0.0-rc.3 | 1.1.0-rc.1 |
|---------|------------|------------|
| Service Declaration | `[Singleton]` on class | `[Provide]` on Host member |
| Constructor Injection | `[InjectConstructor]` | Use provider method parameters |
| Modules Attribute | `Services = [...]` | Removed, only `Hosts = [...]` |
| Service Dependencies | Constructor parameters | `WaitFor` mechanism |
| Async Support | Not supported | `Task<T>` return types |

---

## Diagnostic Codes

The framework provides comprehensive compile-time error checking. For a complete list of diagnostic codes, please refer to [AnalyzerReleases.Shipped.md](./GodotSharpDI.SourceGenerator/AnalyzerReleases.Shipped.md).

**Diagnostic Code Categories**:

| Prefix | Category | Description |
|--------|----------|-------------|
| GDI_C | Class | Class-level errors |
| GDI_M | Member | Member-level errors |
| GDI_D | Dependency Graph | Dependency graph errors |
| GDI_E | Internal Error | Internal errors |
| GDI_U | User Behavior | User behavior warnings |

---

## License

MIT License

## Appendix: _Notification method explicitly definition requirement

All Host, User, and Scope types **must** explicitly define the `_Notification` method in C# script file attached to the node:

```csharp
public override partial void _Notification(int what);
```

### Why is this required?

- When you attach a C# script to a node in Godot, the engine creates a binding between the node and that specific script file
- Godot's script binding mechanism scans only the attached script file for virtual method overrides
- Source-generated files (*.g.cs) are compiled into the same class via `partial`, but Godot doesn't scan these files for lifecycle methods
- Therefore, lifecycle hooks like `_Notification` must be declared in the user's source file as a `partial` method

### IDE Support

IDE (Visual Studio, Rider) will provide automatic fixes:

1. If you forget to add this method, you'll see a **GDI_C080** error
2. Press `Ctrl+.` (VS) or `Alt+Enter` (Rider) on the error
3. Select "Add _Notification method declaration" to auto-generate the correct declaration

### Example:

```csharp
// Your source file: GameManager.cs (attached to node)
[Host]
public partial class GameManager : Node
{
    // Required: Godot needs to see this declaration
    public override partial void _Notification(int what);

    [Provide(ExposedTypes = [typeof(IGameState)])]
    public IGameState Self => this;
}

// Generated file: GameManager.DI.g.cs (not scanned by Godot)
partial class GameManager
{
    // Framework provides the implementation
    public override partial void _Notification(int what)
    {
        base._Notification(what);
        switch ((long)what)
        {
            case NotificationEnterTree:
                AttachToScope();
                break;
            case NotificationExitTree:
                UnattachToScope();
                break;
        }
    }
}
```

## Todo List

### 1. Documentation and Examples

- [ ] Complete bilingual (Chinese-English) documentation
- [ ] Add comprehensive sample projects
- [ ] Create video tutorials
- [ ] Enhance comment coverage in generated code

### 2. Testing

- [ ] Add runtime integration tests
- [ ] Add generator, analyzer, code fixer integration tests
- [ ] Add WaitFor mechanism tests

### 3. Features

- [x] Implement dependency WaitFor mechanism
- [x] Support asynchronous service providers
- [ ] Support asynchronous operations (using CallDeferred)
- [ ] Add service lifetime configuration options

### 4. Diagnostics

- [x] Diagnose generator internal errors (GDI_E)
- [ ] Add more detailed WaitFor cycle detection
- [ ] Improve error messages with code examples
