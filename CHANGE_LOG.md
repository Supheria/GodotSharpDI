

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
> * **GDI_M044**: Inject member cannot be regular Node (Error)
> * **GDI_M045**: Inject member type should be interface (Warning)
> * **GDI_M050**: Singleton member type is invalid (Error)
> * **GDI_M052**: Singleton member is Host type (Warning)
> * **GDI_M053**: Singleton member cannot be User type (Error)
> * **GDI_M054**: Singleton member cannot be Scope/regular Node (Error)
> * **GDI_M055**: Singleton member exposed type not implemented (Error)
> * **GDI_M056**: Singleton member exposed type should be interface (Warning)
> * 
> * **GDI_S021**: Constructor parameter is Host type (Warning)
> * **GDI_S022**: Constructor parameter cannot be User type (Error)
> * **GDI_S023**: Constructor parameter cannot be Scope type (Error)
> * **GDI_S024**: Constructor parameter cannot be regular Node (Error)
> * **GDI_S025**: Constructor parameter should be interface (Warning)
> * 
> * **GDI_D050**: Inject member type not exposed by any service (Error)
>
> ---
>
> ## Improved Error Messages
>
> All diagnostic messages now provide:
>
> * Clear explanation of what went wrong
> * Why it's problematic
> * Suggested fix when applicable
>
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
>
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