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
