# GodotSharpDI Future Roadmap - v1.2.0 and Beyond

## Overview

This document outlines the architectural vision for future versions of GodotSharpDI, focusing on:
1. Enhanced runtime dependency analysis
2. Bringing back service classes with `[Service]` attribute (replacing removed `[Singleton]`)
3. Auto-generating Host provider code to simplify user code
4. Supporting multiple service lifetimes (Singleton, Transient)

---

# Milestone 1: v1.2.0 - Runtime Dependency Analysis

## Goals

- Implement runtime circular dependency detection
- Provide detailed dependency visualization
- Add runtime dependency graph validation
- Improve debugging experience

---

## Feature 1: Dynamic Circular Dependency Detection

### Current Limitation (v1.1.0)

Currently, WaitFor circular dependencies are detected at **compile time** only. This is good but limited:

```csharp
// Detected at compile time ✅
[Provide(ExposedTypes = [typeof(IA)], WaitFor = [nameof(CreateB)])]
public IA CreateA() => new ServiceA();

[Provide(ExposedTypes = [typeof(IB)], WaitFor = [nameof(CreateA)])]
public IB CreateB() => new ServiceB();
```

However, **runtime injection cycles** through complex paths are not detected:

```csharp
// Not detected at compile time ❌
[Host]
public partial class Host1 : Node
{
    [Inject] private IServiceC _serviceC; // Will cause runtime cycle
    
    [Provide(ExposedTypes = [typeof(IServiceA)])]
    public IServiceA CreateA() => new ServiceA(_serviceC);
}

// In another scope
[Host] 
public partial class Host2 : Node
{
    [Inject] private IServiceA _serviceA; // Cycle: A->C->B->A
    
    [Provide(ExposedTypes = [typeof(IServiceB)])]
    public IServiceB CreateB() => new ServiceB(_serviceA);
}

public class ServiceC : IServiceC
{
    private readonly IServiceB _serviceB;
    public ServiceC(IServiceB serviceB) => _serviceB = serviceB;
}
```

### Proposed Solution: Runtime Dependency Tracker

Add a runtime dependency resolution tracker that detects cycles as they form:

```csharp
// Generated in Scope
public partial class GameScope
{
    #if DEBUG
    private readonly DependencyResolutionTracker _tracker = new();
    #endif
    
    public T GetService<T>() where T : class
    {
        #if DEBUG
        // Track dependency resolution
        if (_tracker.BeginResolving<T>())
        {
            try
            {
                var service = GetServiceInternal<T>();
                _tracker.EndResolving<T>();
                return service;
            }
            catch
            {
                _tracker.FailResolving<T>();
                throw;
            }
        }
        else
        {
            // Cycle detected!
            var cycle = _tracker.GetCurrentCycle<T>();
            throw new CircularDependencyException(
                $"Circular dependency detected:\n{FormatCycle(cycle)}"
            );
        }
        #else
        return GetServiceInternal<T>();
        #endif
    }
}
```

### Implementation Details

#### DependencyResolutionTracker Class

```csharp
public class DependencyResolutionTracker
{
    private readonly Stack<Type> _resolutionStack = new();
    private readonly HashSet<Type> _resolving = new();
    
    public bool BeginResolving<T>()
    {
        var type = typeof(T);
        
        if (_resolving.Contains(type))
        {
            return false; // Cycle detected
        }
        
        _resolutionStack.Push(type);
        _resolving.Add(type);
        return true;
    }
    
    public void EndResolving<T>()
    {
        var type = typeof(T);
        _resolutionStack.Pop();
        _resolving.Remove(type);
    }
    
    public void FailResolving<T>()
    {
        var type = typeof(T);
        while (_resolutionStack.Count > 0 && _resolutionStack.Peek() != type)
        {
            var failed = _resolutionStack.Pop();
            _resolving.Remove(failed);
        }
        
        if (_resolutionStack.Count > 0)
        {
            _resolutionStack.Pop();
            _resolving.Remove(type);
        }
    }
    
    public string GetCurrentCycle<T>()
    {
        var cycle = new List<Type>();
        var target = typeof(T);
        
        foreach (var type in _resolutionStack.Reverse())
        {
            cycle.Add(type);
            if (type == target)
                break;
        }
        
        return string.Join(" → ", cycle.Select(t => t.Name));
    }
}
```

### Benefits

✅ Catches runtime cycles that compile-time analysis misses  
✅ Provides detailed cycle information for debugging  
✅ Zero overhead in Release builds (DEBUG only)  
✅ Works with complex multi-scope scenarios

---

## Feature 2: Dependency Graph Visualization

### Goal

Provide runtime dependency graph export for debugging and documentation.

### API

```csharp
public interface IScope
{
    // Existing members...
    
    #if DEBUG
    DependencyGraph ExportDependencyGraph();
    #endif
}

public class DependencyGraph
{
    public List<ServiceNode> Services { get; set; }
    public List<DependencyEdge> Dependencies { get; set; }
    
    public string ToMermaid();  // Mermaid diagram format
    public string ToDot();      // GraphViz DOT format
    public string ToJson();     // JSON for custom visualization
}
```

### Example Usage

```csharp
[User]
public partial class DebugUI : Control, IDependenciesResolved
{
    [Inject] private IScope _scope;
    
    public void OnDependenciesResolved(bool isAllReady)
    {
        #if DEBUG
        var graph = _scope.ExportDependencyGraph();
        
        // Export as Mermaid diagram
        var mermaid = graph.ToMermaid();
        System.IO.File.WriteAllText("dependency_graph.md", $"```mermaid\n{mermaid}\n```");
        
        GD.Print("Dependency graph exported!");
        #endif
    }
}
```

---

# Milestone 2: v1.3.0 - Service Classes with Auto-Generated Hosts

## Philosophy: Best of Both Worlds

**v1.0.0 approach (removed in v1.1.0):**
- ✅ Simple service classes with `[Singleton]`
- ❌ Limited flexibility, no async support, no access to Node resources

**v1.1.0 approach (current):**
- ✅ Full flexibility with `[Provide]` in Hosts
- ✅ Async support, access to Node resources
- ❌ Verbose - requires manually writing provider methods

**v1.3.0 approach (proposed):**
- ✅ Simple service classes with `[Service]` (like old `[Singleton]`)
- ✅ Framework auto-generates Host with `[Provide]` methods
- ✅ Users can still write custom Hosts for complex scenarios
- ✅ Support for Singleton and Transient lifetimes

---

## Architecture: Service Classes + Auto-Generated Hosts

### User Code (Simple Services)

```csharp
// User writes simple service classes ✅
[Service(typeof(IConfig))]
public partial class ConfigService : IConfig
{
    public string GetValue(string key) => _data[key];
    private Dictionary<string, string> _data = new();
}

[Service(typeof(ILogger), Lifetime = ServiceLifetime.Transient)]
public partial class Logger : ILogger
{
    public void Log(string message) => GD.Print(message);
}

[Service(typeof(IDatabase))]
public partial class DatabaseService : IDatabase
{
    // Constructor injection - dependencies auto-resolved
    public DatabaseService(IConfig config)
    {
        _connectionString = config.GetValue("ConnectionString");
    }
    
    private readonly string _connectionString;
}
```

### Framework-Generated Code (Auto-Generated Host)

```csharp
// Framework automatically generates this ✨
[Host]
internal partial class __AutoGeneratedServicesHost__ : Node
{
    // For services with dependencies, auto-generate [Inject]
    [Inject] private IConfig _injected_IConfig;
    
    // Singleton service - cached instance
    private IConfig _singleton_IConfig;
    
    [Provide(ExposedTypes = [typeof(IConfig)])]
    public IConfig __Provide_ConfigService()
    {
        if (_singleton_IConfig == null)
        {
            _singleton_IConfig = new ConfigService();
        }
        return _singleton_IConfig;
    }
    
    // Transient service - new instance each time
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger __Provide_Logger()
    {
        return new Logger();
    }
    
    // Singleton with dependencies - wait for injection
    private IDatabase _singleton_IDatabase;
    
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_injected_IConfig)])]
    public IDatabase __Provide_DatabaseService()
    {
        if (_singleton_IDatabase == null)
        {
            _singleton_IDatabase = new DatabaseService(_injected_IConfig);
        }
        return _singleton_IDatabase;
    }
    
    public override partial void _Notification(int what);
}
```

### Auto-Registration in Scope

```csharp
// User's scope automatically includes generated host
[Modules(Hosts = [typeof(GameManager)])] // User's custom hosts
public partial class GameScope : Node, IScope
{
    public override partial void _Notification(int what);
}

// Framework generates this extension
partial class GameScope
{
    // Automatically add generated services host
    partial void OnModulesInitialized()
    {
        RegisterHost<__AutoGeneratedServicesHost__>();
    }
}
```

---

## Feature Details

### 1. Service Attribute

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ServiceAttribute : Attribute
{
    // Required: exposed type(s)
    public Type[] ExposedTypes { get; set; }
    
    // Optional: service lifetime (default: Singleton)
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Singleton;
    
    // Constructor
    public ServiceAttribute(params Type[] exposedTypes)
    {
        ExposedTypes = exposedTypes;
    }
}

public enum ServiceLifetime
{
    /// <summary>
    /// One instance per Scope (default)
    /// </summary>
    Singleton,
    
    /// <summary>
    /// New instance every time requested
    /// </summary>
    Transient
}
```

### 2. Dependency Resolution Rules

The generator analyzes service constructors to determine dependencies:

```csharp
[Service(typeof(IRepository))]
public partial class Repository : IRepository
{
    // Generator detects: depends on IDatabase and ILogger
    public Repository(IDatabase database, ILogger logger)
    {
        _database = database;
        _logger = logger;
    }
    
    private readonly IDatabase _database;
    private readonly ILogger _logger;
}

// Generated provider method:
[Inject] private IDatabase _injected_IDatabase;
[Inject] private ILogger _injected_ILogger;

[Provide(ExposedTypes = [typeof(IRepository)], 
         WaitFor = [nameof(_injected_IDatabase), nameof(_injected_ILogger)])]
public IRepository __Provide_Repository()
{
    if (_singleton_IRepository == null)
    {
        _singleton_IRepository = new Repository(_injected_IDatabase, _injected_ILogger);
    }
    return _singleton_IRepository;
}
```

### 3. Lifetime Implementation

#### Singleton (Default)

```csharp
// Generated field for caching
private IService _singleton_IService;

[Provide(ExposedTypes = [typeof(IService)])]
public IService __Provide_Service()
{
    return _singleton_IService ??= new ServiceImpl();
}
```

#### Transient

```csharp
// No caching - new instance each time
[Provide(ExposedTypes = [typeof(IService)])]
public IService __Provide_Service()
{
    return new ServiceImpl();
}
```

### 4. Async Service Support

Services can also be async:

```csharp
[Service(typeof(IDatabase))]
public partial class DatabaseService : IDatabase
{
    // Async factory method detected by generator
    public static async Task<DatabaseService> CreateAsync(IConfig config)
    {
        var service = new DatabaseService();
        await service.ConnectAsync(config.GetValue("ConnectionString"));
        return service;
    }
    
    private DatabaseService() { }
    
    private async Task ConnectAsync(string connectionString)
    {
        // Async initialization
    }
}

// Generated provider:
[Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_injected_IConfig)])]
public async Task<IDatabase> __Provide_DatabaseService()
{
    if (_singleton_IDatabase == null)
    {
        _singleton_IDatabase = await DatabaseService.CreateAsync(_injected_IConfig);
    }
    return _singleton_IDatabase;
}
```

---

## Coexistence with Manual Hosts

Users can still write custom Hosts for complex scenarios:

```csharp
// Auto-generated services (simple) ✅
[Service(typeof(IConfig))]
public partial class ConfigService : IConfig { }

[Service(typeof(ILogger))]
public partial class Logger : ILogger { }

// Manual Host (complex scenarios) ✅
[Host]
public partial class GameHost : Node, IDependenciesResolved
{
    [Inject] private IConfig _config;
    
    // Complex service needing Node resources
    [Provide(ExposedTypes = [typeof(ISceneManager)])]
    public ISceneManager CreateSceneManager()
    {
        // Access to Node context, scene tree, etc.
        return new SceneManager(GetTree(), _config);
    }
    
    // Async service with progress reporting
    [Provide(ExposedTypes = [typeof(IWorldGenerator)])]
    public async Task<IWorldGenerator> GenerateWorldAsync()
    {
        var generator = new WorldGenerator();
        
        // Can report progress via signals, update UI, etc.
        generator.OnProgress += (p) => EmitSignal("WorldLoadProgress", p);
        
        await generator.GenerateAsync();
        return generator;
    }
    
    public void OnDependenciesResolved(bool isAllReady) { }
    
    public override partial void _Notification(int what);
}

// Both auto-generated and manual hosts work together
[Modules(Hosts = [typeof(GameHost)])]
public partial class GameScope : Node, IScope
{
    // Auto-generated host is added automatically
    public override partial void _Notification(int what);
}
```

---

## Migration Path

### From v1.1.0 to v1.3.0

Users can migrate incrementally:

```csharp
// v1.1.0 (current) - Manual providers
[Host]
public partial class ServiceHost : Node
{
    [Provide(ExposedTypes = [typeof(IConfig)])]
    public IConfig CreateConfig() => new ConfigService();
    
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger CreateLogger() => new Logger();
}

// v1.3.0 Option 1 - Use [Service] (simpler)
[Service(typeof(IConfig))]
public partial class ConfigService : IConfig { }

[Service(typeof(ILogger))]
public partial class Logger : ILogger { }
// Auto-generated host handles everything ✨

// v1.3.0 Option 2 - Keep manual Host (more control)
[Host]
public partial class ServiceHost : Node
{
    [Provide(ExposedTypes = [typeof(IConfig)])]
    public IConfig CreateConfig() => new ConfigService();
    
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger CreateLogger() => new Logger();
}
// Still works exactly the same ✅
```

**Backward Compatibility**: v1.1.0 code continues to work in v1.3.0 without any changes.

---

## Benefits of This Approach

### For Simple Services

**Before (v1.1.0):**
```csharp
// Step 1: Define service class
public class ConfigService : IConfig
{
    public string GetValue(string key) => _data[key];
}

// Step 2: Create Host
[Host]
public partial class ServiceHost : Node
{
    [Provide(ExposedTypes = [typeof(IConfig)])]
    public IConfig CreateConfig() => new ConfigService();
    
    public override partial void _Notification(int what);
}

// Step 3: Register Host in Scope
[Modules(Hosts = [typeof(ServiceHost)])]
public partial class GameScope : Node, IScope { }
```

**After (v1.3.0):**
```csharp
// One step: Define service with [Service]
[Service(typeof(IConfig))]
public partial class ConfigService : IConfig
{
    public string GetValue(string key) => _data[key];
}

// Everything else is auto-generated! ✨
```

### For Complex Services

Manual Hosts still available for:
- Services needing access to Node resources (GetTree, scene tree, etc.)
- Services requiring complex initialization logic
- Services with custom async patterns
- Services needing to interact with Godot signals/events

---

## Implementation Strategy

### Phase 1: Code Generation Enhancement

1. **Service Class Scanner**
   - Scan assemblies for `[Service]` classes
   - Extract constructor dependencies
   - Determine service lifetime

2. **Host Code Generator**
   - Generate `__AutoGeneratedServicesHost__` class
   - Generate `[Provide]` methods for each service
   - Generate `[Inject]` members for dependencies
   - Generate WaitFor relationships

3. **Scope Integration**
   - Auto-register generated host in scopes
   - Maintain list of auto-generated services

### Phase 2: Dependency Analysis

1. **Constructor Analysis**
   - Parse constructor parameters
   - Identify service dependencies
   - Generate WaitFor chains

2. **Factory Method Detection**
   - Detect static `Create` or `CreateAsync` methods
   - Generate appropriate provider code

### Phase 3: Lifetime Management

1. **Singleton Implementation**
   - Generate caching fields
   - Implement lazy initialization
   - Thread-safe if needed

2. **Transient Implementation**
   - No caching
   - New instance per call

### Phase 4: Testing and Validation

1. **Compile-time Validation**
   - Ensure service constructors are valid
   - Detect missing dependencies
   - Validate lifetime configurations

2. **Runtime Testing**
   - Test auto-generated providers
   - Verify dependency injection
   - Check lifetime behaviors

---

## Example: Complete Application

### Service Definitions

```csharp
// Core services - auto-generated
[Service(typeof(IConfig))]
public partial class ConfigService : IConfig
{
    public string GetValue(string key) => _data[key];
    private Dictionary<string, string> _data = LoadConfig();
}

[Service(typeof(ILogger), Lifetime = ServiceLifetime.Transient)]
public partial class Logger : ILogger
{
    public void Log(string message) => GD.Print($"[{DateTime.Now}] {message}");
}

[Service(typeof(IDatabase))]
public partial class DatabaseService : IDatabase
{
    public DatabaseService(IConfig config, ILogger logger)
    {
        _logger = logger;
        _connectionString = config.GetValue("DbConnection");
        _logger.Log("DatabaseService initialized");
    }
    
    private readonly ILogger _logger;
    private readonly string _connectionString;
}

// Complex service - manual Host
[Host]
public partial class GameHost : Node, IDependenciesResolved
{
    [Inject] private IDatabase _database;
    [Inject] private ILogger _logger;
    
    [Provide(ExposedTypes = [typeof(IGameWorld)], 
             WaitFor = [nameof(_database), nameof(_logger)])]
    public async Task<IGameWorld> CreateGameWorldAsync()
    {
        _logger.Log("Creating game world...");
        
        var world = new GameWorld(GetTree());
        await world.LoadFromDatabase(_database);
        
        _logger.Log("Game world created");
        return world;
    }
    
    public void OnDependenciesResolved(bool isAllReady)
    {
        if (isAllReady)
        {
            _logger.Log("All dependencies resolved!");
        }
    }
    
    public override partial void _Notification(int what);
}
```

### Scope Definition

```csharp
// Simple scope definition
[Modules(Hosts = [typeof(GameHost)])]
public partial class GameScope : Node, IScope
{
    // Auto-generated host is added automatically
    public override partial void _Notification(int what);
}
```

### Usage

```csharp
[User]
public partial class PlayerController : Control, IDependenciesResolved
{
    // All services available - both auto-generated and manual
    [Inject] private IConfig _config;
    [Inject] private ILogger _logger;
    [Inject] private IDatabase _database;
    [Inject] private IGameWorld _gameWorld;
    
    public void OnDependenciesResolved(bool isAllReady)
    {
        if (isAllReady)
        {
            _logger.Log("PlayerController initialized");
            var playerName = _config.GetValue("PlayerName");
            _logger.Log($"Player: {playerName}");
        }
    }
    
    public override partial void _Notification(int what);
}
```

---

## Summary

### v1.2.0 Goals
✅ Runtime circular dependency detection  
✅ Dependency graph visualization  
✅ Better debugging tools

### v1.3.0 Goals
✅ Bring back service classes with `[Service]` attribute  
✅ Auto-generate Host provider code  
✅ Support Singleton and Transient lifetimes  
✅ Maintain backward compatibility with v1.1.0  
✅ Coexist with manual Hosts for complex scenarios

### Benefits
🎯 **Simplicity**: One-line service definitions for simple cases  
🎯 **Flexibility**: Manual Hosts still available for complex scenarios  
🎯 **Power**: Full async/await support, dependency injection  
🎯 **Safety**: Compile-time and runtime validation  
🎯 **Compatibility**: v1.1.0 code continues to work

This approach provides the best of both worlds - the simplicity of v1.0.0's `[Singleton]` and the flexibility of v1.1.0's `[Provide]`!
