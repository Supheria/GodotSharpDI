# GodotSharpDI 中的线程与异步安全

## 概述

GodotSharpDI 1.1.0 提供了对异步服务初始化的完全支持，同时确保与 Godot 单线程架构配合时的线程安全。

## CallDeferred 集成

所有异步服务提供者都会自动使用 Godot 的 `CallDeferred` 机制来确保线程安全。当异步提供者完成时（可能在后台线程上），框架会自动使用 `CallDeferred` 将结果编组回主线程。

## Godot 的线程模型

Godot 引擎对大多数操作使用**单个主线程**：
- 场景树操作
- Node 生命周期事件
- 信号发射
- 大多数 API 调用

**关键**：从后台线程直接操作 Godot 对象**不是线程安全的**，会导致崩溃或未定义行为。

## GodotSharpDI 如何确保线程安全

### 1. CallDeferred 集成

当异步提供者在后台线程上完成时，GodotSharpDI 会自动使用 `CallDeferred` 将结果编组回主线程：

```csharp
// 生成的代码（简化版）
private static async Task ProvideAsync_LoadResources_IResourceLoader(
    Task<IResourceLoader> task, 
    IScope scope)
{
    try
    {
        var result = await task; // 可能在后台线程上完成
        
        // 使用 CallDeferred 返回主线程
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

### 2. 线程安全保证

**什么是安全的**：
- ✅ 异步 I/O 操作（文件加载、网络请求）
- ✅ 后台线程上的 CPU 密集型计算
- ✅ 数据库查询
- ✅ 任何不接触 Godot API 的 async/await 操作

**什么是不安全的（没有 CallDeferred）**：
- ❌ 从后台线程创建或修改 Node
- ❌ 从后台线程调用 Godot 场景树方法
- ❌ 从后台线程访问 Node 属性
- ❌ 从后台线程发射信号

### 3. 示例：安全的异步提供者

```csharp
[Host]
public partial class DataHost : Node
{
    // ✅ 安全：I/O 操作不直接接触 Godot API
    [Provide(ExposedTypes = [typeof(IDatabase)])]
    public async Task<IDatabase> ConnectToDatabaseAsync()
    {
        var db = new DatabaseService();
        
        // 这在后台线程上运行 - 安全，因为只是 I/O
        await db.ConnectAsync();
        await db.LoadSchemaAsync();
        
        // 结果通过 CallDeferred 自动编组到主线程
        return db;
    }
    
    // ✅ 安全：计算不接触 Godot API
    [Provide(ExposedTypes = [typeof(IWorldGenerator)])]
    public async Task<IWorldGenerator> GenerateWorldAsync()
    {
        var generator = new WorldGenerator();
        
        // 后台线程上的重度计算 - 安全
        await Task.Run(() => generator.GenerateTerrain());
        
        // 结果自动编组到主线程
        return generator;
    }
    
    // ⚠️ 警告：必须小心提供者内部
    [Provide(ExposedTypes = [typeof(ISceneLoader)])]
    public async Task<ISceneLoader> LoadSceneAsync()
    {
        var loader = new SceneLoader();
        
        // ❌ 错误：不要从后台线程调用 Godot API
        // await Task.Run(() => loader.LoadScene(GetTree())); 
        
        // ✅ 正确：只做异步 I/O，不调用 Godot API
        await loader.LoadSceneDataAsync();
        
        return loader;
    }
    
    public override partial void _Notification(int what);
}
```

## 最佳实践

### 1. 在主线程上保留 Godot API 调用

```csharp
// ❌ 错误
[Provide(ExposedTypes = [typeof(INodeFactory)])]
public async Task<INodeFactory> CreateFactoryAsync()
{
    var factory = new NodeFactory();
    
    // 在后台线程上调用 Godot API - 崩溃！
    await Task.Run(() => 
    {
        factory.RootNode = new Node(); // ❌ 在后台线程上创建 Node
    });
    
    return factory;
}

// ✅ 正确
[Provide(ExposedTypes = [typeof(INodeFactory)])]
public async Task<INodeFactory> CreateFactoryAsync()
{
    var factory = new NodeFactory();
    
    // 执行异步 I/O 或计算而不接触 Godot API
    await factory.LoadTemplatesAsync();
    
    // Factory 可以稍后在主线程上创建 Node
    return factory;
}
```

### 2. 使用 ConfigureAwait(false) 以获得更好的性能

当你知道延续不需要在主线程上时：

```csharp
[Provide(ExposedTypes = [typeof(IDataLoader)])]
public async Task<IDataLoader> LoadDataAsync()
{
    var loader = new DataLoader();
    
    // ConfigureAwait(false) 允许在任何线程上继续
    // （框架仍然会将最终结果编组到主线程）
    await loader.ReadFileAsync().ConfigureAwait(false);
    await loader.ParseDataAsync().ConfigureAwait(false);
    
    return loader;
}
```

### 3. 在服务创建后初始化 Godot 对象

```csharp
public class ResourceManager : IResourceManager, IDependenciesResolved
{
    private Dictionary<string, Resource> _resources;
    
    // 构造函数不接触 Godot API - 安全
    public ResourceManager()
    {
        _resources = new Dictionary<string, Resource>();
    }
    
    // 在注入后初始化 Godot 资源，在主线程上
    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        // 现在我们保证在主线程上
        LoadGodotResources();
    }
    
    private void LoadGodotResources()
    {
        // 在这里创建 Godot 资源是安全的
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
        
        // 异步加载数据（不使用 Godot API）
        await manager.LoadManifestAsync();
        
        // Godot 资源将在 OnDependenciesResolved 中加载
        return manager;
    }
    
    public override partial void _Notification(int what);
}
```

## 常见模式

### 模式 1：异步 I/O，同步初始化

```csharp
[Host]
public partial class ConfigHost : Node
{
    [Provide(ExposedTypes = [typeof(IConfig)])]
    public async Task<IConfig> LoadConfigAsync()
    {
        var config = new ConfigService();
        
        // 异步文件 I/O - 在线程池上运行
        string jsonData = await File.ReadAllTextAsync("config.json");
        
        // 同步解析 - 在 CallDeferred 后在主线程上运行
        config.Parse(jsonData);
        
        return config;
    }
    
    public override partial void _Notification(int what);
}
```

### 模式 2：并行异步操作

```csharp
[Host]
public partial class AssetHost : Node
{
    [Provide(ExposedTypes = [typeof(IAssetLoader)])]
    public async Task<IAssetLoader> LoadAssetsAsync()
    {
        var loader = new AssetLoader();
        
        // 并行加载多个资源
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

### 模式 3：进度报告（高级）

对于长时间运行的操作，你可能想报告进度：

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
        
        // 从后台线程报告进度
        loader.OnProgress += (progress) =>
        {
            // 使用 CallDeferred 在主线程上发射信号
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

## 调试线程问题

### 线程安全违规的症状

1. **随机崩溃**，没有明确的堆栈跟踪
2. **对象意外变为 null**
3. **竞态条件**导致状态不一致
4. **Godot 警告**关于从错误线程访问场景树

### 如何调试

1. **启用线程清理器**（如果在你的平台上可用）
2. **添加日志**来跟踪操作发生在哪个线程上：

```csharp
[Provide(ExposedTypes = [typeof(IService)])]
public async Task<IService> CreateServiceAsync()
{
    GD.Print($"在线程上创建服务: {Thread.CurrentThread.ManagedThreadId}");
    
    var service = new MyService();
    await service.InitializeAsync();
    
    GD.Print($"服务初始化在线程上: {Thread.CurrentThread.ManagedThreadId}");
    
    return service;
}
```

3. **检查 Godot 控制台**的线程相关警告

## 性能考虑

### CallDeferred 开销

- `CallDeferred` 增加最小开销（单帧延迟）
- 安全收益远远超过性能成本
- 服务初始化在每个 scope 生命周期内只发生一次

### 何时使用异步

**好的使用场景**：
- 从磁盘加载文件
- 网络请求
- 数据库查询
- CPU 密集型计算
- 并行操作

**不好的使用场景**：
- 立即初始化的服务
- 已经在主线程上的操作
- 简单的对象构造

## 从同步迁移到异步

如果你有一个需要变为异步的同步提供者：

```csharp
// 之前（同步）
[Provide(ExposedTypes = [typeof(IDatabase)])]
public IDatabase CreateDatabase()
{
    var db = new DatabaseService();
    db.Connect(); // 阻塞主线程
    return db;
}

// 之后（异步）
[Provide(ExposedTypes = [typeof(IDatabase)])]
public async Task<IDatabase> CreateDatabaseAsync()
{
    var db = new DatabaseService();
    await db.ConnectAsync(); // 不阻塞主线程
    return db;
}
```

不需要其他更改 - 框架自动处理其他所有事情！

## 总结

✅ **GodotSharpDI 通过 CallDeferred 自动确保线程安全**  
✅ **异步提供者可以安全地在后台线程上进行 I/O 和计算**  
✅ **服务结果总是在主线程上提供**  
⚠️ **永远不要直接从后台线程调用 Godot API**  
⚠️ **让框架处理线程编组**

框架使 async/await 与 Godot 的线程模型无缝配合！
