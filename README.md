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
  - [Host Using Inject](#host-using-inject)
- [Type Constraints](#type-constraints)
  - [Role Type Constraints](#role-type-constraints)
  - [Injectable Type Constraints](#injectable-type-constraints)
  - [Exposed Type Constraints](#exposed-type-constraints)
  - [Other Constraints](#other-constraints)
- [API Reference](#api-reference)
  - [Attributes](#attributes)
  - [Injection Callbacks](#injection-callbacks)
  - [Interfaces](#interfaces)
  - [Generated Code](#generated-code)
  - [Scene Tree Integration](#scene-tree-integration)
- [Best Practices](#best-practices)
  - [Scope Granularity Design](#scope-granularity-design)
  - [Service Disposal](#service-disposal)
  - [Avoiding Circular Dependencies](#avoiding-circular-dependencies)
  - [Interface-First Principle](#interface-first-principle)
  - [Host Injecting and Providing Services](#host-injecting-and-providing-services)
  - [Using Service Factories](#using-service-factories)
- [Diagnostic Codes](#diagnostic-codes)
- [License](#license)
- [Appendix: _Notification method explicitly definition requirement](#appendix-_notification-method-explicitly-definition-requirement)

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
<PackageReference Include="GodotSharpDI" Version="1.4.1" />
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
    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
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
[Modules(typeof(GameManager))]
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
    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
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

**New in 1.1.1**: Hosts can now use `[Inject]` members with full callback support (FailureCallback and ReadyCallback).

Hosts can also be service consumers by adding `[Inject]` members:

```csharp
[Host]
public partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // Consume services with callbacks (New in 1.1.1)
    [Inject(ReadyCallback = true, FailureCallback = true)]
    private IConfig? _config;
    
    // Provide services
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
    
    // Use WaitFor to ensure _config is injected before providing database
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public IDatabase CreateDatabase()
    {
        if (!IsConfigInjectionReady || _config == null)
        {
            return new InMemoryDatabase();
        }
        return new DatabaseService(_config.ConnectionString);
    }
    
    // Injection callbacks (New in 1.1.1)
    partial void OnConfigInjectionReady(IConfigService config)
    {
        GD.Print("Config loaded successfully");
        ApplyConfiguration();
    }
    
    partial void OnConfigInjectionFailed()
    {
        GD.PrintErr("Failed to load config");
        UseDefaultConfiguration();
    }
    
    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
        {
            // Initialize with injected dependencies
            InitializeGame();
        }
    }
    
    public override partial void _Notification(int what);
}
```

**Benefits**:
- Host can consume services from other Hosts in the same Scope
- Full callback support for better error handling
- Seamless integration with `WaitFor` mechanism
- Enables complex service dependency graphs

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
    
    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
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

#### Injection Callbacks

**New in 1.1.1**: Both `FailureCallback` and `ReadyCallback` for fine-grained injection control.

##### FailureCallback - Handle Injection Failures

```csharp
[User]
public partial class NetworkManager : Node, IDependenciesResolved
{
    [Inject(FailureCallback = true)]
    private INetworkService? _networkService;
    
    // Automatically called when injection fails
    partial void OnNetworkServiceInjectionFailed()
    {
        GD.PrintErr($"Network service unavailable");
        EnableOfflineMode();  // Fallback strategy
    }
    
    public void OnDependenciesResolved() { }
    public override partial void _Notification(int what);
}
```

##### ReadyCallback - Initialize After Successful Injection

```csharp
[User]
public partial class GameUI : Control, IDependenciesResolved
{
    [Inject(ReadyCallback = true)]
    private IGameState? _gameState;
    
    // Automatically called when injection succeeds
    partial void OnGameStateInjectionReady(IGameState gameState)
    {
        GD.Print("Game state service ready");
        _gameState!.Initialize();
        UpdateUI();
    }
    
    public void OnDependenciesResolved() { }
    public override partial void _Notification(int what);
}
```

##### Combined Usage

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
        GD.PrintErr($"Database connection failed");
        UseFallbackDataSource();
    }
    
    public void OnDependenciesResolved() { }
    public override partial void _Notification(int what);
}
```

**Key Features**:
- **FailureCallback**: Parameterless — called when injection fails (check `IsXxxInjectionReady` for status)
- **ReadyCallback**: Receives a non-null reference to the injected value — called immediately after successful injection
- **Optional Implementation**: Partial methods - implement only when needed
- **IDE Support**: Smart analyzers detect missing implementations and offer one-click fixes (GDI_U004, GDI_U006)

---

### Scope (Container)

#### Responsibilities

Scope is the DI container that manages service lifecycle and coordinates dependency injection.

#### Declaration

```csharp
[Modules(typeof(GameManager), typeof(ServiceHost))]
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

### Field (New in v1.3.0) or Property Providers


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

`[Provide]` can be applied directly to fields. This is particularly useful when combined with Godot's `[Export]` to expose child nodes as services, without requiring those nodes to be `[Host]` themselves.

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

This enables a scene tree like:

```
Root (Scope)
  |- GuiHost  [Host]
  |    |- AlertBox        ← plain Node, exposed via GuiHost
  |- MapLoader  [User]    ← injects IAlertBox
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

#### Core Concepts

When using `WaitFor`, understand the distinction between these two important concepts:

| Concept | Description | Corresponding State |
|---------|-------------|---------------------|
| **Dependency Resolution Completed** | Framework has attempted to resolve the dependency and invoked the callback | `OnDependencyResolved<T>()` is called |
| **Dependency Actually Ready** | Dependency successfully resolved and instance is available | `IsXxxInjectionReady = true` |

⚠️ **Important**: `WaitFor` only guarantees that dependency resolution has been **attempted**, not that it **succeeded**!

#### Basic Example

```csharp
[Host]
public partial class DependentHost : Node, IDependenciesResolved
{
    [Inject] private IConfig? _config;
    [Inject] private ILogger? _logger;
    
    // Framework automatically generates these properties (in generated code):
    // private bool IsConfigInjectionReady { get; set; } = false;
    // private bool IsLoggerInjectionReady { get; set; } = false;
    
    // Immediately provided service (no dependencies to wait for)
    [Provide(ExposedTypes = [typeof(IMetrics)])]
    public IMetrics CreateMetrics()
    {
        return new MetricsService();
    }
    
    // Waits for _config injection before providing
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public IDatabase CreateDatabase()
    {
        // ⚠️ WaitFor only guarantees resolution was attempted, must check if truly successful
        if (!IsConfigInjectionReady || _config == null)
        {
            GD.PrintErr("Config dependency not ready, using in-memory database");
            return new InMemoryDatabase();
        }
        
        // Safe: _config is guaranteed not null here
        return new DatabaseService(_config.ConnectionString);
    }
    
    // Waits for both _logger and _config injection before providing
    [Provide(ExposedTypes = [typeof(IRepository)], WaitFor = [nameof(_config), nameof(_logger)])]
    public IRepository CreateRepository()
    {
        // Check both dependencies' status
        if (!IsConfigInjectionReady || _config == null)
        {
            GD.PrintErr("Config dependency not ready, using default config");
            return new Repository(new DefaultConfig(), _logger);
        }
        
        if (!IsLoggerInjectionReady || _logger == null)
        {
            GD.PrintErr("Logger dependency not ready, using null logger");
            return new Repository(_config, new NullLogger());
        }
        
        // Safe: both dependencies are ready
        return new Repository(_config, _logger);
    }
    
    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
        {
            GD.Print("All dependencies successfully injected");
        }
        else
        {
            GD.PrintErr("Some dependencies failed to inject");
            
            // Check which specific dependency failed
            if (!IsConfigInjectionReady)
            {
                GD.PrintErr("Config injection failed");
            }
            if (!IsLoggerInjectionReady)
            {
                GD.PrintErr("Logger injection failed");
            }
        }
    }
    
    public override partial void _Notification(int what);
}
```

#### Complex Dependency Chain Example

```csharp
[Host]
public partial class ServiceHost : Node, IDependenciesResolved
{
    [Inject] private IConfig? _config;
    [Inject] private ILogger? _logger;
    [Inject] private IAuthService? _authService;
    
    // Generated readiness flags (available for use in code):
    // private bool IsConfigInjectionReady { get; set; } = false;
    // private bool IsLoggerInjectionReady { get; set; } = false;
    // private bool IsAuthServiceInjectionReady { get; set; } = false;
    // private bool IsAllDependenciesReady => 
    //     IsConfigInjectionReady && IsLoggerInjectionReady && IsAuthServiceInjectionReady;
    
    // Layer 1: Basic service (no dependencies)
    [Provide(ExposedTypes = [typeof(IMetrics)])]
    public IMetrics CreateMetrics()
    {
        // No injection dependencies, provided immediately
        return new MetricsService();
    }
    
    // Layer 2: Wait for single dependency
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public async Task<IDatabase> CreateDatabaseAsync()
    {
        // Even though WaitFor'd _config, still need to check if successful
        if (!IsConfigInjectionReady || _config == null)
        {
            GD.PrintErr("Config not ready, using in-memory database");
            return new InMemoryDatabase();
        }
        
        var connectionString = _config.DatabaseConnectionString;
        var db = new DatabaseService(connectionString);
        await db.InitializeAsync();
        return db;
    }
    
    // Layer 3: Wait for multiple dependencies
    [Provide(
        ExposedTypes = [typeof(IUserRepository)], 
        WaitFor = [nameof(_logger), nameof(_config)]
    )]
    public async Task<IUserRepository> CreateUserRepositoryAsync()
    {
        // All WaitFor dependencies have been attempted to resolve
        // Note: Still need to handle cases where dependencies may have failed
        
        var hasLogger = IsLoggerInjectionReady && _logger != null;
        if (!hasLogger)
        {
            GD.PrintErr("Logger not ready, will use null logger");
        }
        
        var hasConfig = IsConfigInjectionReady && _config != null;
        if (!hasConfig)
        {
            GD.PrintErr("Config not ready, using default config");
        }
        
        // Get other services through dependency injection (like IDatabase)
        // Or create degraded version directly
        return await UserRepository.CreateAsync(
            config: hasConfig ? _config : new DefaultConfig(),
            logger: hasLogger ? _logger : new NullLogger()
        );
    }
    
    // Layer 4: Wait for all dependencies
    [Provide(
        ExposedTypes = [typeof(ISecureRepository)],
        WaitFor = [nameof(_authService), nameof(_logger), nameof(_config)]
    )]
    public ISecureRepository CreateSecureRepository()
    {
        // Check readiness status of all dependencies
        if (!IsAllDependenciesReady)
        {
            // Some dependencies failed, log details
            if (!IsAuthServiceInjectionReady)
                GD.PrintErr("AuthService not ready");
            if (!IsLoggerInjectionReady)
                GD.PrintErr("Logger not ready");
            if (!IsConfigInjectionReady)
                GD.PrintErr("Config not ready");
                
            // Return degraded version or throw exception
            throw new InvalidOperationException("Cannot create SecureRepository: critical dependencies not ready");
        }
        
        // All dependencies ready, safe to create
        return new SecureRepository(_authService!, _logger!, _config!);
    }
    
    public void OnDependenciesResolved()
    {
        if (!IsAllDependenciesReady)
        {
            GD.PrintErr("Some dependencies failed, certain services may run in degraded mode");
        }
        else
        {
            GD.Print("All dependencies successfully injected");
        }
    }
    
    public override partial void _Notification(int what);
}
```

#### WaitFor Rules

1. **Wait Targets**:
   - ✅ Can only wait for `[Inject]` members (e.g., `nameof(_config)`)
   - ❌ Cannot wait for `[Provide]` members (compile-time error)
   - ❌ Cannot wait for non-existent members (compile-time error)

2. **Execution Order**:
   - WaitFor creates dependency topological sort
   - Services without WaitFor start providing immediately
   - Services with WaitFor provide only after dependencies resolve

3. **Failure Handling**:
   - Even if a dependency fails, WaitFor continues
   - Use `IsXxxInjectionReady` to check dependency status
   - Handle failure cases in `OnDependenciesResolved`

4. **Circular Detection**:
   - Circular WaitFor dependencies detected at compile time
   - Example: A WaitFor B, B WaitFor A (compile error)

5. **Async Support**:
   - WaitFor supports both sync and async providers
   - Async provider completion notifies subsequent dependencies

#### Best Practices

1. **Always Check Dependency Status**
   ```csharp
   [Provide(WaitFor = [nameof(_config)])]
   public IService CreateService()
   {
       if (!IsConfigInjectionReady || _config == null)
       {
           // Handle failure: use defaults, throw exception, or return degraded version
           return new ServiceWithDefaults();
       }
       return new Service(_config);
   }
   ```

2. **Implement IDependenciesResolved**
   ```csharp
   public void OnDependenciesResolved()
   {
       if (!IsAllDependenciesReady)
       {
           // Log or handle dependency failures
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

3. **Avoid Overly Long Dependency Chains**
   - Keep dependency layers within 2-3 levels
   - Longer chains increase failure risk and debugging difficulty

4. **Consider Using Nullable Types**
   ```csharp
   [Inject] private IConfig? _config;  // Use nullable type
   
   [Provide(WaitFor = [nameof(_config)])]
   public IService CreateService()
   {
       // Compiler will remind to check null
       return new Service(_config ?? new DefaultConfig());
   }
   ```

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

#### Standard Injection Flow (without WaitFor)

```
1. Node.EnterTree
   ↓
2. Reset injection state (cancel in-flight async ops, clear callback lists)
   ↓
3. Node.Ready
   ↓
4. Provide all [Provide] services (sync providers run immediately, async providers fire-and-forget)
   ↓
5. Resolve all [Inject] dependencies concurrently
   │
   ├─ Dependency A: Success → IsAInjectionReady = true
   ├─ Dependency B: Success → IsBInjectionReady = true
   └─ Dependency C: Failed → IsCInjectionReady = false
   ↓
6. OnDependenciesResolved() called after all dependencies resolved
```

#### WaitFor Injection Flow (New in 1.1.0)

```
1. Node.EnterTree
   ↓
2. Reset injection state (cancel in-flight async ops, clear callback lists)
   ↓
3. Node.Ready
   ↓
4. Phase 1: Provide services (independent of dependency injection)
   │
   ├─ Service X (no WaitFor): Provide immediately
   │
   ├─ Service Y (WaitFor = [A, X]): 
   │  ├─ Wait for A resolution
   │  ├─ Wait for X provision
   │  └─ All complete → Provide service Y
   │
   └─ Service Z (WaitFor = [B, Y]):
      ├─ Wait for B resolution (failed but continues)
      ├─ Wait for Y provision
      └─ All complete → Provide service Z
         (Must check IsBInjectionReady)
   ↓
5. Phase 2: Resolve all [Inject] dependencies concurrently
   │
   ├─ Dependency A: Success → IsAInjectionReady = true
   ├─ Dependency B: Failed → IsBInjectionReady = false
   └─ Dependency C: Success → IsCInjectionReady = true
   ↓
6. OnDependenciesResolved() called after all dependencies resolved
```

#### Key Concepts

1. **Resolution Complete vs Dependency Ready**
   - **Resolution Complete**: Framework attempted to get dependency and invoked callback (may succeed or fail)
   - **Dependency Ready**: `IsXxxInjectionReady = true` and instance is not null

2. **WaitFor Behavior**
   - WaitFor waits for dependency **resolution complete**, not **resolution success**
   - Even if dependency fails, WaitFor continues execution
   - Use `IsXxxInjectionReady` to check if dependency is truly available

3. **Concurrent vs Sequential**
   - Without WaitFor: All operations execute concurrently
   - With WaitFor: Creates dependency graph, executes in topological order

#### Example Timeline

```csharp
[Host]
public partial class ExampleHost : Node, IDependenciesResolved
{
    [Inject] private IConfig? _config;    // T1: Start resolving
    [Inject] private ILogger? _logger;    // T1: Start resolving (concurrent)
    
    [Provide(ExposedTypes = [typeof(IMetrics)])]  
    public IMetrics CreateMetrics()       // T1: Start providing immediately
    {
        return new Metrics();
    }
    
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public IDatabase CreateDatabase()     // T2: Wait for _config resolution complete
    {
        // T2 timing: _config resolution complete (success or failure)
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
    public IRepository CreateRepository() // T3: Wait for _logger and _config
    {
        // T3 timing: both _logger and _config resolution complete
        var hasLogger = IsLoggerInjectionReady && _logger != null;
        var hasConfig = IsConfigInjectionReady && _config != null;
        
        return new Repository(
            config: hasConfig ? _config : new DefaultConfig(),
            logger: hasLogger ? _logger : new NullLogger()
        );
    }
    
    public void OnDependenciesResolved()
    {
        // Called at T4: All Inject dependencies have been resolved
        // Some Provide services may still be executing asynchronously
    }
}

// Timeline:
// T1: CreateMetrics starts providing (ProvideServices in NotificationReady)
// T2: _config starts resolving, _logger starts resolving (ResolveDependencies)
// T3: _config resolution complete → CreateDatabase starts providing (WaitFor satisfied)
// T4: both _config and _logger resolved → OnDependenciesResolved is called
```

### Host Using Inject

**New in 1.1.0**: Hosts can directly use `[Inject]` to inject dependencies without needing to be marked as `[User]`.

⚠️ **Important**: Host, User, and Scope roles **cannot coexist** on the same class.

```csharp
[Host]
public partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // Host can directly inject dependencies (no [User] attribute needed)
    [Inject] private IConfig? _config;
    [Inject] private ISaveSystem? _saveSystem;
    
    // Host also provides services
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
    
    public GameState CurrentState { get; set; }
    
    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
        {
            // All dependencies ready, safe to initialize
            // Both IsConfigInjectionReady and IsSaveSystemInjectionReady are true
            LoadLastSave();
        }
        else
        {
            // Some dependencies failed, use degraded mode
            if (!IsConfigInjectionReady)
                GD.PrintErr("Config not ready, using default config");
            if (!IsSaveSystemInjectionReady)
                GD.PrintErr("SaveSystem not ready, cannot load save");
        }
    }
    
    private void LoadLastSave()
    {
        // Safe to use _config and _saveSystem here
        var config = _config!;
        var saveSystem = _saveSystem!;
        // ...
    }
    
    public override partial void _Notification(int what);
}
```

**Features**:
- Host can inject dependencies for use in provider methods
- Host can use WaitFor to wait for injection completion
- Host can implement IDependenciesResolved to receive notifications
- No additional `[User]` attribute needed

**Using Injected Dependencies in Providers**:

```csharp
[Host]
public partial class ServiceFactory : Node
{
    [Inject] private IConfig? _config;
    [Inject] private ILogger? _logger;
    
    // Wait for dependency injection before providing service
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public async Task<IDatabase> CreateDatabaseAsync()
    {
        if (!IsConfigInjectionReady || _config == null)
        {
            GD.PrintErr("Config not ready, using in-memory database");
            return new InMemoryDatabase();
        }
        
        // Safe to use injected config
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
        // Check multiple dependencies
        if (!IsAllDependenciesReady)
        {
            return new RepositoryWithDefaults();
        }
        
        // All dependencies ready
        return new Repository(_config!, _logger!);
    }
    
    public override partial void _Notification(int what);
}
```

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

#### `[Inject(FailureCallback = ..., ReadyCallback = ...)]`
Marks a field or property for dependency injection.

**Parameters**:
- `FailureCallback` (optional, default: `false`): Generate a callback method for injection failures
- `ReadyCallback` (optional, default: `false`): Generate a callback method for successful injections

**Basic Usage**:
```csharp
[Inject] private IService _service;
[Inject] private IConfig Config { get; set; }
```

**With Callbacks**:
```csharp
// Injection failure callback
[Inject(FailureCallback = true)]
private INetworkService _network;

partial void OnNetworkInjectionFailed()
{
    GD.PrintErr("Network service unavailable");
    EnableOfflineMode();
}

// Injection ready callback
[Inject(ReadyCallback = true)]
private IGameState _gameState;

partial void OnGameStateInjectionReady(IGameState gameState)
{
    GD.Print("Game state ready");
    _gameState.Initialize();
}

// Both callbacks
[Inject(FailureCallback = true, ReadyCallback = true)]
private IDatabaseService _database;

partial void OnDatabaseInjectionReady(IDatabaseService database)
{
    _database.MigrateSchema();
}

partial void OnDatabaseInjectionFailed()
{
    GD.PrintErr("Database connection failed");
    UseFallbackDataSource();
}
```

**See**: [Injection Callbacks](#injection-callbacks) for detailed documentation.

#### `[Modules(...)]`
Defines which Hosts belong to a Scope.

```csharp
// Constructor parameters (since v1.3.3)
[Modules(typeof(Host1), typeof(Host2))]
public partial class GameScope : Node, IScope { }
```

---

### Injection Callbacks

**New in 1.1.1**: GodotSharpDI provides callback mechanisms to handle injection success and failure events.

#### Overview

Injection callbacks allow you to:
- **Handle injection failures gracefully** with `FailureCallback`
- **Perform initialization after successful injection** with `ReadyCallback`
- **Implement fallback strategies** when critical services are unavailable
- **Control initialization order** based on injection readiness

#### FailureCallback

When an `[Inject]` member is marked with `FailureCallback = true`, the framework generates a partial method that you can implement to handle injection failures:

```csharp
[User]
public partial class PlayerController : Node
{
    [Inject(FailureCallback = true)]
    private INetworkService _networkService;
    
    // Framework generates this declaration:
    // partial void OnNetworkServiceInjectionFailed();
    
    // You implement it:
    partial void OnNetworkServiceInjectionFailed()
    {
        GD.PrintErr("Network service failed to inject");
        
        // Implement fallback strategy
        EnableOfflineMode();
        ShowOfflineNotification();
    }
}
```

**Generated Method Signature**:
```csharp
partial void On{MemberName}InjectionFailed()
```

**Use Cases**:
- Critical services that need graceful degradation
- Network or external dependencies that may fail
- Optional services with fallback implementations

#### ReadyCallback

When an `[Inject]` member is marked with `ReadyCallback = true`, the framework generates a partial method called when the injection succeeds. The method receives a **non-null** reference to the injected value as a parameter, so you can use it directly without any null checks:

```csharp
[User]
public partial class GameUI : Control
{
    [Inject(ReadyCallback = true)]
    private IGameState? _gameState;
    
    // Framework generates this declaration:
    // partial void OnGameStateInjectionReady(IGameState gameState);
    
    // You implement it:
    partial void OnGameStateInjectionReady(IGameState gameState)
    {
        GD.Print("Game state service ready");
        
        // Parameter is guaranteed non-null — no need to check IsGameStateInjectionReady
        gameState.Initialize();
        UpdateUI();
    }
}
```

**Generated Method Signature**:
```csharp
partial void On{MemberName}InjectionReady(TService value)
```

> **Note:** The parameter type matches the injected member's declared type (without `?`), so the compiler enforces non-nullability inside the callback automatically.

**Use Cases**:
- Services requiring immediate initialization after injection
- Coordinating initialization across multiple services
- Triggering UI updates when services become available

#### Combined Usage

Both callbacks can be used together for comprehensive error handling:

```csharp
[Host]
public partial class GameManager : Node
{
    [Inject(FailureCallback = true, ReadyCallback = true)]
    private IDatabaseService _database;
    
    partial void OnDatabaseInjectionReady(IDatabaseService database)
    {
        // Success path
        _database.MigrateSchema();
        LoadInitialData();
    }
    
    partial void OnDatabaseInjectionFailed()
    {
        // Failure path
        GD.PrintErr("Database unavailable");
        UseFallbackDataSource();
    }
}
```

#### Callback Execution Order

For a single `[Inject]` member, the callback execution follows this order:

1. **Injection attempted** by the framework
2. **On Success**: `OnXxxInjectionReady(TService value)` called (if `ReadyCallback = true`)
3. **On Failure**: `OnXxxInjectionFailed()` called (if `FailureCallback = true`)
4. **Finally**: `IDependenciesResolved.OnDependenciesResolved()` called after all injections complete

#### IDE Support

The framework provides compile-time analysis and automatic code generation:

**Analyzer**:
- Detects when `FailureCallback = true` but callback method not implemented
- Detects when `ReadyCallback = true` but callback method not implemented
- Shows clear error messages with the exact method signature needed

**Code Fix** (Quick Actions):
- Press `Ctrl+.` (VS) or `Alt+Enter` (Rider) on the error
- Select "Implement {MethodName} method"
- Framework automatically generates the correct method signature

**Example**:
```csharp
// 1. You write:
[Inject(ReadyCallback = true)]
private IService _service;

// 2. Analyzer shows error:
// "Member '_service' is marked with [Inject(ReadyCallback = true)] 
//  but the required callback method 'OnServiceInjectionReady' is not implemented"

// 3. You press Ctrl+. and select "Implement OnServiceInjectionReady method"

// 4. Framework generates:
partial void OnServiceInjectionReady(IService service)
{
    GD.Print("Dependency injection ready");
}

// 5. You customize the implementation
```

#### Best Practices

**1. Use FailureCallback for Critical Services**:
```csharp
[Inject(FailureCallback = true)]
private INetworkService _network;

partial void OnNetworkInjectionFailed()
{
    // Always provide fallback for critical services
    EnableOfflineMode();
}
```

**2. Use ReadyCallback for Initialization**:
```csharp
[Inject(ReadyCallback = true)]
private IConfigService? _config;

partial void OnConfigInjectionReady(IConfigService config)
{
    // Initialize immediately after injection — config is non-null here
    ApplyConfiguration();
}
```

**3. Combine Both for Important Services**:
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

**4. Coordinate Multiple Services**:
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
            GD.Print("All services ready, starting game");
            StartGame();
        }
    }
}
```

---

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
    void OnDependenciesResolved();
}
```

#### Parameter Description

- Use **`IsAllDependenciesReady`** (generated property with `[MemberNotNull]`) to check whether all injections succeeded:
  - `true`: All `[Inject]` members were successfully injected
  - `false`: At least one `[Inject]` member failed to inject

#### Generated Helper Properties

The framework automatically generates readiness flags for each `[Inject]` member. These properties are in the generated `*.DI.g.cs` files:

```csharp
// User code
[Host]
public partial class MyHost : Node, IDependenciesResolved
{
    [Inject] private IConfig? _config;
    [Inject] private ILogger? _logger;
    
    // ... other code
}

// Generated code (in MyHost.DI.Inject.g.cs)
partial class MyHost
{
    // Readiness flag generated for each Inject member
    [MemberNotNullWhen(true, nameof(_config))]
    private bool IsConfigInjectionReady { get; set; } = false;
    
    [MemberNotNullWhen(true, nameof(_logger))]
    private bool IsLoggerInjectionReady { get; set; } = false;
    
    // Combined readiness flag
    [MemberNotNullWhen(true, nameof(_config))]
    [MemberNotNullWhen(true, nameof(_logger))]
    private bool IsAllDependenciesReady => 
        IsConfigInjectionReady && IsLoggerInjectionReady;
    
    // Unresolved dependency tracking
    private readonly HashSet<Type> __unresolvedDependencies = new()
    {
        typeof(IConfig),
        typeof(ILogger),
    };
    
    // Dependency resolution callback
    private void OnDependencyResolved<T>()
    {
        __unresolvedDependencies.Remove(typeof(T));
        if (__unresolvedDependencies.Count == 0)
        {
            ((IDependenciesResolved)this).OnDependenciesResolved();
        }
    }
}

// Generated code (in MyHost.DI.Lifecycle.g.cs)
partial class MyHost
{
    private IScope? __parentScope;

    private IScope? GetParentScope()
    {
        if (__parentScope is not null) return __parentScope;
        var parent = GetParent();
        while (parent is not null)
        {
            if (parent is IScope scope) { __parentScope = scope; return __parentScope; }
            parent = parent.GetParent();
        }
        return null;
    }

    public override partial void _Notification(int what)
    {
        base._Notification(what);
        switch ((long)what)
        {
            case NotificationEnterTree:
                __parentScope = null;
                ResetInjectionState();
                break;
            case NotificationReady:
                ProvideServices();
                ResolveDependencies();
                break;
            case NotificationExitTree:
                __parentScope = null;
                ResetInjectionState();
                break;
        }
    }
}
```

#### Usage Examples

##### Basic Usage

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
            GD.Print("All dependencies ready, game can start");
            StartGame();
        }
        else
        {
            GD.PrintErr("Dependency injection failed, cannot start game");
            ShowErrorScreen();
        }
    }
    
    public override partial void _Notification(int what);
}
```

##### Fine-Grained Status Checking

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
            // All dependencies succeeded, enable full features
            EnableAllFeatures();
        }
        else
        {
            // Some dependencies failed, enable degraded mode
            EnableDegradedMode();
            
            // Check which specific dependencies are available
            if (IsStatsInjectionReady)
            {
                UpdateStatsDisplay(_stats!);  // ! operator is safe because IsStatsInjectionReady = true
            }
            else
            {
                GD.PrintErr("Stats service unavailable");
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
        // All features available
        UpdateStatsDisplay(_stats!);
        UpdateInventoryDisplay(_inventory!);
        ShowAchievements(_achievements!);
    }
    
    private void EnableDegradedMode()
    {
        // Some features running in degraded mode
        GD.Print("UI running in degraded mode");
    }
    
    public override partial void _Notification(int what);
}
```

##### Using with WaitFor

```csharp
[Host]
public partial class DataManager : Node, IDependenciesResolved
{
    [Inject] private IConfig? _config;
    [Inject] private ILogger? _logger;
    
    // Generated properties available for checking:
    // private bool IsConfigInjectionReady { get; set; }
    // private bool IsLoggerInjectionReady { get; set; }
    
    // Wait for _config injection before providing database service
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public async Task<IDatabase> CreateDatabaseAsync()
    {
        // WaitFor guarantees _config resolution was attempted, but need to check if successful
        if (!IsConfigInjectionReady || _config == null)
        {
            // Config injection failed, use in-memory database
            GD.PrintErr("Config not ready, using in-memory database");
            return new InMemoryDatabase();
        }
        
        // Config successfully injected, use configured database
        var db = new DatabaseService(_config.ConnectionString);
        await db.InitializeAsync();
        return db;
    }
    
    public void OnDependenciesResolved()
    {
        if (!IsAllDependenciesReady)
        {
            GD.PrintErr("Some dependencies failed to inject:");
            
            if (!IsConfigInjectionReady)
                GD.PrintErr("  - Config injection failed, will use default config");
                
            if (!IsLoggerInjectionReady)
                GD.PrintErr("  - Logger injection failed, logging will be disabled");
        }
        else
        {
            GD.Print("All dependencies successfully injected");
        }
    }
    
    public override partial void _Notification(int what);
}
```

#### Invocation Timing

`OnDependenciesResolved` is called at the following timing:

1. **All `[Inject]` dependencies have been attempted to resolve** (success or failure)
2. **After the node's `_Notification(NotificationReady)`**
3. **Before async `[Provide]` services have necessarily completed initialization** (async providers may still be running when this is called)

#### Best Practices

1. **Always check `IsAllDependenciesReady`**
   ```csharp
   public void OnDependenciesResolved()
   {
       if (IsAllDependenciesReady)
       {
           // Normal flow
       }
       else
       {
           // Degradation or error handling
       }
   }
   ```

2. **Use generated `IsXxxInjectionReady` for fine-grained checking**
   ```csharp
   if (!IsConfigInjectionReady)
   {
       GD.PrintErr("Config injection failed");
       // Use default config
   }
   ```

3. **Combine with null checking for increased safety**
   ```csharp
   if (IsStatsInjectionReady && _stats != null)
   {
       // Safe to use _stats
       DisplayStats(_stats);
   }
   ```

4. **Log dependency status for debugging**
   ```csharp
   public void OnDependenciesResolved()
   {
       GD.Print($"Dependencies ready: {IsAllDependenciesReady}");
       GD.Print($"  Config: {IsConfigInjectionReady}");
       GD.Print($"  Logger: {IsLoggerInjectionReady}");
   }
   ```

#### Notes

⚠️ **Important**:
- `IsXxxInjectionReady` properties and `IsAllDependenciesReady` property are generated when there are `[Inject]` members, regardless of whether `IDependenciesResolved` interface is implemented
- These properties are private and can only be used inside the class
- **`[MemberNotNullWhen(true, ...)]` attribute effect**: When `IsXxxInjectionReady` is `true`, the compiler ensures the corresponding nullable member is not `null`. This means after checking `IsXxxInjectionReady`, you can safely use the null-forgiving operator (`!`) or directly access the member without additional null checks
- `OnDependenciesResolved` is called even if dependency injection fails (parameter will be `false`)

**Benefits of Using `IsXxxInjectionReady`**:

```csharp
[Host]
public partial class MyHost : Node
{
    [Inject] private IConfig? _config;
    
    // Generated:
    // [MemberNotNullWhen(true, nameof(_config))]
    // private bool IsConfigInjectionReady { get; set; }
    
    [Provide(ExposedTypes = [typeof(IService)], WaitFor = [nameof(_config)])]
    public IService CreateService()
    {
        if (IsConfigInjectionReady)
        {
            // ✅ Compiler knows _config is not null
            // Can use directly without null check
            return new Service(_config.ConnectionString);
            
            // Or use null-forgiving operator
            return new Service(_config!.ConnectionString);
        }
        
        // Handle case where _config might be null
        return new ServiceWithDefaults();
    }
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
// Generated _Notification for Host/User types:
public override partial void _Notification(int what)
{
    base._Notification(what);
    switch ((long)what)
    {
        case NotificationEnterTree:
            __parentScope = null;
            ResetInjectionState();
            break;
        case NotificationReady:
            // Host: ProvideServices() + ResolveDependencies()
            // User: ResolveDependencies()
            break;
        case NotificationExitTree:
            __parentScope = null;
            ResetInjectionState();
            break;
    }
}
```

**Lifecycle Phases**:
- **EnterTree**: Reset injection state — cancel in-flight async providers, clear callback lists, reset ready flags
- **Ready**: Provide services and resolve dependencies (Host provides first, then both resolve)
- **ExitTree**: Reset injection state again to abort any pending operations

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

**Compile-time Detection**: The framework detects two types of WaitFor cycles:

- **GDI_D010** – circular WaitFor within the same Host
- **GDI_D011** – circular WaitFor across different Hosts *(New in 1.2.0)*

**Cross-Host Deadlock (GDI_D011)**:

```csharp
// ❌ HostA provides IServiceA but waits for IServiceB injection
[Host]
public partial class HostA : Node
{
    [Inject] private IServiceB? _serviceB;
    
    [Provide(ExposedTypes = [typeof(IServiceA)], WaitFor = [nameof(_serviceB)])]
    public IServiceA CreateA() => new ServiceA(_serviceB);
    
    public override partial void _Notification(int what);
}

// ❌ HostB provides IServiceB but waits for IServiceA → cross-host deadlock → GDI_D011
[Host]
public partial class HostB : Node
{
    [Inject] private IServiceA? _serviceA;
    
    [Provide(ExposedTypes = [typeof(IServiceB)], WaitFor = [nameof(_serviceA)])]
    public IServiceB CreateB() => new ServiceB(_serviceA);
    
    public override partial void _Notification(int what);
}
```

**Solution**: Refactor so that only one direction waits:

```csharp
// ✅ Correct approach - only one direction waits
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

### Host Injecting and Providing Services

Hosts can both inject dependencies and provide services without needing the `[User]` attribute:

```csharp
[Host]
public partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // Host directly injects dependencies
    [Inject] private IConfig? _config;
    [Inject] private ISaveSystem? _saveSystem;
    
    // Host provides services
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
        // Both IsConfigInjectionReady and IsSaveSystemInjectionReady are true
        // Safe to use _config and _saveSystem
        var config = _config!;
        var saveSystem = _saveSystem!;
        // ...
    }
    
    public override partial void _Notification(int what);
}
```

**Role Exclusivity Rules**:

| Role | Can Be Combined | Cannot Be Combined |
|------|----------------|-------------------|
| **Host** | Can be used alone | Cannot coexist with User or Scope |
| **User** | Can be used alone | Cannot coexist with Host or Scope |
| **Scope** | Must be used alone | Cannot coexist with any other role |

**Host Capabilities**:
- ✅ Use `[Provide]` to provide services
- ✅ Use `[Inject]` to inject dependencies
- ✅ Use `WaitFor` to wait for dependencies
- ✅ Implement `IDependenciesResolved`
- ❌ Cannot be marked as `[User]` simultaneously
- ❌ Cannot be marked as `[Scope]` simultaneously

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

## Diagnostic Codes

The framework provides comprehensive compile-time error checking.

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

1. If you forget to add this method, you'll see a **GDI_C060** error
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

// Generated file: GameManager.DI.Lifecycle.g.cs (not scanned by Godot)
partial class GameManager
{
    // Framework provides the implementation
    public override partial void _Notification(int what)
    {
        base._Notification(what);
        switch ((long)what)
        {
            case NotificationEnterTree:
                __parentScope = null;
                ResetInjectionState();
                break;
            case NotificationReady:
                ProvideServices();
                ResolveDependencies();
                break;
            case NotificationExitTree:
                __parentScope = null;
                ResetInjectionState();
                break;
        }
    }
}
```
