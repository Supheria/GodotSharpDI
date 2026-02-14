# GodotSharpDI 未来路线图 - v1.2.0 及更高版本

## 概述

本文档概述了 GodotSharpDI 未来版本的架构愿景，重点关注：
1. 增强的运行时依赖分析
2. 使用 `[Service]` 特性恢复服务类（替换已移除的 `[Singleton]`）
3. 自动生成 Host 提供者代码以简化用户代码
4. 支持多种服务生命周期（Singleton、Transient）

---

# 里程碑 1：v1.2.0 - 运行时依赖分析

## 目标

- 实现运行时循环依赖检测
- 提供详细的依赖可视化
- 添加运行时依赖图验证
- 改进调试体验

---

## 功能 1：动态循环依赖检测

### 当前限制（v1.1.0）

目前，WaitFor 循环依赖仅在**编译时**检测。这很好但有局限：

```csharp
// 在编译时检测到 ✅
[Provide(ExposedTypes = [typeof(IA)], WaitFor = [nameof(CreateB)])]
public IA CreateA() => new ServiceA();

[Provide(ExposedTypes = [typeof(IB)], WaitFor = [nameof(CreateA)])]
public IB CreateB() => new ServiceB();
```

然而，通过复杂路径的**运行时注入循环**未被检测：

```csharp
// 在编译时未检测到 ❌
[Host]
public partial class Host1 : Node
{
    [Inject] private IServiceC _serviceC; // 将导致运行时循环
    
    [Provide(ExposedTypes = [typeof(IServiceA)])]
    public IServiceA CreateA() => new ServiceA(_serviceC);
}

// 在另一个 scope 中
[Host] 
public partial class Host2 : Node
{
    [Inject] private IServiceA _serviceA; // 循环：A->C->B->A
    
    [Provide(ExposedTypes = [typeof(IServiceB)])]
    public IServiceB CreateB() => new ServiceB(_serviceA);
}

public class ServiceC : IServiceC
{
    private readonly IServiceB _serviceB;
    public ServiceC(IServiceB serviceB) => _serviceB = serviceB;
}
```

### 提议的解决方案：运行时依赖跟踪器

添加运行时依赖解析跟踪器，在循环形成时检测：

```csharp
// 在 Scope 中生成
public partial class GameScope
{
    #if DEBUG
    private readonly DependencyResolutionTracker _tracker = new();
    #endif
    
    public T GetService<T>() where T : class
    {
        #if DEBUG
        // 跟踪依赖解析
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
            // 检测到循环！
            var cycle = _tracker.GetCurrentCycle<T>();
            throw new CircularDependencyException(
                $"检测到循环依赖:\n{FormatCycle(cycle)}"
            );
        }
        #else
        return GetServiceInternal<T>();
        #endif
    }
}
```

### 实现细节

#### DependencyResolutionTracker 类

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
            return false; // 检测到循环
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

### 优势

✅ 捕获编译时分析遗漏的运行时循环  
✅ 提供详细的循环信息用于调试  
✅ Release 构建中零开销（仅 DEBUG）  
✅ 适用于复杂的多 scope 场景

---

## 功能 2：依赖图可视化

### 目标

提供运行时依赖图导出用于调试和文档。

### API

```csharp
public interface IScope
{
    // 现有成员...
    
    #if DEBUG
    DependencyGraph ExportDependencyGraph();
    #endif
}

public class DependencyGraph
{
    public List<ServiceNode> Services { get; set; }
    public List<DependencyEdge> Dependencies { get; set; }
    
    public string ToMermaid();  // Mermaid 图表格式
    public string ToDot();      // GraphViz DOT 格式
    public string ToJson();     // JSON 用于自定义可视化
}
```

### 示例用法

```csharp
[User]
public partial class DebugUI : Control, IDependenciesResolved
{
    [Inject] private IScope _scope;
    
    public void OnDependenciesResolved(bool isAllReady)
    {
        #if DEBUG
        var graph = _scope.ExportDependencyGraph();
        
        // 导出为 Mermaid 图表
        var mermaid = graph.ToMermaid();
        System.IO.File.WriteAllText("dependency_graph.md", $"```mermaid\n{mermaid}\n```");
        
        GD.Print("依赖图已导出！");
        #endif
    }
}
```

---

# 里程碑 2：v1.3.0 - 带自动生成 Host 的服务类

## 理念：两全其美

**v1.0.0 方法（在 v1.1.0 中移除）：**
- ✅ 使用 `[Singleton]` 的简单服务类
- ❌ 灵活性有限，不支持异步，无法访问 Node 资源

**v1.1.0 方法（当前）：**
- ✅ Host 中使用 `[Provide]` 的完全灵活性
- ✅ 异步支持，访问 Node 资源
- ❌ 冗长 - 需要手动编写提供者方法

**v1.3.0 方法（提议）：**
- ✅ 使用 `[Service]` 的简单服务类（类似旧的 `[Singleton]`）
- ✅ 框架自动生成带 `[Provide]` 方法的 Host
- ✅ 用户仍可为复杂场景编写自定义 Host
- ✅ 支持 Singleton 和 Transient 生命周期

---

## 架构：服务类 + 自动生成的 Host

### 用户代码（简单服务）

```csharp
// 用户编写简单的服务类 ✅
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
    // 构造函数注入 - 依赖自动解析
    public DatabaseService(IConfig config)
    {
        _connectionString = config.GetValue("ConnectionString");
    }
    
    private readonly string _connectionString;
}
```

### 框架生成的代码（自动生成的 Host）

```csharp
// 框架自动生成这个 ✨
[Host]
internal partial class __AutoGeneratedServicesHost__ : Node
{
    // 对于有依赖的服务，自动生成 [Inject]
    [Inject] private IConfig _injected_IConfig;
    
    // Singleton 服务 - 缓存实例
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
    
    // Transient 服务 - 每次新实例
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger __Provide_Logger()
    {
        return new Logger();
    }
    
    // 带依赖的 Singleton - 等待注入
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

### Scope 中的自动注册

```csharp
// 用户的 scope 自动包含生成的 host
[Modules(Hosts = [typeof(GameManager)])] // 用户的自定义 hosts
public partial class GameScope : Node, IScope
{
    public override partial void _Notification(int what);
}

// 框架生成此扩展
partial class GameScope
{
    // 自动添加生成的服务 host
    partial void OnModulesInitialized()
    {
        RegisterHost<__AutoGeneratedServicesHost__>();
    }
}
```

---

## 功能详情

### 1. Service 特性

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ServiceAttribute : Attribute
{
    // 必需：暴露的类型
    public Type[] ExposedTypes { get; set; }
    
    // 可选：服务生命周期（默认：Singleton）
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Singleton;
    
    // 构造函数
    public ServiceAttribute(params Type[] exposedTypes)
    {
        ExposedTypes = exposedTypes;
    }
}

public enum ServiceLifetime
{
    /// <summary>
    /// 每个 Scope 一个实例（默认）
    /// </summary>
    Singleton,
    
    /// <summary>
    /// 每次请求时新实例
    /// </summary>
    Transient
}
```

### 2. 依赖解析规则

生成器分析服务构造函数以确定依赖：

```csharp
[Service(typeof(IRepository))]
public partial class Repository : IRepository
{
    // 生成器检测：依赖于 IDatabase 和 ILogger
    public Repository(IDatabase database, ILogger logger)
    {
        _database = database;
        _logger = logger;
    }
    
    private readonly IDatabase _database;
    private readonly ILogger _logger;
}

// 生成的提供者方法：
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

### 3. 生命周期实现

#### Singleton（默认）

```csharp
// 生成的缓存字段
private IService _singleton_IService;

[Provide(ExposedTypes = [typeof(IService)])]
public IService __Provide_Service()
{
    return _singleton_IService ??= new ServiceImpl();
}
```

#### Transient

```csharp
// 无缓存 - 每次调用新实例
[Provide(ExposedTypes = [typeof(IService)])]
public IService __Provide_Service()
{
    return new ServiceImpl();
}
```

### 4. 异步服务支持

服务也可以是异步的：

```csharp
[Service(typeof(IDatabase))]
public partial class DatabaseService : IDatabase
{
    // 生成器检测到异步工厂方法
    public static async Task<DatabaseService> CreateAsync(IConfig config)
    {
        var service = new DatabaseService();
        await service.ConnectAsync(config.GetValue("ConnectionString"));
        return service;
    }
    
    private DatabaseService() { }
    
    private async Task ConnectAsync(string connectionString)
    {
        // 异步初始化
    }
}

// 生成的提供者：
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

## 与手动 Host 共存

用户仍可为复杂场景编写自定义 Host：

```csharp
// 自动生成的服务（简单）✅
[Service(typeof(IConfig))]
public partial class ConfigService : IConfig { }

[Service(typeof(ILogger))]
public partial class Logger : ILogger { }

// 手动 Host（复杂场景）✅
[Host]
public partial class GameHost : Node, IDependenciesResolved
{
    [Inject] private IConfig _config;
    
    // 需要 Node 资源的复杂服务
    [Provide(ExposedTypes = [typeof(ISceneManager)])]
    public ISceneManager CreateSceneManager()
    {
        // 访问 Node 上下文、场景树等
        return new SceneManager(GetTree(), _config);
    }
    
    // 带进度报告的异步服务
    [Provide(ExposedTypes = [typeof(IWorldGenerator)])]
    public async Task<IWorldGenerator> GenerateWorldAsync()
    {
        var generator = new WorldGenerator();
        
        // 可以通过信号报告进度、更新 UI 等
        generator.OnProgress += (p) => EmitSignal("WorldLoadProgress", p);
        
        await generator.GenerateAsync();
        return generator;
    }
    
    public void OnDependenciesResolved(bool isAllReady) { }
    
    public override partial void _Notification(int what);
}

// 自动生成和手动 host 一起工作
[Modules(Hosts = [typeof(GameHost)])]
public partial class GameScope : Node, IScope
{
    // 自动生成的 host 自动添加
    public override partial void _Notification(int what);
}
```

---

## 迁移路径

### 从 v1.1.0 到 v1.3.0

用户可以渐进式迁移：

```csharp
// v1.1.0（当前）- 手动提供者
[Host]
public partial class ServiceHost : Node
{
    [Provide(ExposedTypes = [typeof(IConfig)])]
    public IConfig CreateConfig() => new ConfigService();
    
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger CreateLogger() => new Logger();
}

// v1.3.0 选项 1 - 使用 [Service]（更简单）
[Service(typeof(IConfig))]
public partial class ConfigService : IConfig { }

[Service(typeof(ILogger))]
public partial class Logger : ILogger { }
// 自动生成的 host 处理一切 ✨

// v1.3.0 选项 2 - 保留手动 Host（更多控制）
[Host]
public partial class ServiceHost : Node
{
    [Provide(ExposedTypes = [typeof(IConfig)])]
    public IConfig CreateConfig() => new ConfigService();
    
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger CreateLogger() => new Logger();
}
// 仍然完全一样工作 ✅
```

**向后兼容性**：v1.1.0 代码在 v1.3.0 中无需任何更改即可继续工作。

---

## 这种方法的优势

### 对于简单服务

**之前（v1.1.0）：**
```csharp
// 步骤 1：定义服务类
public class ConfigService : IConfig
{
    public string GetValue(string key) => _data[key];
}

// 步骤 2：创建 Host
[Host]
public partial class ServiceHost : Node
{
    [Provide(ExposedTypes = [typeof(IConfig)])]
    public IConfig CreateConfig() => new ConfigService();
    
    public override partial void _Notification(int what);
}

// 步骤 3：在 Scope 中注册 Host
[Modules(Hosts = [typeof(ServiceHost)])]
public partial class GameScope : Node, IScope { }
```

**之后（v1.3.0）：**
```csharp
// 一步：用 [Service] 定义服务
[Service(typeof(IConfig))]
public partial class ConfigService : IConfig
{
    public string GetValue(string key) => _data[key];
}

// 其他一切都是自动生成的！✨
```

### 对于复杂服务

手动 Host 仍可用于：
- 需要访问 Node 资源（GetTree、场景树等）的服务
- 需要复杂初始化逻辑的服务
- 具有自定义异步模式的服务
- 需要与 Godot 信号/事件交互的服务

---

## 实现策略

### 阶段 1：代码生成增强

1. **服务类扫描器**
   - 扫描程序集中的 `[Service]` 类
   - 提取构造函数依赖
   - 确定服务生命周期

2. **Host 代码生成器**
   - 生成 `__AutoGeneratedServicesHost__` 类
   - 为每个服务生成 `[Provide]` 方法
   - 为依赖生成 `[Inject]` 成员
   - 生成 WaitFor 关系

3. **Scope 集成**
   - 在 scope 中自动注册生成的 host
   - 维护自动生成服务的列表

### 阶段 2：依赖分析

1. **构造函数分析**
   - 解析构造函数参数
   - 识别服务依赖
   - 生成 WaitFor 链

2. **工厂方法检测**
   - 检测静态 `Create` 或 `CreateAsync` 方法
   - 生成适当的提供者代码

### 阶段 3：生命周期管理

1. **Singleton 实现**
   - 生成缓存字段
   - 实现延迟初始化
   - 如果需要则线程安全

2. **Transient 实现**
   - 无缓存
   - 每次调用新实例

### 阶段 4：测试和验证

1. **编译时验证**
   - 确保服务构造函数有效
   - 检测缺失的依赖
   - 验证生命周期配置

2. **运行时测试**
   - 测试自动生成的提供者
   - 验证依赖注入
   - 检查生命周期行为

---

## 示例：完整应用程序

### 服务定义

```csharp
// 核心服务 - 自动生成
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
        _logger.Log("DatabaseService 已初始化");
    }
    
    private readonly ILogger _logger;
    private readonly string _connectionString;
}

// 复杂服务 - 手动 Host
[Host]
public partial class GameHost : Node, IDependenciesResolved
{
    [Inject] private IDatabase _database;
    [Inject] private ILogger _logger;
    
    [Provide(ExposedTypes = [typeof(IGameWorld)], 
             WaitFor = [nameof(_database), nameof(_logger)])]
    public async Task<IGameWorld> CreateGameWorldAsync()
    {
        _logger.Log("正在创建游戏世界...");
        
        var world = new GameWorld(GetTree());
        await world.LoadFromDatabase(_database);
        
        _logger.Log("游戏世界已创建");
        return world;
    }
    
    public void OnDependenciesResolved(bool isAllReady)
    {
        if (isAllReady)
        {
            _logger.Log("所有依赖已解析！");
        }
    }
    
    public override partial void _Notification(int what);
}
```

### Scope 定义

```csharp
// 简单的 scope 定义
[Modules(Hosts = [typeof(GameHost)])]
public partial class GameScope : Node, IScope
{
    // 自动生成的 host 自动添加
    public override partial void _Notification(int what);
}
```

### 使用

```csharp
[User]
public partial class PlayerController : Control, IDependenciesResolved
{
    // 所有服务可用 - 自动生成和手动的都可以
    [Inject] private IConfig _config;
    [Inject] private ILogger _logger;
    [Inject] private IDatabase _database;
    [Inject] private IGameWorld _gameWorld;
    
    public void OnDependenciesResolved(bool isAllReady)
    {
        if (isAllReady)
        {
            _logger.Log("PlayerController 已初始化");
            var playerName = _config.GetValue("PlayerName");
            _logger.Log($"玩家: {playerName}");
        }
    }
    
    public override partial void _Notification(int what);
}
```

---

## 总结

### v1.2.0 目标
✅ 运行时循环依赖检测  
✅ 依赖图可视化  
✅ 更好的调试工具

### v1.3.0 目标
✅ 使用 `[Service]` 特性恢复服务类  
✅ 自动生成 Host 提供者代码  
✅ 支持 Singleton 和 Transient 生命周期  
✅ 保持与 v1.1.0 的向后兼容性  
✅ 与复杂场景的手动 Host 共存

### 优势
🎯 **简洁性**：简单情况下的一行服务定义  
🎯 **灵活性**：复杂场景仍可使用手动 Host  
🎯 **强大性**：完整的 async/await 支持、依赖注入  
🎯 **安全性**：编译时和运行时验证  
🎯 **兼容性**：v1.1.0 代码继续工作

这种方法提供了两全其美 - v1.0.0 的 `[Singleton]` 的简洁性和 v1.1.0 的 `[Provide]` 的灵活性！
