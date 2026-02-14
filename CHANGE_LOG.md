# v1.1.0-rc.1

> ## Why 1.1.0 Instead of 1.0.0?
>
> After releasing 1.0.0-rc.3, we identified a significant architectural limitation: the `[Singleton]` attribute and standalone service classes, while functional, created unnecessary complexity and limited flexibility in several ways:
>
> 1. **Tight Coupling**: Services were declared separately from where they were logically created
> 2. **Limited Flexibility**: No easy way to use Node resources or context when creating services
> 3. **No Async Support**: Constructor-only injection couldn't handle asynchronous initialization
> 4. **Complex Dependencies**: Managing service dependencies through constructors was inflexible
>
> The new **provider-based architecture** in 1.1.0 fundamentally addresses these issues, offering:
>
> - Services defined inline with Hosts for better cohesion
> - Direct access to Node resources and context during service creation
> - Native async/await support for service initialization
> - Flexible dependency ordering through the WaitFor mechanism
> - Simpler mental model (one less concept to learn)
>
> **Given the magnitude of these architectural improvements and breaking changes, we decided to increment to 1.1.0 rather than release 1.0.0 with known architectural limitations.** This allows us to move forward with a more robust and flexible foundation.
>
> ---

## 🎯 Major Architectural Changes

### ⚡ Provider-Based Architecture

**Removed in 1.1.0**: `[Singleton]` attribute and standalone service classes

**Replaced with**: `[Provide]` attribute on Host members (properties and methods)

**Migration Example**:

```csharp
// ❌ Old approach (1.0.0-rc.3)
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

// ✅ New approach (1.1.0-rc.1)
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

**Benefits**:
- Services are defined where they logically belong
- Full access to Host's context and resources
- More flexible service creation patterns
- Cleaner separation of concerns

---

### 🔄 WaitFor Mechanism

**New in 1.1.0**: Services can explicitly wait for dependencies before being provided.

**Usage**:

```csharp
[Host]
public partial class ServiceHost : Node, IDependenciesResolved
{
    [Inject] private IConfig _config;
    
    // Provided immediately
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger CreateLogger()
    {
        return new Logger();
    }
    
    // Waits for _config injection
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public IDatabase CreateDatabase()
    {
        return new DatabaseService(_config.ConnectionString);
    }
    
    // Waits for both CreateLogger and CreateDatabase
    [Provide(ExposedTypes = [typeof(IRepository)], 
             WaitFor = [nameof(CreateLogger), nameof(CreateDatabase)])]
    public IRepository CreateRepository()
    {
        // All dependencies guaranteed to be ready
        return new Repository();
    }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady) { }
    public override partial void _Notification(int what);
}
```

**Features**:
- Wait for `[Inject]` members to be injected
- Wait for other `[Provide]` members to complete
- Compile-time circular dependency detection
- Supports complex dependency chains
- Works with both sync and async providers

---

### ⚡ Asynchronous Service Support

**New in 1.1.0**: Providers can return `Task<T>` for async initialization.

**Usage**:

```csharp
[Host]
public partial class AsyncHost : Node
{
    [Provide(ExposedTypes = [typeof(IResourceLoader)])]
    public async Task<IResourceLoader> LoadResourcesAsync()
    {
        var loader = new ResourceLoader();
        await loader.LoadAssetsAsync();
        await loader.ValidateAsync();
        return loader;
    }
    
    [Provide(ExposedTypes = [typeof(INetworkService)])]
    public async Task<INetworkService> ConnectAsync()
    {
        var service = new NetworkService();
        await service.ConnectToServerAsync();
        return service;
    }
    
    // Can wait for async providers
    [Provide(ExposedTypes = [typeof(IGameSession)], 
             WaitFor = [nameof(LoadResourcesAsync), nameof(ConnectAsync)])]
    public IGameSession CreateSession()
    {
        // Resources and network are ready
        return new GameSession();
    }
    
    public override partial void _Notification(int what);
}
```

**Benefits**:
- Natural async/await syntax
- Better control over initialization order
- Proper error handling with try/catch
- Integrates seamlessly with WaitFor mechanism

---

## 🔨 Breaking Changes

### Removed Features

1. **`[Singleton]` Attribute**: Removed entirely
   - **Migration**: Use `[Provide]` on Host members instead

2. **`[InjectConstructor]` Attribute**: No longer needed
   - **Migration**: Control construction in provider methods

3. **`Services` Parameter in `[Modules]`**: Removed
   - **Migration**: Remove this parameter; only `Hosts = [...]` is needed

4. **Standalone Service Classes**: No longer a concept
   - **Migration**: Move service creation logic into Host providers

### Changed Behavior

1. **Service Registration**: Now happens through Host providers, not class declarations
2. **Service Construction**: Fully controlled by provider methods, not constructors
3. **Dependency Resolution**: Uses WaitFor mechanism instead of constructor parameters

---

## 📝 API Changes

### New Attributes

#### `[Provide(ExposedTypes = [...], WaitFor = [...])]`

Marks a property or method as a service provider.

**Parameters**:
- `ExposedTypes` (required): Array of types to expose
- `WaitFor` (optional): Array of member names to wait for

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

// Async provider with WaitFor
[Provide(ExposedTypes = [typeof(IRepository)], WaitFor = [nameof(_config)])]
public async Task<IRepository> InitializeRepositoryAsync()
{
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

**After (1.1.0-rc.1)**:
```csharp
[Modules(Hosts = [typeof(Host1), typeof(Host2)])]
```

---

## 💡 New Capabilities

### 1. Property-Based Service Provision

Simple services can be provided through properties:

```csharp
[Host]
public partial class ConfigHost : Node
{
    [Provide(ExposedTypes = [typeof(IConfig)])]
    public IConfig Config => new ConfigService();
    
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger Logger { get; } = new Logger();
    
    public override partial void _Notification(int what);
}
```

### 2. Method-Based Service Provision with Context

Complex services can access Host context:

```csharp
[Host]
public partial class GameHost : Node, IDependenciesResolved
{
    [Inject] private IConfig _config;
    
    private PlayerData _playerData;
    
    [Provide(ExposedTypes = [typeof(IPlayerStats)], WaitFor = [nameof(_config)])]
    public IPlayerStats CreatePlayerStats()
    {
        // Can access Host's state and injected dependencies
        var stats = new PlayerStatsService();
        stats.Initialize(_config.StartingHealth, _playerData);
        return stats;
    }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady) 
    {
        _playerData = LoadPlayerData();
    }
    
    public override partial void _Notification(int what);
}
```

### 3. Async Service Initialization

Services with async initialization requirements:

```csharp
[Host]
public partial class DataHost : Node
{
    [Provide(ExposedTypes = [typeof(IDatabase)])]
    public async Task<IDatabase> ConnectToDatabaseAsync()
    {
        var db = new DatabaseService();
        await db.ConnectAsync();
        await db.MigrateSchemaAsync();
        return db;
    }
    
    [Provide(ExposedTypes = [typeof(ICache)])]
    public async Task<ICache> InitializeCacheAsync()
    {
        var cache = new CacheService();
        await cache.WarmUpAsync();
        return cache;
    }
    
    public override partial void _Notification(int what);
}
```

### 4. Complex Dependency Chains

Explicit control over service initialization order:

```csharp
[Host]
public partial class ComplexHost : Node, IDependenciesResolved
{
    [Inject] private IConfig _config;
    
    // Layer 1: Core services
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger CreateLogger() => new Logger();
    
    // Layer 2: Depends on Layer 1
    [Provide(ExposedTypes = [typeof(IDatabase)], 
             WaitFor = [nameof(CreateLogger), nameof(_config)])]
    public async Task<IDatabase> ConnectDatabaseAsync()
    {
        var db = new DatabaseService(_config.ConnectionString);
        await db.ConnectAsync();
        return db;
    }
    
    // Layer 3: Depends on Layer 2
    [Provide(ExposedTypes = [typeof(IRepository)], 
             WaitFor = [nameof(ConnectDatabaseAsync)])]
    public IRepository CreateRepository()
    {
        return new Repository(/* database is ready */);
    }
    
    // Layer 4: Depends on multiple previous layers
    [Provide(ExposedTypes = [typeof(IDataService)], 
             WaitFor = [nameof(CreateLogger), nameof(CreateRepository)])]
    public IDataService CreateDataService()
    {
        return new DataService(/* logger and repository are ready */);
    }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady) { }
    public override partial void _Notification(int what);
}
```

---

## 🔍 Enhanced Diagnostics

### New Error Codes

| Code | Category | Description |
|------|----------|-------------|
| GDI_M100 | Provide | [Provide] member must have at least one exposed type |
| GDI_M101 | Provide | [Provide] exposed type not implemented by return type |
| GDI_M102 | Provide | [Provide] WaitFor target not found |
| GDI_M103 | Provide | [Provide] WaitFor target is invalid |
| GDI_D200 | WaitFor | Circular WaitFor dependency detected |
| GDI_D201 | WaitFor | WaitFor chain validation failed |

### Improved Error Messages

All diagnostics now include:
- Clear explanation of the problem
- Why it's problematic
- Suggested fix
- Code examples when applicable

**Example**:
```
Error GDI_D200: Circular WaitFor dependency detected
  
  CreateA (waits for) → CreateB
  CreateB (waits for) → CreateC
  CreateC (waits for) → CreateA
  
  Suggestion: Break the circular dependency by removing one of the WaitFor dependencies
  or refactor to use event-based communication instead.
```

---

## 🔧 Internal Improvements

### Code Generation

1. **Refactored Generation Pipeline**:
   - Separated dependency injection phase
   - Dedicated service provision phase
   - WaitFor dependency resolution phase

2. **Better Performance**:
   - Optimized service lookup
   - Reduced generated code size
   - Faster compilation times

3. **Cleaner Generated Code**:
   - More readable output
   - Better comments
   - Consistent formatting

### Testing

1. **New Test Suites**:
   - WaitFor circular dependency tests
   - WaitFor validation tests
   - Async provider tests

2. **Enhanced Coverage**:
   - Provider registration scenarios
   - Complex dependency chains
   - Error conditions

---

## 📖 Migration Guide

### Step 1: Convert Service Classes to Provider Methods

**Before**:
```csharp
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats
{
    [InjectConstructor]
    public PlayerStatsService(IConfig config)
    {
        Health = config.StartingHealth;
    }
    
    public int Health { get; set; }
    public int Mana { get; set; }
}
```

**After**:
```csharp
// In a Host class:
[Inject] private IConfig _config;

[Provide(ExposedTypes = [typeof(IPlayerStats)], WaitFor = [nameof(_config)])]
public IPlayerStats CreatePlayerStats()
{
    return new PlayerStatsService 
    { 
        Health = _config.StartingHealth,
        Mana = 50 
    };
}

// Service implementation (no attributes):
public class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; }
    public int Mana { get; set; }
}
```

### Step 2: Update Scope Definitions

**Before**:
```csharp
[Modules(
    Services = [typeof(PlayerStatsService), typeof(CombatService)],
    Hosts = [typeof(GameManager)]
)]
public partial class GameScope : Node, IScope { }
```

**After**:
```csharp
[Modules(Hosts = [typeof(GameManager)])]
public partial class GameScope : Node, IScope
{
    public override partial void _Notification(int what);
}
```

### Step 3: Handle Service Dependencies

**Before**:
```csharp
[Singleton(typeof(IRepository))]
public partial class Repository : IRepository
{
    [InjectConstructor]
    public Repository(IDatabase database, ILogger logger)
    {
        // Constructor injection
    }
}
```

**After**:
```csharp
[Host]
public partial class DataHost : Node
{
    [Provide(ExposedTypes = [typeof(IDatabase)])]
    public IDatabase CreateDatabase() => new DatabaseService();
    
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger CreateLogger() => new Logger();
    
    [Provide(ExposedTypes = [typeof(IRepository)], 
             WaitFor = [nameof(CreateDatabase), nameof(CreateLogger)])]
    public IRepository CreateRepository()
    {
        // Dependencies are guaranteed to be ready
        return new Repository();
    }
    
    public override partial void _Notification(int what);
}
```

### Step 4: Add Async Support Where Needed

If services need async initialization:

```csharp
[Host]
public partial class AsyncHost : Node
{
    [Provide(ExposedTypes = [typeof(INetworkService)])]
    public async Task<INetworkService> InitializeNetworkAsync()
    {
        var service = new NetworkService();
        await service.ConnectAsync();
        return service;
    }
    
    public override partial void _Notification(int what);
}
```

---

## 🎓 Best Practices

### 1. Group Related Providers

Organize providers logically within Hosts:

```csharp
[Host]
public partial class DataServicesHost : Node
{
    // All data-related services in one place
    [Provide(ExposedTypes = [typeof(IDatabase)])]
    public IDatabase CreateDatabase() => new DatabaseService();
    
    [Provide(ExposedTypes = [typeof(ICache)])]
    public ICache CreateCache() => new CacheService();
    
    [Provide(ExposedTypes = [typeof(IRepository)], 
             WaitFor = [nameof(CreateDatabase)])]
    public IRepository CreateRepository() => new Repository();
    
    public override partial void _Notification(int what);
}
```

### 2. Use WaitFor for Clear Dependencies

Make dependencies explicit:

```csharp
// ✅ Clear and explicit
[Provide(ExposedTypes = [typeof(IService)], 
         WaitFor = [nameof(_config), nameof(CreateLogger)])]
public IService CreateService()
{
    // Dependencies guaranteed ready
}

// ❌ Implicit and error-prone
[Provide(ExposedTypes = [typeof(IService)])]
public IService CreateService()
{
    // Hope that dependencies are ready?
}
```

### 3. Prefer Async for I/O Operations

```csharp
// ✅ Async for network/file operations
[Provide(ExposedTypes = [typeof(IDatabase)])]
public async Task<IDatabase> ConnectAsync()
{
    var db = new DatabaseService();
    await db.ConnectAsync();
    return db;
}

// ❌ Blocking in provider
[Provide(ExposedTypes = [typeof(IDatabase)])]
public IDatabase Connect()
{
    var db = new DatabaseService();
    db.ConnectAsync().Wait(); // Blocks!
    return db;
}
```

### 4. Keep Providers Simple

```csharp
// ✅ Simple and focused
[Provide(ExposedTypes = [typeof(IConfig)])]
public IConfig CreateConfig()
{
    return ConfigService.LoadFromFile("config.json");
}

// ❌ Too much logic in provider
[Provide(ExposedTypes = [typeof(IConfig)])]
public IConfig CreateConfig()
{
    var config = new ConfigService();
    config.Load();
    config.Validate();
    config.ApplyDefaults();
    config.MigrateOldFormat();
    config.Save();
    return config;
}
```

---

## 🔮 Looking Forward

This release establishes a solid foundation for future enhancements:

- **Future consideration**: Service lifetime scopes (Transient, Scoped, Singleton)
- **Future consideration**: Lazy service initialization
- **Future consideration**: Service decorators
- **Future consideration**: Multiple service instances of same type

We believe the provider-based architecture provides the flexibility needed for these future features while maintaining simplicity for common use cases.

---

## 📝 Summary

v1.1.0-rc.1 represents a significant architectural evolution:

**✅ New Architecture**:
- Provider-based service definition with `[Provide]`
- WaitFor mechanism for dependency ordering
- Native async/await support
- Simplified conceptual model

**⚠️ Breaking Changes**:
- `[Singleton]` attribute removed
- `[InjectConstructor]` attribute removed  
- `Services` parameter removed from `[Modules]`
- Standalone service classes no longer used

**🚀 Benefits**:
- Greater flexibility in service creation
- Better integration with Node resources
- Cleaner separation of concerns
- More powerful dependency management
- Future-proof architecture

---

**Migration effort**: Moderate. Most projects can be migrated in a few hours by following the migration guide.

**We recommend this release for all new projects and encourage existing projects to migrate when convenient.**

---


# v1.0.0-rc.3

> ## Major New Features
>
> ### ✨ Injection Failure Callback Mechanism
>
> **New in RC.3**: Individual inject members can now have failure callbacks for fine-grained error handling.
>
> **Usage**:
>
> ```csharp
> [User]
> public partial class PlayerUI : Control
> {
> [Inject(FailureCallback = true)]
> private IGameManager GameManager { get; set; }
> 
> partial void OnGameManagerInjectionFailed(string error)
> {
> GD.PrintErr($"GameManager injection failed: {error}");
> // Implement fallback logic
> }
> }
> ```
>
> **Benefits**:
> * Handle injection failures per-dependency instead of globally
> * Implement fallback logic for optional dependencies
> * Better error handling and user experience
>
> ---
>
> ### 🎯 Injection Ready Indicators
>
> **New in RC.3**: Every `[Inject]` member now generates a corresponding `IsXxxInjectionReady` boolean indicator.
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
>         // Check if dependency is ready at runtime
>         if (IsGameManagerInjectionReady)
>         {
>             GameManager.DoSomething();
>         }
>     }
> }
> ```
>
> **Benefits**:
> * Runtime checks for dependency availability
> * Safer code when dealing with optional dependencies
> * Better control flow based on injection status
>
> ---
>
> ### 🔄 Interface Renamed: IServicesReady → IDependenciesResolved
>
> **Breaking Change**: The interface has been renamed to better reflect its purpose, with an updated method signature.
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
> **Example Migration**:
> ```csharp
> // Old code (RC.2)
> [User]
> public partial class PlayerUI : Control, IServicesReady
> {
>     public void OnServicesReady()
>     {
>         Initialize();
>     }
> }
> 
> // New code (RC.3)
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
> **Rationale**:
> * Generic types cannot be instantiated without type arguments
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
> If you need to use generic types, create a concrete class that inherits from the generic type:
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
> ### 📊 Complete Dependency Chain Display
>
> **Enhanced in RC.3**: When dependency resolution fails, error messages now show the complete dependency chain.
>
> **Example Error Message**:
> ```
> Error: Failed to resolve dependency chain:
>   PlayerController (User)
>   → ICombatSystem (Service)
>   → IWeaponFactory (Service)
>   → IResourceLoader (missing)
> ```
>
> **Benefits**:
> * Quickly identify which service is missing
> * Understand the full context of dependency failures
> * Easier debugging of complex dependency graphs
>
> ---
>
> ### 🔍 Runtime Circular Dependency Detection
>
> **Optimized in RC.3**: Circular dependency detection now runs only in DEBUG builds for better performance.
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
> **Improved in RC.3**: All error messages now include:
> * What went wrong
> * Why it's problematic  
> * Suggested fix when applicable
> * Complete dependency chain context
>
> ---
>
> ## Code Generation Improvements
>
> ### 🏭 Service Factory Optimization
>
> **Changed in RC.3**: `ServiceFactories` is now a static collection for better memory efficiency.
>
> **Impact**:
> * Reduced memory footprint
> * Faster service factory lookups
> * Better performance in large dependency graphs
>
> ---
>
> ### 🏭 Service Creation or Provision Failures Also Trigger Callbacks
>
> **Changed in RC.3**: Service creation failures are now written into the service cache and trigger failure callbacks.
>
> **Impact**:
>
> - Better error propagation
> - Prevents waiting queues from hanging on services that have already definitively failed
> - Clearer error messages
>
> ---
>
> ### 📁 Enhanced File Naming
>
> **Improved in RC.3**: Generated files now use `Namespace+MetaName` format for better organization.
>
> **Example**:
> * Before: `PlayerController.DI.g.cs`
> * After: `MyGame.Player.PlayerController.DI.g.cs`
>
> **Benefits**:
> * Avoids naming conflicts in large projects
> * Better file organization in solution explorer
> * Easier to locate generated files
>
> ---
>
> ## Internal Error Handling & Robustness
>
> ### 🛡️ Comprehensive Exception Handling
>
> **New in RC.3**: The source generator, analyzers, and code fix providers now have robust exception handling to ensure stability.
>
> **Improvements**:
>
> #### Source Generator
> - **Layered Exception Handling**: Each stage of code generation has independent error handling
> - **Detailed Diagnostics**: New internal error diagnostics (GDI_E001-E101) provide clear error messages
> - **Graceful Degradation**: Failures in one class don't prevent generation for other classes
> - **User-Friendly Messages**: Error messages explain what failed and how to fix it
>
> **New Error Codes**:
> - `GDI_E001`: Generator initialization failed
> - `GDI_E010`: Class analysis failed
> - `GDI_E011`: Symbol cache unavailable
> - `GDI_E012`: Class validation failed
> - `GDI_E020`: Dependency graph build failed
> - `GDI_E021`: Graph build phase failed
> - `GDI_E030`: Service provider registration failed
> - `GDI_E040`: Node build failed
> - `GDI_E050`: Dependency graph validation failed
> - `GDI_E100`: Code generation failed
> - `GDI_E101`: Source output failed
>
> #### Analyzers
> - **Silent Failure**: Analyzer exceptions no longer crash compilation
> - **Protected Analysis**: Each syntax node analyzed independently with exception protection
> - **Cancellation Support**: Proper handling of `OperationCanceledException`
> - **Conservative Approach**: When in doubt, skip reporting rather than crash
>
> **Affected Analyzers**:
>
> - `GeneratedMemberAccessAnalyzer`: Detects manual access to generated members
> - `InjectionFailureCallbackAnalyzer`: Detects missing failure callback implementations
>
> #### Code Fix Providers
> - **Stable IDE Experience**: Code fix failures no longer crash the quick fix menu
> - **Fallback Mechanisms**: Simplified code generation when complex generation fails
> - **Safe Parsing**: String extraction and method generation protected against edge cases
> - **Return Original Document**: Failed fixes return the original document unchanged
>
> **Affected Providers**:
> - `NotificationMethodCodeFixProvider`: Adds missing `_Notification` method
> - `InjectionFailureCallbackCodeFixProvider`: Implements missing failure callbacks
>
> ---
>
> ## Migration Guide
>
> ### Required Changes
>
> 1. **Update Interface Implementation**:
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
>    2. **Check for Generic Types**:
>         * Remove generic type parameters from any Service, Host, User, or Scope classes
>    * Create concrete wrapper classes if needed
>    3. **Optional: Add Failure Callbacks**:
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
> - Better error diagnostics with complete dependency chains
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
> After further refining and polishing the overall project code, the next version will be the 1.0 release! 🎉


# v1.0.0-rc.2

> ## Critical Fixes
>
> ### ✅ Fixed `OnServicesReady()` Timing Issue
>
> **Problem in RC.1**: `OnServicesReady()` could be called before `_Ready()`, breaking the guarantee that all dependencies are available when nodes are ready.
>
> **Fixed in RC.2**:
>
> * `OnServicesReady()` now guaranteed to be called after `_Ready()`
> * Dependencies are fully resolved before callback execution
> * Proper integration with Godot's lifecycle
>
> ---
>
> ## Enhanced Type Validation
>
> ### New Diagnostics Added
>
> * Inject member cannot be regular Node (Error)
> * Inject member type should be interface (Warning)
> 
> * Singleton member type is invalid (Error)
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
> * Clear explanation of what went wrong
> * Why it's problematic
> * Suggested fix when applicable
> ```csharp
> // Before (RC.1):
> // Error: [Inject] member 'IGameState _state' has invalid type
> 
> // After (RC.2):
> // Warning GDI_M041: [Inject] member '_manager' has type 'GameManager', 
> // which is a [Host] type. While allowed, injecting Host types directly 
> // is not recommended - consider injecting an interface exposed by the 
> // Host instead
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
> It's almost production-ready and look forward to the stable 1.0 release! 🚀
