# Threading and Async Safety in GodotSharpDI

## Overview

GodotSharpDI 1.1.0 provides full support for asynchronous service initialization while ensuring thread safety when working with Godot's single-threaded architecture.

## CallDeferred Integration

All async service providers automatically use Godot's `CallDeferred` mechanism to ensure thread safety. When an async provider completes (potentially on a background thread), the framework automatically marshals the result back to the main thread using `CallDeferred`.

## Godot's Threading Model

Godot Engine uses a **single main thread** for most operations:
- Scene tree manipulation
- Node lifecycle events
- Signal emissions
- Most API calls

**Critical**: Direct manipulation of Godot objects from background threads is **not thread-safe** and will cause crashes or undefined behavior.

## How GodotSharpDI Ensures Thread Safety

### 1. CallDeferred Integration

When async providers complete on background threads, GodotSharpDI automatically uses `CallDeferred` to marshal results back to the main thread:

```csharp
// Generated code (simplified)
private static async Task ProvideAsync_LoadResources_IResourceLoader(
    Task<IResourceLoader> task, 
    IScope scope)
{
    try
    {
        var result = await task; // May complete on background thread
        
        // Use CallDeferred to return to main thread
        Callable.From(() =>
        {
            scope.ProvideService<IResourceLoader>(result);
        }).CallDeferred();
    }
    catch (Exception ex)
    {
        Callable.From(() =>
        {
            scope.ProvideService<IResourceLoader>(null, ex.Message);
        }).CallDeferred();
    }
}
```

### 2. Thread Safety Guarantees

**What's Safe**:
- ✅ Async I/O operations (file loading, network requests)
- ✅ CPU-intensive computations on background threads
- ✅ Database queries
- ✅ Any async/await operations that don't touch Godot APIs

**What's NOT Safe (without CallDeferred)**:
- ❌ Creating or modifying Nodes from background threads
- ❌ Calling Godot scene tree methods from background threads
- ❌ Accessing Node properties from background threads
- ❌ Emitting signals from background threads

### 3. Example: Safe Async Provider

```csharp
[Host]
public partial class DataHost : Node
{
    // ✅ Safe: I/O operations don't touch Godot APIs directly
    [Provide(ExposedTypes = [typeof(IDatabase)])]
    public async Task<IDatabase> ConnectToDatabaseAsync()
    {
        var db = new DatabaseService();
        
        // This runs on background thread - safe because it's just I/O
        await db.ConnectAsync();
        await db.LoadSchemaAsync();
        
        // Result is automatically marshaled to main thread via CallDeferred
        return db;
    }
    
    // ✅ Safe: Computation doesn't touch Godot APIs
    [Provide(ExposedTypes = [typeof(IWorldGenerator)])]
    public async Task<IWorldGenerator> GenerateWorldAsync()
    {
        var generator = new WorldGenerator();
        
        // Heavy computation on background thread - safe
        await Task.Run(() => generator.GenerateTerrain());
        
        // Result marshaled to main thread automatically
        return generator;
    }
    
    // ⚠️ Warning: Must be careful inside the provider
    [Provide(ExposedTypes = [typeof(ISceneLoader)])]
    public async Task<ISceneLoader> LoadSceneAsync()
    {
        var loader = new SceneLoader();
        
        // ❌ WRONG: Don't call Godot APIs from background thread
        // await Task.Run(() => loader.LoadScene(GetTree())); 
        
        // ✅ RIGHT: Only do async I/O, not Godot API calls
        await loader.LoadSceneDataAsync();
        
        return loader;
    }
    
    public override partial void _Notification(int what);
}
```

## Best Practices

### 1. Keep Godot API Calls on Main Thread

```csharp
// ❌ WRONG
[Provide(ExposedTypes = [typeof(INodeFactory)])]
public async Task<INodeFactory> CreateFactoryAsync()
{
    var factory = new NodeFactory();
    
    // Calling Godot APIs on background thread - CRASH!
    await Task.Run(() => 
    {
        factory.RootNode = new Node(); // ❌ Creates Node on background thread
    });
    
    return factory;
}

// ✅ CORRECT
[Provide(ExposedTypes = [typeof(INodeFactory)])]
public async Task<INodeFactory> CreateFactoryAsync()
{
    var factory = new NodeFactory();
    
    // Do async I/O or computation without touching Godot APIs
    await factory.LoadTemplatesAsync();
    
    // Factory can create Nodes later on main thread
    return factory;
}
```

### 2. Use ConfigureAwait(false) for Better Performance

When you know the continuation doesn't need to be on the main thread:

```csharp
[Provide(ExposedTypes = [typeof(IDataLoader)])]
public async Task<IDataLoader> LoadDataAsync()
{
    var loader = new DataLoader();
    
    // ConfigureAwait(false) allows continuation on any thread
    // (Framework will still marshal final result to main thread)
    await loader.ReadFileAsync().ConfigureAwait(false);
    await loader.ParseDataAsync().ConfigureAwait(false);
    
    return loader;
}
```

### 3. Initialize Godot Objects After Service Creation

```csharp
public class ResourceManager : IResourceManager, IDependenciesResolved
{
    private Dictionary<string, Resource> _resources;
    
    // Constructor doesn't touch Godot APIs - safe
    public ResourceManager()
    {
        _resources = new Dictionary<string, Resource>();
    }
    
    // Initialize Godot resources after injection, on main thread
    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        // Now we're guaranteed to be on main thread
        LoadGodotResources();
    }
    
    private void LoadGodotResources()
    {
        // Safe to create Godot resources here
        _resources["player"] = GD.Load<PackedScene>("res://Player.tscn");
    }
}

[Host]
public partial class GameHost : Node
{
    [Provide(ExposedTypes = [typeof(IResourceManager)])]
    public async Task<IResourceManager> CreateResourceManagerAsync()
    {
        var manager = new ResourceManager();
        
        // Load data asynchronously (no Godot APIs)
        await manager.LoadManifestAsync();
        
        // Godot resources will be loaded in OnDependenciesResolved
        return manager;
    }
    
    public override partial void _Notification(int what);
}
```

## Common Patterns

### Pattern 1: Async I/O, Sync Initialization

```csharp
[Host]
public partial class ConfigHost : Node
{
    [Provide(ExposedTypes = [typeof(IConfig)])]
    public async Task<IConfig> LoadConfigAsync()
    {
        var config = new ConfigService();
        
        // Async file I/O - runs on thread pool
        string jsonData = await File.ReadAllTextAsync("config.json");
        
        // Synchronous parsing - runs on main thread after CallDeferred
        config.Parse(jsonData);
        
        return config;
    }
    
    public override partial void _Notification(int what);
}
```

### Pattern 2: Parallel Async Operations

```csharp
[Host]
public partial class AssetHost : Node
{
    [Provide(ExposedTypes = [typeof(IAssetLoader)])]
    public async Task<IAssetLoader> LoadAssetsAsync()
    {
        var loader = new AssetLoader();
        
        // Load multiple assets in parallel
        var tasks = new[]
        {
            loader.LoadTexturesAsync(),
            loader.LoadSoundsAsync(),
            loader.LoadModelsAsync()
        };
        
        await Task.WhenAll(tasks);
        
        return loader;
    }
    
    public override partial void _Notification(int what);
}
```

### Pattern 3: Progress Reporting (Advanced)

For long-running operations, you might want to report progress:

```csharp
public interface IProgressReporter
{
    void ReportProgress(float progress);
}

[Host]
public partial class LoadingHost : Node, IProgressReporter
{
    [Signal]
    public delegate void LoadingProgressEventHandler(float progress);
    
    [Provide(ExposedTypes = [typeof(IWorldLoader)])]
    public async Task<IWorldLoader> LoadWorldAsync()
    {
        var loader = new WorldLoader();
        
        // Report progress from background thread
        loader.OnProgress += (progress) =>
        {
            // Use CallDeferred to emit signal on main thread
            CallDeferred(nameof(EmitProgressSignal), progress);
        };
        
        await loader.LoadAsync();
        
        return loader;
    }
    
    private void EmitProgressSignal(float progress)
    {
        EmitSignal(SignalName.LoadingProgress, progress);
    }
    
    public void ReportProgress(float progress)
    {
        CallDeferred(nameof(EmitProgressSignal), progress);
    }
    
    public override partial void _Notification(int what);
}
```

## Debugging Thread Issues

### Symptoms of Thread Safety Violations

1. **Random crashes** with no clear stack trace
2. **Objects becoming null** unexpectedly
3. **Race conditions** causing inconsistent state
4. **Godot warnings** about accessing scene tree from wrong thread

### How to Debug

1. **Enable Thread Sanitizer** (if available on your platform)
2. **Add logging** to track which thread operations occur on:

```csharp
[Provide(ExposedTypes = [typeof(IService)])]
public async Task<IService> CreateServiceAsync()
{
    GD.Print($"Creating service on thread: {Thread.CurrentThread.ManagedThreadId}");
    
    var service = new MyService();
    await service.InitializeAsync();
    
    GD.Print($"Service initialized on thread: {Thread.CurrentThread.ManagedThreadId}");
    
    return service;
}
```

3. **Check Godot console** for thread-related warnings

## Performance Considerations

### CallDeferred Overhead

- `CallDeferred` adds minimal overhead (single frame delay)
- The safety benefit far outweighs the performance cost
- Service initialization happens once per scope lifetime

### When to Use Async

**Good Use Cases**:
- Loading files from disk
- Network requests
- Database queries
- CPU-intensive calculations
- Parallel operations

**Poor Use Cases**:
- Services that initialize instantly
- Operations that are already on main thread
- Simple object construction

## Migration from Synchronous to Asynchronous

If you have a synchronous provider that needs to become async:

```csharp
// Before (Synchronous)
[Provide(ExposedTypes = [typeof(IDatabase)])]
public IDatabase CreateDatabase()
{
    var db = new DatabaseService();
    db.Connect(); // Blocks main thread
    return db;
}

// After (Asynchronous)
[Provide(ExposedTypes = [typeof(IDatabase)])]
public async Task<IDatabase> CreateDatabaseAsync()
{
    var db = new DatabaseService();
    await db.ConnectAsync(); // Doesn't block main thread
    return db;
}
```

No other changes needed - the framework handles everything else automatically!

## Summary

✅ **GodotSharpDI automatically ensures thread safety** through CallDeferred
✅ **Async providers can safely do I/O and computation** on background threads
✅ **Service results are always provided on the main thread**
⚠️ **Never call Godot APIs directly from background threads**
⚠️ **Let the framework handle thread marshaling**

The framework makes async/await work seamlessly with Godot's threading model!
