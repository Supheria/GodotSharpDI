# v1.3.1

## Bug Fixes

Fix error message in Scope:

> [GodotSharpDI] Host '{providerType}' failed to provide service

After fixed:

> [GodotSharpDI] Host 'HostaNodeA' failed to provide service

---

# v1.3.0

## ✨ New Features

### `[Inject]` and `[Provide]` Now Allow `[User]`-Typed Members (with Warning)

Previously, using a `[User]` type as the type of an `[Inject]` or `[Provide]` member was a compile-time **Error**. These diagnostics are now removed.

---

### `IScope.ProvideService` Now Includes `string providerType` Parameter

`ProvideService<TImpl>` now accepts a `providerType` string that records the name of the Host or User class providing the service. This name is included in all error and diagnostic messages emitted by the Scope, making it much easier to identify which node is responsible when a service provision fails.

**Before (1.2.x)**:
```csharp
void ProvideService<TImpl>(TImpl? instance) where TImpl : class;
```

**After (1.3.0)**:
```csharp
void ProvideService<TImpl>(TImpl? instance, string providerType) where TImpl : class;
```

**Impact**: Generated code is updated automatically. 

---

### `[Provide]` Now Supports Field Members

`[Provide]` can now be applied to field members in addition to properties and methods. This is especially useful when combined with Godot's `[Export]` attribute to expose child nodes as services.

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

---

### `[Provide]` Now Supports Node-Type Members

`[Provide]` can now be applied to members whose type is a Godot Node (without requiring `[Host]` on the Node type). This enables a Host to expose child nodes as services through their interfaces.

**Typical scene tree pattern:**
```
Root (Scope)
  |- Gui (GuiHost — [Host])
  |    |- AlertBox
  |- MapLoader ([User])
```

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

Previously, this required `AlertBox` to be a `[Host]` itself and registered in the Scope's `[Modules]`. With v1.3.0, `GuiHost` can host and expose `AlertBox` directly.

---

### `[Inject]` Now Supports Node-Type Members (Warning)

`[Inject]` can now be applied to members whose type is a Node class (i.e. the type itself is not an interface). A **Warning** (`GDI_M046`) is emitted to encourage injecting an interface instead of the concrete Node type, but the injection will proceed normally.

```csharp
[User]
public partial class MapLoader : Node
{
    // Allowed (with GDI_M046 warning — prefer injecting IAlertBox instead)
    [Inject]
    private AlertBox _alertBox;

    public override partial void _Notification(int what);
}
```

---

## 🔨 Bug Fixes

### Async `[Provide]` Member Without `ExposedTypes` Now Infers Correct Service Type

**Fixed**: When an async `[Provide]` member (method or property returning `Task<T>` / `ValueTask<T>`) did not specify `ExposedTypes`, the inferred service type was incorrectly set to `Task<T>` instead of the inner type `T`.

**Before (broken)**:
```csharp
[Host]
public partial class MyHost : Node
{
    // ❌ Service type was incorrectly inferred as Task<MyService>
    [Provide]
    public async Task<MyService> CreateServiceAsync() { ... }
}
```

**After (fixed)**:
```csharp
[Host]
public partial class MyHost : Node
{
    // ✅ Service type is now correctly inferred as MyService
    [Provide]
    public async Task<MyService> CreateServiceAsync() { ... }
}
```

---

## Deleted Diagnostics

| Rule ID  | Description                               |
| -------- | ----------------------------------------- |
| GDI_M045 | `[Inject]` member type is a regular Node  |
| GDI_M055 | `[provide]` member type is a regular Node |
| GDI_M043 | Cannot inject a [User] type               |
| GDI_M053 | [Provide] member cannot be a [User] type  |

---

# v1.2.2

## 🔨 Breaking Changes

### `IDependenciesResolved.OnDependenciesResolved` Parameter Removed

The `isAllDependenciesReady` parameter has been removed from `OnDependenciesResolved`. Use the generated `IsAllDependenciesReady` property (which carries `[MemberNotNull]` attributes) directly inside the implementation to verify null-safety.

**Before (1.2.1)**:
```csharp
public void OnDependenciesResolved(bool isAllDependenciesReady)
{
    if (isAllDependenciesReady)
    {
        // use injected members
    }
}
```

**After (1.2.2)**:
```csharp
public void OnDependenciesResolved()
{
    if (IsAllDependenciesReady)
    {
        // use injected members — null-safety guaranteed by [MemberNotNull]
    }
}
```

**Migration**: Remove the `bool isAllDependenciesReady` parameter from all `OnDependenciesResolved` implementations and replace references to the parameter with `IsAllDependenciesReady`.

---

### `OnXxxInjectionReady` Callback Now Receives a Typed Non-Null Parameter

The `ReadyCallback` partial method now receives a non-null reference to the successfully injected value, removing the need to call `IsXxxInjectionReady` inside the callback.

**Before (1.2.1)**:
```csharp
[Inject(ReadyCallback = true)] private INetworkService? _network;

partial void OnNetworkInjectionReady()
{
    // had to use IsNetworkInjectionReady / _network! to access the value
    GD.Print(_network!.ToString());
}
```

**After (1.2.2)**:
```csharp
[Inject(ReadyCallback = true)] private INetworkService? _network;

partial void OnNetworkInjectionReady(INetworkService network)
{
    // parameter is guaranteed non-null
    GD.Print(network.ToString());
}
```

**Migration**: Add a typed parameter matching the injected member's type to all `OnXxxInjectionReady` partial method implementations.

---

# v1.2.1

Fix missing auto-fix of GDI_C060 `MissingNotificationMethod`.
Reason: GDI_C080 had been changed to GDI_C060, while diagnostic code doesn't change in `NotificationMethodCodeFixProvider`.

---

# v1.2.0

## 🔨 Breaking Changes

### FailureCallback Method Signature Changed

The `FailureCallback` partial method no longer takes a `string error` parameter. The method is now **parameterless**.

**Before (1.1.1)**:
```csharp
partial void OnNetworkServiceInjectionFailed(string error)
{
    GD.PrintErr($"Network service unavailable: {error}");
    EnableOfflineMode();
}
```

**After (1.2.0)**:
```csharp
partial void OnNetworkServiceInjectionFailed()
{
    GD.PrintErr("Network service unavailable");
    EnableOfflineMode();
}
```

**Migration**: Remove the `string error` parameter from all `OnXxxInjectionFailed` method implementations.

---

### IScope Interface API Changed

The `ResolutionResult` struct has been removed. `IScope` now uses nullable types directly.

**Before (1.1.1)**:
```csharp
void ProvideService<TImpl>(ResolutionResult result);
void ResolveDependency<TExposed>(Action<ResolutionResult> onResult, string requestorType);
```

**After (1.2.0)**:
```csharp
void ProvideService<TImpl>(TImpl? instance);
void ResolveDependency<TExposed>(Action<TExposed?> onResult, string requestorType);
```

**Impact**: This only affects users implementing `IScope` directly (very rare). If you only use `[Host]`, `[User]`, and `[Modules]`, no migration is needed.

---

## 🎯 New Features

### 🔒 Cross-Host Deadlock Detection (GDI_D011)

**New in 1.2.0**: Compile-time detection of cross-host `WaitFor` circular dependencies.

When two or more `[Host]` nodes mutually wait for each other's provided services via `WaitFor`, this creates a deadlock at runtime. The framework now detects these cycles at **compile time** using Tarjan's SCC algorithm and reports `GDI_D011`.

**Example of a deadlock**:
```csharp
// ❌ HostA provides IServiceA but waits for IServiceB injection
[Host]
public partial class HostA : Node
{
    [Inject] private IServiceB? _serviceB;

    [Provide(ExposedTypes = [typeof(IServiceA)], WaitFor = [nameof(_serviceB)])]
    public IServiceA CreateA() => new ServiceA();

    public override partial void _Notification(int what);
}

// ❌ HostB provides IServiceB but waits for IServiceA injection
// → IServiceA → IServiceB → IServiceA: cross-host deadlock!
[Host]
public partial class HostB : Node
{
    [Inject] private IServiceA? _serviceA;

    [Provide(ExposedTypes = [typeof(IServiceB)], WaitFor = [nameof(_serviceA)])]
    public IServiceB CreateB() => new ServiceB();

    public override partial void _Notification(int what);
}
```

**Diagnostic**: `GDI_D011` (Error) – emitted on all hosts involved in the cycle, with the full cycle path in the message (e.g., `IServiceA -> IServiceB -> IServiceA`).

**Difference from GDI_D010**:
- `GDI_D010` – circular `WaitFor` within a **single Host**
- `GDI_D011` – circular `WaitFor` **across different Hosts**

**Solution**: Break the dependency cycle by removing one of the `WaitFor` references, or restructure which Host provides which service.

---

## 🛠️ Internal Improvements

### Centralized Generated String Constants

Added `GeneratedStrings.cs` – all runtime string literals used in generated code are now defined in one place, improving maintainability and preparing for future localization.

### TCS-Based WaitFor Synchronization

The `WaitFor` mechanism now uses `TaskCompletionSource<bool>` fields (`__xxx_tcs`) for dependency synchronization, replacing the previous event-based approach. This improves correctness when dependencies resolve across async boundaries.

### Generation Counter (`_diGeneration`)

A new `volatile int _diGeneration` field is generated for every DI node. It is incremented on `ExitTree` to invalidate in-flight async callbacks, preventing stale updates from a previous scene entry from being applied after a node re-enters the tree.

---

## 📊 Diagnostic Code Changes

| Code | Category | Change |
|------|----------|--------|
| `GDI_C060` | Class | **Renumbered** from `GDI_C080` – Missing `_Notification` declaration |
| `GDI_C061` | Class | **Renumbered** from `GDI_C081` – Incorrect `_Notification` signature |
| `GDI_D011` | Dependency Graph | **New** – Cross-host WaitFor deadlock detected |
| `GDI_E030` | Internal Error | **Removed** – Service provider registration failed |
| `GDI_M051–M056` | Member | **Renamed** – `SingletonMember*` → `ProvideMember*` |
| `GDI_M060,M062` | Member | **Renamed** – `SingletonMemberExposedType*` → `ProvideMemberExposedType*` |
| `GDI_M061` | Member | **New** – `[Provide]` member exposed type should be interface (Warning) |
| `GDI_M070` | Member | **Renamed** – `HostMissingSingletonMember` → `HostMissingProvideMember` |
| `GDI_M080` | Member | **Moved to resources** – WaitFor references non-existent field |
| `GDI_M081` | Member | **Moved to resources + severity** – WaitFor field not marked with [Inject]; upgraded Warning → **Error** |
| `GDI_M082` | Member | **Moved to resources** – WaitFor circular dependency (was hardcoded string) |

---

## ✅ Compatibility

- ⚠️ **Breaking**: `FailureCallback` partial method signature changed (remove `string error` param)
- ⚠️ **Breaking**: `IScope` interface changed (remove `ResolutionResult`, use nullable types)
- ✅ All other existing code continues to work without modification
- ✅ New features (`GDI_D011`) are purely additive diagnostics

---

# v1.1.1

## 🎯 New Features

### ⚡ Injection Callbacks

**New in 1.1.1**: Enhanced injection handling with `FailureCallback` and `ReadyCallback` mechanisms.

#### FailureCallback (Restored)

The `FailureCallback` feature from previous versions has been fully restored and enhanced:

```csharp
[User]
public partial class NetworkManager : Node
{
    [Inject(FailureCallback = true)]
    private INetworkService _networkService;
    
    partial void OnNetworkServiceInjectionFailed(string error)
    {
        GD.PrintErr($"Network service unavailable: {error}");
        EnableOfflineMode();  // Graceful degradation
    }
}
```

**Use Cases**:
- Critical services that need fallback strategies
- Network or external dependencies that may fail
- Optional services with alternative implementations

#### ReadyCallback (New)

New callback mechanism that triggers when injection succeeds:

```csharp
[User]
public partial class GameUI : Control
{
    [Inject(ReadyCallback = true)]
    private IGameState _gameState;
    
    partial void OnGameStateInjectionReady()
    {
        GD.Print("Game state ready");
        _gameState.Initialize();  // Safe to use immediately
    }
}
```

**Use Cases**:
- Services requiring immediate initialization after injection
- Coordinating initialization across multiple services
- Triggering UI updates when services become available

#### Combined Usage

Both callbacks can be used together:

```csharp
[Host]
public partial class GameManager : Node
{
    [Inject(FailureCallback = true, ReadyCallback = true)]
    private IDatabaseService _database;
    
    partial void OnDatabaseInjectionReady()
    {
        _database.MigrateSchema();
        LoadInitialData();
    }
    
    partial void OnDatabaseInjectionFailed(string error)
    {
        GD.PrintErr($"Database unavailable: {error}");
        UseFallbackDataSource();
    }
}
```

---

### 🔍 Smart Analyzers and Code Fixers

**New in 1.1.1**: Comprehensive IDE support for injection callbacks.

#### Analyzers

- **GDI_U004**: Detects missing `FailureCallback` implementations
- **GDI_U006**: Detects missing `ReadyCallback` implementations

The analyzers automatically detect when you mark an `[Inject]` member with callbacks but forget to implement the corresponding methods.

**Error Messages**:
```
GDI_U004: Member '_myService' is marked with [Inject(FailureCallback = true)] 
but the required callback method 'OnMyServiceInjectionFailed' is not implemented.
Please implement this partial method to handle injection failures.

GDI_U006: Member '_gameState' is marked with [Inject(ReadyCallback = true)] 
but the required callback method 'OnGameStateInjectionReady' is not implemented.
Please implement this partial method to handle successful injections.
```

#### Code Fixers

One-click code generation through IDE quick actions:

1. Analyzer detects missing callback implementation
2. Press `Ctrl+.` (VS) or `Alt+Enter` (Rider)
3. Select "Implement {MethodName} method"
4. Framework generates the correct method signature

**Generated Code**:

For `FailureCallback`:
```csharp
partial void OnMyServiceInjectionFailed(string error)
{
    GD.PushError(error);
}
```

For `ReadyCallback`:
```csharp
partial void OnMyServiceInjectionReady()
{
    GD.Print("Dependency injection ready");
}
```

---

## 🔨 Bug Fixes

### Host Injection Support in Analyzers

**Fixed**: `InjectionFailureCallbackAnalyzer` now correctly supports `[Inject]` members in `[Host]` classes.

**Before**:
```csharp
[Host]
public partial class GameManager : Node
{
    [Inject(FailureCallback = true)]  // ❌ Analyzer didn't check this
    private IConfig _config;
}
```

**After**:
```csharp
[Host]
public partial class GameManager : Node
{
    [Inject(FailureCallback = true)]  // ✅ Analyzer now checks this
    private IConfig _config;
    
    partial void OnConfigInjectionFailed(string error)
    {
        // Required implementation
    }
}
```

---

## 📝 API Changes

### InjectAttribute

**Enhanced**: Added `ReadyCallback` parameter

```csharp
public sealed class InjectAttribute : Attribute
{
    public bool FailureCallback { get; set; }  // Restored from previous versions
    public bool ReadyCallback { get; set; }     // New in 1.1.1
    // ... other members
}
```

### Generated Code

**New**: For `[Inject]` members with callbacks, the framework now generates:

1. **Callback Method Declarations**:
```csharp
partial void On{MemberName}InjectionFailed(string error);
partial void On{MemberName}InjectionReady();
```

2. **Callback Invocations** (in generated dependency resolution code):
```csharp
if (result.IsSuccess)
{
    try
    {
        _myService ??= (IMyService)result.Instance!;
        IsMyServiceInjectionReady = true;
        OnMyServiceInjectionReady();  // ← New
    }
    catch (Exception ex)
    {
        // error handling
    }
}
else
{
    OnMyServiceInjectionFailed(result.ErrorMessage ?? "Unknown error");  // ← Restored
}
```

---

## 📚 Documentation

**New**: Comprehensive documentation for injection callbacks:
- Added "Injection Callbacks" section to README.md
- Added "注入回调" section to README.zh-CN.md
- Detailed usage examples and best practices
- IDE integration guide

---

## ✅ Compatibility

- ✅ Fully backward compatible with 1.1.0
- ✅ All existing code continues to work
- ✅ New features are optional (callbacks default to `false`)
- ✅ No breaking changes

---

# v1.1.0

---

> ## Why 1.1.0 Instead of 1.0.0?
>
> After releasing 1.0.0-rc.3, the design where Scope creates and manages pure logic services led to DI container logic and service lifecycle management logic being intertwined. This resulted in overly complex generated code for Scope and caused confusion in Scope's semantics and scope of responsibilities, while also creating some limitations:
>
> 1. **Limited Flexibility**: Difficulty using Node resources or context when creating services
> 2. **No Async Support**: Constructor-only injection couldn't handle asynchronous initialization
> 
> The new **provider-based architecture** in 1.1.0 fundamentally addresses these issues, providing:
>
> - Services defined inline with Hosts for better cohesion
> - Direct access to Node resources and context during service creation
> - Native async/await support for service initialization
> - Flexible dependency ordering through the WaitFor mechanism
> 
> **Given the magnitude of these architectural improvements and breaking changes, we decided to increment the project version to 1.1.0 rather than release 1.0.0 with known architectural limitations.** 
> 
>---

## 🎯 Major Architectural Changes

### ⚡ Provider-Based Architecture

**Removed in 1.1.0**: `[Singleton]` attribute and standalone service classes

**Replaced with**: `[Provide]` attribute on Host members (properties and methods)

**Migration Example**:

```csharp
// ❌ Old Way (1.0.0-rc.3)
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;
}

[Modules(
    Services = [typeof(PlayerStatsService)],
    Hosts = [typeof(GameManager)]
)]
public partial class GameScope : Node, IScope { }

// ✅ New Way (1.1.0)
[Host]
public partial class GameManager : Node
{
    [Provide(ExposedTypes = [typeof(IPlayerStats)])]
    public IPlayerStats CreatePlayerStats()
    {
        return new PlayerStatsService { Health = 100, Mana = 50 };
    }
    
    public override partial void _Notification(int what);
}

[Modules(Hosts = [typeof(GameManager)])]
public partial class GameScope : Node, IScope
{
    public override partial void _Notification(int what);
}

// Service implementation (no attributes needed)
public class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; }
    public int Mana { get; set; }
}
```

**Advantages**:

- Services defined where they logically belong
- Full access to Host's context and resources
- More flexible service creation patterns
- Clearer separation of concerns

---

### 🔄 WaitFor Mechanism

**New in 1.1.0**: Services can explicitly wait for dependencies before being provided.

**Important Note**: `WaitFor` can **only wait for** `[Inject]` members, not `[Provide]` members.

**Usage**:

```csharp
[Host]
public partial class ServiceHost : Node, IDependenciesResolved
{
    [Inject] private IConfig? _config;
    [Inject] private ILogger? _logger;
    
    // Provided immediately (no dependencies)
    [Provide(ExposedTypes = [typeof(IMetrics)])]
    public IMetrics CreateMetrics()
    {
        return new MetricsService();
    }
    
    // Waits for _config injection
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public IDatabase CreateDatabase()
    {
        // WaitFor guarantees resolution was attempted, but need to check if successful
        if (!IsConfigInjectionReady || _config == null)
        {
            GD.PrintErr("Config not ready, using in-memory database");
            return new InMemoryDatabase();
        }
        return new DatabaseService(_config.ConnectionString);
    }
    
    // Waits for both _config and _logger injection
    [Provide(ExposedTypes = [typeof(IRepository)], 
             WaitFor = [nameof(_config), nameof(_logger)])]
    public IRepository CreateRepository()
    {
        // Check readiness of multiple dependencies
        if (!IsAllDependenciesReady)
        {
            GD.PrintErr("Some dependencies not ready");
            return new RepositoryWithDefaults();
        }
        // All dependencies ready
        return new Repository(_config!, _logger!);
    }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady) 
    {
        if (!isAllDependenciesReady)
        {
            GD.PrintErr("Some dependencies failed to inject");
        }
    }
    
    public override partial void _Notification(int what);
}
```

**Features**:
- Can **only** wait for `[Inject]` members to be injected
- Compile-time circular dependency detection
- Supports complex dependency chains
- Supports both sync and async providers
- Continues even if dependencies fail (must manually check `IsXxxInjectionReady`)

---

### ⚡ Async Service Support

**New in 1.1.0**: Providers can return `Task<T>` for async initialization.

**Usage**:

```csharp
[Host]
public partial class AsyncHost : Node, IDependenciesResolved
{
    [Inject] private IConfig? _config;
    
    // Async service provision, waiting for _config injection
    [Provide(ExposedTypes = [typeof(IResourceLoader)], WaitFor = [nameof(_config)])]
    public async Task<IResourceLoader> LoadResourcesAsync()
    {
        if (!IsConfigInjectionReady || _config == null)
        {
            return new ResourceLoader();  // Default loader
        }
        
        var loader = new ResourceLoader();
        await loader.LoadAssetsAsync(_config.AssetPath);
        await loader.ValidateAsync();
        return loader;
    }
    
    [Provide(ExposedTypes = [typeof(INetworkService)], WaitFor = [nameof(_config)])]
    public async Task<INetworkService> ConnectAsync()
    {
        if (!IsConfigInjectionReady || _config == null)
        {
            return new OfflineNetworkService();
        }
        
        var service = new NetworkService();
        await service.ConnectToServerAsync(_config.ServerUrl);
        return service;
    }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        if (!isAllDependenciesReady)
        {
            GD.PrintErr("Some dependencies not ready, certain services will use degraded versions");
        }
    }
    
    public override partial void _Notification(int what);
}
```

**Advantages**:
- Natural async/await syntax
- Better control over initialization order
- Proper error handling with try/catch
- Seamless integration with WaitFor mechanism

---

## 🔨 Breaking Changes

### Removed Features

1. **`[Singleton]` Attribute**: Completely removed
   - **Migration**: Use `[Provide]` on Host members

2. **`[InjectConstructor]` Attribute**: No longer needed
   - **Migration**: Control construction in provider methods

3. **`Services` Parameter in `[Modules]`**: Removed
   - **Migration**: Remove this parameter; only need `Hosts = [...]`

4. **Standalone Service Classes**: No longer a concept
   - **Migration**: Move service creation logic to Host providers

5. **Host + User Role Coexistence**: No longer allowed
   - **Migration**: Host can now directly use `[Inject]` without `[User]` attribute
   - **Rule**: Host, User, and Scope roles cannot coexist

### Behavior Changes

1. **Service Registration**: Now through Host providers, not class declarations
2. **Service Construction**: Fully controlled by provider methods, not constructors
3. **Dependency Resolution**: Uses WaitFor mechanism instead of constructor parameters
4. **Role Exclusivity**: A class can only have one role (Host, User, or Scope)
5. **Host Injection**: Host can directly inject dependencies without additional role marking

---

## 📝 API Changes

### New Attributes

#### `[Provide(ExposedTypes = [...], WaitFor = [...])]`

Marks a property or method as a service provider.

**Parameters**:
- `ExposedTypes` (required): Array of types to expose
- `WaitFor` (optional): Array of `[Inject]` member names to wait for before providing

**Can be applied to**:
- Properties (for simple service provision)
- Methods (for complex service creation)
- Async methods (for async initialization)

```csharp
// Property provider
[Provide(ExposedTypes = [typeof(IConfig)])]
public IConfig Config => new ConfigService();

// Method provider
[Provide(ExposedTypes = [typeof(IDatabase)])]
public IDatabase CreateDatabase() => new DatabaseService();

// Async provider with WaitFor (can only wait for Inject members)
[Inject] private IConfig? _config;

[Provide(ExposedTypes = [typeof(IRepository)], WaitFor = [nameof(_config)])]
public async Task<IRepository> InitializeRepositoryAsync()
{
    if (!IsConfigInjectionReady || _config == null)
    {
        return new Repository();
    }
    var repo = new Repository(_config);
    await repo.ConnectAsync();
    return repo;
}
```

### Modified Attributes

#### `[Modules(Hosts = [...])]`

Simplified to only accept Hosts.

**Before (1.0.0-rc.3)**:
```csharp
[Modules(
    Services = [typeof(Service1), typeof(Service2)],
    Hosts = [typeof(Host1), typeof(Host2)]
)]
```

**After (1.1.0)**:

```csharp
[Modules(Hosts = [typeof(Host1), typeof(Host2)])]
```

---

## 📖 Migration Guide (from 1.0.0-rc.3)

### Required Changes

#### 1. Remove Singleton Attribute

```csharp
// ❌ Old Code (1.0.0-rc.3)
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; }
}

[Modules(Services = [typeof(PlayerStatsService)])]
public partial class GameScope : Node, IScope { }

// ✅ New Code (1.1.0)
[Host]
public partial class PlayerHost : Node
{
    [Provide(ExposedTypes = [typeof(IPlayerStats)])]
    public IPlayerStats CreatePlayerStats()
    {
        return new PlayerStatsService();
    }
    
    public override partial void _Notification(int what);
}

// Service implementation doesn't need any attributes
public class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; }
}

[Modules(Hosts = [typeof(PlayerHost)])]
public partial class GameScope : Node, IScope
{
    public override partial void _Notification(int what);
}
```

#### 2. Remove InjectConstructor Attribute

```csharp
// ❌ Old Code
public class ServiceA
{
    [InjectConstructor]
    public ServiceA(IServiceB serviceB) { }
}

// ✅ New Code
[Host]
public partial class ServiceHost : Node
{
    [Inject] private IServiceB? _serviceB;
    
    [Provide(ExposedTypes = [typeof(IServiceA)], WaitFor = [nameof(_serviceB)])]
    public IServiceA CreateServiceA()
    {
        if (!IsServiceBInjectionReady || _serviceB == null)
        {
            return new ServiceA(new NullServiceB());
        }
        return new ServiceA(_serviceB);
    }
    
    public override partial void _Notification(int what);
}
```

#### 3. Update Modules Attribute

```csharp
// ❌ Old Code
[Modules(
    Services = [typeof(Service1), typeof(Service2)],
    Hosts = [typeof(Host1)]
)]

// ✅ New Code
[Modules(Hosts = [typeof(Host1)])]
```

#### 4. Use WaitFor for Service Dependencies

**Important**: WaitFor can only wait for `[Inject]` members.

```csharp
[Host]
public partial class ServiceHost : Node
{
    [Inject] private IConfig? _config;
    [Inject] private ILogger? _logger;
    
    // Metrics created immediately (no dependencies)
    [Provide(ExposedTypes = [typeof(IMetrics)])]
    public IMetrics CreateMetrics()
    {
        return new MetricsService();
    }
    
    // Database waits for _config injection
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public IDatabase CreateDatabase()
    {
        if (!IsConfigInjectionReady || _config == null)
        {
            return new InMemoryDatabase();
        }
        return new DatabaseService(_config.ConnectionString);
    }
    
    // Repository waits for both _config and _logger injection
    [Provide(ExposedTypes = [typeof(IRepository)], 
             WaitFor = [nameof(_config), nameof(_logger)])]
    public IRepository CreateRepository()
    {
        // Check if dependencies are ready
        if (!IsAllDependenciesReady)
        {
            return new RepositoryWithDefaults();
        }
        
        // Both dependencies ready, injected services can also be obtained through scope
        return new Repository(_config!, _logger!);
    }
    
    public override partial void _Notification(int what);
}
```

#### 5. Remove Host + User Combination

**1.1.0 Change**: Host, User, and Scope roles cannot coexist.

```csharp
// ❌ Old Code (may have been valid in 1.0.0-rc.3)
[Host, User]
public partial class GameManager : Node
{
    [Inject] private IConfig _config;
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
}

// ✅ New Code (1.1.0)
[Host]
public partial class GameManager : Node
{
    // Host can directly use Inject without User
    [Inject] private IConfig? _config;
    
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
    
    public override partial void _Notification(int what);
}
```

### Breaking Changes Summary

| Feature | 1.0.0-rc.3 | 1.1.0 |
|---------|------------|------------|
| Service Registration | `[Singleton]` attribute | `[Provide]` on Host members |
| Constructor Injection | `[InjectConstructor]` | WaitFor + provider method parameters |
| Async Initialization | Not supported | `Task<T>` providers |
| Dependency Ordering | Constructor parameters | `WaitFor` mechanism |
| Host + User | Can combine | Cannot combine (Host can directly Inject) |
| WaitFor Targets | N/A | Can only wait for `[Inject]` members |
| Modules Parameters | `Services` + `Hosts` | Only `Hosts` |
| Role Coexistence | Partially allowed | Fully exclusive |

---

## Known Issues and Limitations

### WaitFor Limitations

1. **Can only wait for Inject members**: Cannot wait for other Provide members
2. **Resolution complete ≠ success**: Must check `IsXxxInjectionReady`
3. **Circular waits**: Detected at compile time and error out

### Async Providers

1. **Cancellation**: No cancellation token support

---

## Next Steps

- Complete documentation and examples
- Gather community feedback
- Conduct performance testing
- Fix any discovered issues

---

# v1.0.0-rc.3

> ## Key Enhancements
>
> ### 🎯 Injection Failure Callbacks
>
> **New in RC.3**: You can now add failure callback handlers for each `[Inject]` member.
>
> **Usage**:
> ```csharp
> [User]
> public partial class PlayerController : Node
> {
>     [Inject(FailureCallback = true)]
>     private IOptionalService OptionalService { get; set; }
> 
>     // Generated callback method (implement in partial class)
>     partial void OnOptionalServiceInjectionFailed(string error)
>     {
>         GD.Print($"Optional service unavailable: {error}");
>         // Use fallback logic
>     }
> }
> ```
>
> **Benefits**:
> * Handle optional dependencies gracefully
> * Implement fallback logic for optional dependencies
> * Better error handling and user experience
>
> ---
>
> ### 🎯 Injection Ready Indicators
>
> **New in RC.3**: Each `[Inject]` member now generates a corresponding `IsXxxInjectionReady` boolean indicator.
>
> **Usage**:
> ```csharp
> [User]
> public partial class PlayerUI : Control
> {
>     [Inject]
>     private IGameManager GameManager { get; set; }
> 
>     public void Update()
>     {
>         // Check at runtime if dependency is ready
>         if (IsGameManagerInjectionReady)
>         {
>             GameManager.DoSomething();
>         }
>     }
> }
> ```
>
> **Benefits**:
> * Runtime checking of dependency availability
> * Safer code when handling optional dependencies
> * Better control flow based on injection status
>
> ---
>
> ### 🔄 Interface Rename: IServicesReady → IDependenciesResolved
>
> **Breaking Change**: The interface has been renamed to better reflect its purpose, with updated method signature.
>
> **Before (RC.2)**:
>
> ```csharp
> public interface IServicesReady
> {
>     void OnServicesReady();
> }
> ```
>
> **After (RC.3)**:
> ```csharp
> public interface IDependenciesResolved
> {
>     void OnDependenciesResolved(bool isAllDependenciesReady);
> }
> ```
>
> **Migration Required**:
> * Replace `IServicesReady` with `IDependenciesResolved`
> * Update method signature to accept `isAllDependenciesReady` parameter
> * Add logic to check the parameter and handle partial failures
>
> **Migration Example**:
> ```csharp
> // Old Code (RC.2)
> [User]
> public partial class PlayerUI : Control, IServicesReady
> {
>     public void OnServicesReady()
>     {
>         Initialize();
>     }
> }
> 
> // New Code (RC.3)
> [User]
> public partial class PlayerUI : Control, IDependenciesResolved
> {
>     public void OnDependenciesResolved(bool isAllDependenciesReady)
>     {
>         if (isAllDependenciesReady)
>         {
>             Initialize();
>         }
>         else
>         {
>             GD.PrintErr("Some dependencies failed to inject");
>         }
>     }
> }
> ```
>
> ---
>
> ## Enhanced Type Constraints
>
> ### 🚫 Generic Type Constraints
>
> **New in RC.3**: All DI roles (Service, Host, User, Scope) cannot be generic types.
>
> **Reasoning**:
> * Generic types cannot be instantiated without type parameters
> * Generic types cannot serve as stable service identifiers
> * Type safety and dependency graph construction require concrete types
>
> **Error Messages**:
>
> * Service: "Generic types cannot be used as service implementations"
> * Host: "Generic types cannot be marked as [Host]"
> * User: "Generic types cannot be marked as [User]"
> * Scope: "Generic types cannot be marked as [Scope]"
>
> **Workaround**:
> If you need generic types, create a concrete class that inherits from the generic type:
> ```csharp
> // ❌ Not allowed
> [Singleton(typeof(IRepository<Player>))]
> public partial class Repository<T> : IRepository<T> { }
> 
> // ✅ Correct approach
> public interface IPlayerRepository : IRepository<Player> { }
> 
> [Singleton(typeof(IPlayerRepository))]
> public partial class PlayerRepository : Repository<Player>, IPlayerRepository { }
> ```
>
> ---
>
> ## Improved Error Diagnostics
>
> ### 📊 Full Dependency Chain Display
>
> **RC.3 Enhancement**: When dependency resolution fails, error messages now show the complete dependency chain.
>
> **Error Message Example**:
> ```
> Error: Dependency chain resolution failed:
>   PlayerController (User)
>   → ICombatSystem (Service)
>   → IWeaponFactory (Service)
>   → IResourceLoader (missing)
> ```
>
> **Benefits**:
> * Quickly identify which service is missing
> * Understand the full context of dependency failure
> * Easier debugging of complex dependency graphs
>
> ---
>
> ### 🔍 Runtime Circular Dependency Detection
>
> **RC.3 Optimization**: Circular dependency detection now only runs in DEBUG builds for better performance.
>
> **Detection Scope**:
> * Only checks Service → Service constructor dependencies
> * Does not flag User `[Inject]` members (they resolve after construction)
> * Does not flag Host `[Singleton]` members
> * Does not flag Host+User self-injection patterns
>
> **Why This Matters**:
> Host+User self-injection is not a circular dependency because:
> 1. Host registration doesn't trigger injection
> 2. Service construction completes first
> 3. User injection happens afterward
> 4. No constructor cycle is formed
>
> ---
>
> ### 📝 Clearer Error Messages
>
> **RC.3 Improvement**: All error messages now include:
> * What went wrong
> * Why it's a problem
> * Suggested fix when applicable
> * Full dependency chain context
>
> ---
>
> ## Code Generation Improvements
>
> ### 🏭 Service Factory Optimization
>
> **RC.3 Change**: `ServiceFactories` is now a static collection for better memory efficiency.
>
> **Impact**:
> * Reduced memory footprint
> * Faster service factory lookups
> * Better performance in large dependency graphs
>
> ---
>
> ### 🏭 Service Creation or Provision Failure Also Triggers Callback
>
> **RC.3 Change**: Service creation failures now write to service cache and trigger failure callbacks.
>
> **Impact**:
>
> - Better error propagation
> - Prevents wait queues from hanging on services that have already explicitly failed
> - Clearer error messages
>
> ---
>
> ### 📁 Enhanced File Naming
>
> **RC.3 Improvement**: Generated files now use `Namespace+MetaName` format for better organization.
>
> **Example**:
> * Before: `PlayerController.DI.g.cs`
> * After: `MyGame.Player.PlayerController.DI.g.cs`
>
> **Benefits**:
> * Avoid naming conflicts in large projects
> * Better file organization in Solution Explorer
> * Easier to locate generated files
>
> ---
>
> ## Internal Error Handling & Robustness
>
> ### 🛡️ Comprehensive Exception Handling
>
> **New in RC.3**: Source generators, analyzers, and code fix providers now have robust exception handling to ensure stability.
>
> **Improvements**:
>
> #### Source Generators
> - **Layered exception handling**: Each stage of code generation has independent error handling
> - **Detailed diagnostics**: New internal error diagnostics (GDI_E001-E101) provide clear error messages
> - **Graceful degradation**: Failures in one class don't prevent generation for other classes
> - **User-friendly messages**: Error messages explain what failed and how to fix it
>
> **New Error Codes**:
> - `GDI_E001`: Generator initialization failed
> - `GDI_E010`: Class analysis failed
> - `GDI_E011`: Symbol cache unavailable
> - `GDI_E012`: Class validation failed
> - `GDI_E020`: Dependency graph building failed
> - `GDI_E021`: Graph build phase failed
> - `GDI_E030`: Service provider registration failed
> - `GDI_E040`: Node building failed
> - `GDI_E050`: Dependency graph validation failed
> - `GDI_E100`: Code generation failed
> - `GDI_E101`: Source output failed
>
> #### Analyzers
> - **Silent failures**: Analyzer exceptions no longer crash compilation
> - **Protected analysis**: Each syntax node analyzed independently with exception protection
> - **Cancellation support**: Properly handles `OperationCanceledException`
> - **Conservative approach**: When in doubt, skip reporting rather than crash
>
> **Affected Analyzers**:
>
> - `GeneratedMemberAccessAnalyzer`: Detects manual access to generated members
> - `InjectionFailureCallbackAnalyzer`: Detects missing failure callback implementations
>
> #### Code Fix Providers
> - **Stable IDE experience**: Code fix failures no longer crash quick fix menu
> - **Fallback mechanisms**: Simplified code generation when complex generation fails
> - **Safe parsing**: String extraction and method generation protected from edge cases
> - **Return original document**: Failed fixes return unchanged original document
>
> **Affected Providers**:
> - `NotificationMethodCodeFixProvider`: Adds missing `_Notification` methods
> - `InjectionFailureCallbackCodeFixProvider`: Implements missing failure callbacks
>
> ---
>
> ## Migration Guide
>
> ### Required Changes
>
> 1. **Update interface implementation**:
>   ```csharp
>    // Replace this
>   public partial class MyClass : Node, IServicesReady
>   {
>       public void OnServicesReady() { }
>   }
> 
>   // With this
>    public partial class MyClass : Node, IDependenciesResolved
>    {
>      public void OnDependenciesResolved(bool isAllDependenciesReady)
>        {
>          if (isAllDependenciesReady)
>            {
>               // Your initialization code
>            }
>        }
>    }
>   ```
>
>    2. **Check for generic types**:
>         * Remove generic type parameters from any Service, Host, User, or Scope classes
>    * Create concrete wrapper classes if needed
>    3. **Optional: Add failure callbacks**:
>
>
>    ```csharp
>    [Inject(FailureCallback = true)]
>    private IOptionalService Service { get; set; }
>    
>    partial void OnServiceInjectionFailed(string error)
>    {
>        // Handle failure
>    }
>    ```
>
>
> ---
>
> ## Summary
>
> v1.0.0-rc.3 brings significant improvements to error handling and diagnostics:
>
> ✅ **New Features**:
>    - Injection failure callbacks for fine-grained error handling
>       - Injection ready indicators for runtime checks
> - Better error diagnostics with full dependency chains
>
> ⚠️ **Breaking Changes**:
> - `IServicesReady` → `IDependenciesResolved` (migration required)
> - Generic types no longer allowed in DI roles
>
> 🚀 **Performance**:
> - Static service factory collection
> - Runtime circular dependency detection only in DEBUG
>
> ---
>
> After further refinement and polishing of the overall project code, the next release will be the 1.0 launch! 🎉


# v1.0.0-rc.2

> ## Key Fixes
>
> ### ✅ Fixed `OnServicesReady()` Timing Issue
>
> **Problem in RC.1**: `OnServicesReady()` could be called before `_Ready()`, breaking the guarantee that all dependencies are available when the node is ready.
>
> **Fixed in RC.2**:
>
> * `OnServicesReady()` now guaranteed to be called after `_Ready()`
> * Dependencies fully resolved before callback execution
> * Proper integration with Godot lifecycle
>
> ---
>
> ## Enhanced Type Validation
>
> ### New Diagnostics
>
> * Inject members cannot be regular Nodes (Error)
> * Inject member type should be interface (Warning)
> 
> * Singleton member type invalid (Error)
> * Singleton member is Host type (Warning)
> * Singleton member cannot be User type (Error)
> * Singleton member cannot be Scope/regular Node (Error)
> * Singleton member exposed type not implemented (Error)
> * Singleton member exposed type should be interface (Warning)
> 
> * Constructor parameter is Host type (Warning)
> * Constructor parameter cannot be User type (Error)
> * Constructor parameter cannot be Scope type (Error)
> * Constructor parameter cannot be regular Node (Error)
> * Constructor parameter should be interface (Warning)
> 
> * Inject member type not exposed by any service (Error)
> 
> ---
> 
> ## Improved Error Messages
> 
> All diagnostic messages now provide:
> * Clear description of what went wrong
> * Why it's a problem
> * Suggested fix when applicable
> ```csharp
> // Before (RC.1):
> // Error: [Inject] member 'IGameState _state' has invalid type
> 
> // After (RC.2):
> // Warning GDI_M041: [Inject] member '_manager' has type 'GameManager',
> // which is a [Host] type. While allowed, it's not recommended to inject Host types directly
> // - Consider injecting the interface exposed by the Host
> ```
> 
> ---
> 
> ## Resource Organization
> 
> ### Standardized Resource Naming
> 
> All diagnostic messages now use prefixed resource names:
> * `C_*` - Class-level diagnostics
> * `M_*` - Member-level diagnostics
> * `S_*` - Constructor-level diagnostics
> * `D_*` - Dependency graph diagnostics
> * `E_*` - Internal error diagnostics
> * `U_*` - User behavior diagnostics
> 
> ---
>
> Almost production-ready, looking forward to the stable 1.0 release! 🚀