

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
>   {
>   GD.PrintErr($"GameManager injection failed: {error}");
> // Implement fallback logic
> }
> }
>```
> 
> **Benefits**:
> * Handle injection failures per-dependency instead of globally
> * Implement fallback logic for optional dependencies
>* Better error handling and user experience
> 
>---
> 
>### 🎯 Injection Ready Indicators
> 
>**New in RC.3**: Every `[Inject]` member now generates a corresponding `IsXxxInjectionReady` boolean indicator.
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
>```
> 
> **Benefits**:
> * Runtime checks for dependency availability
> * Safer code when dealing with optional dependencies
>* Better control flow based on injection status
> 
>---
> 
>### 🔄 Interface Renamed: IServicesReady → IDependenciesResolved
> 
>**Breaking Change**: The interface has been renamed to better reflect its purpose, with an updated method signature.
> 
>**Before (RC.2)**:
> 
> ```csharp
> public interface IServicesReady
> {
>     void OnServicesReady();
> }
>```
> 
> **After (RC.3)**:
> ```csharp
> public interface IDependenciesResolved
> {
>     void OnDependenciesResolved(bool isAllDependenciesReady);
> }
>```
> 
> **Migration Required**:
> * Replace `IServicesReady` with `IDependenciesResolved`
> * Update method signature to accept `isAllDependenciesReady` parameter
>* Add logic to check the parameter and handle partial failures
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
>```
> 
>---
> 
>## Enhanced Type Constraints
> 
>### 🚫 Generic Type Constraints
> 
>**New in RC.3**: All DI roles (Service, Host, User, Scope) cannot be generic types.
> 
> **Rationale**:
> * Generic types cannot be instantiated without type arguments
> * Generic types cannot serve as stable service identifiers
>* Type safety and dependency graph construction require concrete types
> 
>**Error Messages**:
> 
> * Service: "Generic types cannot be used as service implementations"
> * Host: "Generic types cannot be marked as [Host]"
> * User: "Generic types cannot be marked as [User]"
>* Scope: "Generic types cannot be marked as [Scope]"
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
>```
> 
>---
> 
>## Improved Error Diagnostics
> 
>### 📊 Complete Dependency Chain Display
> 
>**Enhanced in RC.3**: When dependency resolution fails, error messages now show the complete dependency chain.
> 
> **Example Error Message**:
> ```
> Error: Failed to resolve dependency chain:
>   PlayerController (User)
>   → ICombatSystem (Service)
>   → IWeaponFactory (Service)
>   → IResourceLoader (missing)
>```
> 
> **Benefits**:
> * Quickly identify which service is missing
> * Understand the full context of dependency failures
>* Easier debugging of complex dependency graphs
> 
>---
> 
>### 🔍 Runtime Circular Dependency Detection
> 
>**Optimized in RC.3**: Circular dependency detection now runs only in DEBUG builds for better performance.
> 
> **Detection Scope**:
> * Only checks Service → Service constructor dependencies
> * Does not flag User `[Inject]` members (they resolve after construction)
> * Does not flag Host `[Singleton]` members
>* Does not flag Host+User self-injection patterns
> 
> **Why This Matters**:
> Host+User self-injection is not a circular dependency because:
> 1. Host registration doesn't trigger injection
> 2. Service construction completes first
> 3. User injection happens afterward
>4. No constructor cycle is formed
> 
>---
> 
>### 📝 Clearer Error Messages
> 
> **Improved in RC.3**: All error messages now include:
> * What went wrong
> * Why it's problematic  
> * Suggested fix when applicable
>* Complete dependency chain context
> 
>---
> 
>## Code Generation Improvements
> 
>### 🏭 Service Factory Optimization
> 
>**Changed in RC.3**: `ServiceFactories` is now a static collection for better memory efficiency.
> 
> **Impact**:
> * Reduced memory footprint
> * Faster service factory lookups
>* Better performance in large dependency graphs
> 
>---
> 
>### 🏭 Service Creation or Provision Failures Also Trigger Callbacks
> 
>**Changed in RC.3**: Service creation failures are now written into the service cache and trigger failure callbacks.
> 
>**Impact**:
> 
> - Better error propagation
> - Prevents waiting queues from hanging on services that have already definitively failed
>- Clearer error messages
> 
>---
> 
>### 📁 Enhanced File Naming
> 
>**Improved in RC.3**: Generated files now use `Namespace+MetaName` format for better organization.
> 
> **Example**:
> * Before: `PlayerController.DI.g.cs`
>* After: `MyGame.Player.PlayerController.DI.g.cs`
> 
> **Benefits**:
> * Avoids naming conflicts in large projects
> * Better file organization in solution explorer
>* Easier to locate generated files
> 
>---
> 
>## Migration Guide
> 
>### Required Changes
> 
>1. **Update Interface Implementation**:
>    ```csharp
>   // Replace this
>    public partial class MyClass : Node, IServicesReady
>    {
>        public void OnServicesReady() { }
>    }
>   
>    // With this
>   public partial class MyClass : Node, IDependenciesResolved
>    {
>       public void OnDependenciesResolved(bool isAllDependenciesReady)
>        {
>           if (isAllDependenciesReady)
>            {
>                // Your initialization code
>            }
>        }
>    }
>    ```
> 
>    2. **Check for Generic Types**:
>    * Remove generic type parameters from any Service, Host, User, or Scope classes
>    * Create concrete wrapper classes if needed
> 
> 3. **Optional: Add Failure Callbacks**:
>    ```csharp
>    [Inject(FailureCallback = true)]
>    private IOptionalService Service { get; set; }
>    
>    partial void OnServiceInjectionFailed(string error)
>    {
>        // Handle failure
>    }
>   ```
> 
> ---
> 
>## Summary
> 
> v1.0.0-rc.3 brings significant improvements to error handling and diagnostics:
> 
> ✅ **New Features**:
>    - Injection failure callbacks for fine-grained error handling
> - Injection ready indicators for runtime checks
> - Better error diagnostics with complete dependency chains
> 
> ⚠️ **Breaking Changes**:
> - `IServicesReady` → `IDependenciesResolved` (migration required)
>- Generic types no longer allowed in DI roles
> 
>🚀 **Performance**:
> - Static service factory collection
>- Runtime circular dependency detection only in DEBUG
> 
>---
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
>* Inject member type not exposed by any service (Error)
> 
>---
> 
>## Improved Error Messages
> 
>All diagnostic messages now provide:
> * Clear explanation of what went wrong
> * Why it's problematic
> * Suggested fix when applicable
>```csharp
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
>---
> 
>## Resource Organization
> 
>### Standardized Resource Naming
> 
>All diagnostic messages now use prefixed resource names:
> * `C_*` - Class-level diagnostics
>* `M_*` - Member-level diagnostics
> * `S_*` - Constructor-level diagnostics
> * `D_*` - Dependency graph diagnostics
> * `E_*` - Internal error diagnostics
> * `U_*` - User behavior diagnostics
> 
> ---
>
> It's almost production-ready and look forward to the stable 1.0 release! 🚀