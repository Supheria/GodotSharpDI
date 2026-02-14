# v1.1.0-rc.1

> ## 为什么是 1.1.0 而不是 1.0.0？
>
> 在发布 1.0.0-rc.3 后，我们发现了一个重大的架构局限：`[Singleton]` 特性和独立的服务类虽然功能正常，但在多个方面造成了不必要的复杂性并限制了灵活性：
>
> 1. **紧密耦合**：服务在逻辑上应该创建的地方之外单独声明
> 2. **灵活性受限**：创建服务时难以使用 Node 资源或上下文
> 3. **不支持异步**：仅通过构造函数注入无法处理异步初始化
> 4. **复杂的依赖关系**：通过构造函数管理服务依赖关系不够灵活
>
> 1.1.0 中的新**基于提供者的架构**从根本上解决了这些问题，提供了：
>
> - 服务与 Host 内联定义，更好的内聚性
> - 创建服务时直接访问 Node 资源和上下文
> - 原生的 async/await 支持用于服务初始化
> - 通过 WaitFor 机制实现灵活的依赖排序
> - 更简单的心智模型（减少一个学习概念）
>
> **鉴于这些架构改进和破坏性变更的规模，我们决定增加到 1.1.0，而不是发布具有已知架构限制的 1.0.0。** 这使我们能够以更强大和灵活的基础向前发展。
>
> ---

## 🎯 重大架构变化

### ⚡ 基于提供者的架构

**1.1.0 中移除**：`[Singleton]` 特性和独立的服务类

**替换为**：Host 成员（属性和方法）上的 `[Provide]` 特性

**迁移示例**：

```csharp
// ❌ 旧方法（1.0.0-rc.3）
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

// ✅ 新方法（1.1.0-rc.1）
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

// 服务实现（不需要特性）
public class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; }
    public int Mana { get; set; }
}
```

**优势**：
- 服务定义在它们逻辑上应该在的地方
- 完全访问 Host 的上下文和资源
- 更灵活的服务创建模式
- 更清晰的关注点分离

---

### 🔄 WaitFor 机制

**1.1.0 新功能**：服务可以显式等待依赖项后再被提供。

**使用方法**：

```csharp
[Host]
public partial class ServiceHost : Node, IDependenciesResolved
{
    [Inject] private IConfig _config;
    
    // 立即提供
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger CreateLogger()
    {
        return new Logger();
    }
    
    // 等待 _config 注入
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public IDatabase CreateDatabase()
    {
        return new DatabaseService(_config.ConnectionString);
    }
    
    // 等待 CreateLogger 和 CreateDatabase
    [Provide(ExposedTypes = [typeof(IRepository)], 
             WaitFor = [nameof(CreateLogger), nameof(CreateDatabase)])]
    public IRepository CreateRepository()
    {
        // 所有依赖保证就绪
        return new Repository();
    }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady) { }
    public override partial void _Notification(int what);
}
```

**特性**：
- 等待 `[Inject]` 成员被注入
- 等待其他 `[Provide]` 成员完成
- 编译时循环依赖检测
- 支持复杂的依赖链
- 同时支持同步和异步提供者

---

### ⚡ 异步服务支持

**1.1.0 新功能**：提供者可以返回 `Task<T>` 进行异步初始化。

**使用方法**：

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
    
    // 可以等待异步提供者
    [Provide(ExposedTypes = [typeof(IGameSession)], 
             WaitFor = [nameof(LoadResourcesAsync), nameof(ConnectAsync)])]
    public IGameSession CreateSession()
    {
        // 资源和网络已就绪
        return new GameSession();
    }
    
    public override partial void _Notification(int what);
}
```

**优势**：
- 自然的 async/await 语法
- 更好地控制初始化顺序
- 使用 try/catch 进行适当的错误处理
- 与 WaitFor 机制无缝集成

---

## 🔨 破坏性变更

### 移除的功能

1. **`[Singleton]` 特性**：完全移除
   - **迁移**：改用 Host 成员上的 `[Provide]`

2. **`[InjectConstructor]` 特性**：不再需要
   - **迁移**：在提供者方法中控制构造

3. **`[Modules]` 中的 `Services` 参数**：已移除
   - **迁移**：移除此参数；只需要 `Hosts = [...]`

4. **独立服务类**：不再是一个概念
   - **迁移**：将服务创建逻辑移至 Host 提供者

### 行为变更

1. **服务注册**：现在通过 Host 提供者进行，而不是类声明
2. **服务构造**：完全由提供者方法控制，而不是构造函数
3. **依赖解析**：使用 WaitFor 机制而不是构造函数参数

---

## 📝 API 变更

### 新特性

#### `[Provide(ExposedTypes = [...], WaitFor = [...])]`

标记属性或方法为服务提供者。

**参数**：
- `ExposedTypes`（必需）：要暴露的类型数组
- `WaitFor`（可选）：提供前要等待的成员名称数组

**可应用于**：
- 属性（用于简单的服务提供）
- 方法（用于复杂的服务创建）
- 异步方法（用于异步初始化）

```csharp
// 属性提供者
[Provide(ExposedTypes = [typeof(IConfig)])]
public IConfig Config => new ConfigService();

// 方法提供者
[Provide(ExposedTypes = [typeof(IDatabase)])]
public IDatabase CreateDatabase() => new DatabaseService();

// 带 WaitFor 的异步提供者
[Provide(ExposedTypes = [typeof(IRepository)], WaitFor = [nameof(_config)])]
public async Task<IRepository> InitializeRepositoryAsync()
{
    var repo = new Repository(_config);
    await repo.ConnectAsync();
    return repo;
}
```

### 修改的特性

#### `[Modules(Hosts = [...])]`

简化为只接受 Hosts。

**之前（1.0.0-rc.3）**：
```csharp
[Modules(
    Services = [typeof(Service1), typeof(Service2)],
    Hosts = [typeof(Host1), typeof(Host2)]
)]
```

**之后（1.1.0-rc.1）**：
```csharp
[Modules(Hosts = [typeof(Host1), typeof(Host2)])]
```

---

## 💡 新功能

### 1. 基于属性的服务提供

简单的服务可以通过属性提供：

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

### 2. 带上下文的基于方法的服务提供

复杂的服务可以访问 Host 上下文：

```csharp
[Host]
public partial class GameHost : Node, IDependenciesResolved
{
    [Inject] private IConfig _config;
    
    private PlayerData _playerData;
    
    [Provide(ExposedTypes = [typeof(IPlayerStats)], WaitFor = [nameof(_config)])]
    public IPlayerStats CreatePlayerStats()
    {
        // 可以访问 Host 的状态和注入的依赖
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

### 3. 异步服务初始化

具有异步初始化要求的服务：

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

### 4. 复杂的依赖链

显式控制服务初始化顺序：

```csharp
[Host]
public partial class ComplexHost : Node, IDependenciesResolved
{
    [Inject] private IConfig _config;
    
    // 第 1 层：核心服务
    [Provide(ExposedTypes = [typeof(ILogger)])]
    public ILogger CreateLogger() => new Logger();
    
    // 第 2 层：依赖第 1 层
    [Provide(ExposedTypes = [typeof(IDatabase)], 
             WaitFor = [nameof(CreateLogger), nameof(_config)])]
    public async Task<IDatabase> ConnectDatabaseAsync()
    {
        var db = new DatabaseService(_config.ConnectionString);
        await db.ConnectAsync();
        return db;
    }
    
    // 第 3 层：依赖第 2 层
    [Provide(ExposedTypes = [typeof(IRepository)], 
             WaitFor = [nameof(ConnectDatabaseAsync)])]
    public IRepository CreateRepository()
    {
        return new Repository(/* database 已就绪 */);
    }
    
    // 第 4 层：依赖多个前面的层
    [Provide(ExposedTypes = [typeof(IDataService)], 
             WaitFor = [nameof(CreateLogger), nameof(CreateRepository)])]
    public IDataService CreateDataService()
    {
        return new DataService(/* logger 和 repository 已就绪 */);
    }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady) { }
    public override partial void _Notification(int what);
}
```

---

## 🔍 增强的诊断

### 新错误代码

| 代码 | 类别 | 描述 |
|------|------|------|
| GDI_M100 | Provide | [Provide] 成员必须至少有一个暴露类型 |
| GDI_M101 | Provide | [Provide] 暴露类型未由返回类型实现 |
| GDI_M102 | Provide | [Provide] WaitFor 目标未找到 |
| GDI_M103 | Provide | [Provide] WaitFor 目标无效 |
| GDI_D200 | WaitFor | 检测到循环 WaitFor 依赖 |
| GDI_D201 | WaitFor | WaitFor 链验证失败 |

### 改进的错误消息

所有诊断现在包括：
- 问题的清晰说明
- 为什么有问题
- 建议的修复
- 适用时的代码示例

**示例**：
```
错误 GDI_D200：检测到循环 WaitFor 依赖
  
  CreateA（等待）→ CreateB
  CreateB（等待）→ CreateC
  CreateC（等待）→ CreateA
  
  建议：通过移除其中一个 WaitFor 依赖来打破循环依赖，
  或重构为使用基于事件的通信。
```

---

## 🔧 内部改进

### 代码生成

1. **重构的生成管道**：
   - 分离的依赖注入阶段
   - 专用的服务提供阶段
   - WaitFor 依赖解析阶段

2. **更好的性能**：
   - 优化的服务查找
   - 减少生成的代码大小
   - 更快的编译时间

3. **更清晰的生成代码**：
   - 更可读的输出
   - 更好的注释
   - 一致的格式

### 测试

1. **新测试套件**：
   - WaitFor 循环依赖测试
   - WaitFor 验证测试
   - 异步提供者测试

2. **增强的覆盖率**：
   - 提供者注册场景
   - 复杂的依赖链
   - 错误条件

---

## 📖 迁移指南

### 步骤 1：将服务类转换为提供者方法

**之前**：
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

**之后**：
```csharp
// 在 Host 类中：
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

// 服务实现（无需特性）：
public class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; }
    public int Mana { get; set; }
}
```

### 步骤 2：更新 Scope 定义

**之前**：
```csharp
[Modules(
    Services = [typeof(PlayerStatsService), typeof(CombatService)],
    Hosts = [typeof(GameManager)]
)]
public partial class GameScope : Node, IScope { }
```

**之后**：
```csharp
[Modules(Hosts = [typeof(GameManager)])]
public partial class GameScope : Node, IScope
{
    public override partial void _Notification(int what);
}
```

### 步骤 3：处理服务依赖

**之前**：
```csharp
[Singleton(typeof(IRepository))]
public partial class Repository : IRepository
{
    [InjectConstructor]
    public Repository(IDatabase database, ILogger logger)
    {
        // 构造函数注入
    }
}
```

**之后**：
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
        // 依赖保证就绪
        return new Repository();
    }
    
    public override partial void _Notification(int what);
}
```

### 步骤 4：在需要时添加异步支持

如果服务需要异步初始化：

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

## 🎓 最佳实践

### 1. 组织相关提供者

在 Host 中逻辑地组织提供者：

```csharp
[Host]
public partial class DataServicesHost : Node
{
    // 所有数据相关的服务在一个地方
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

### 2. 使用 WaitFor 明确依赖

使依赖显式：

```csharp
// ✅ 清晰明确
[Provide(ExposedTypes = [typeof(IService)], 
         WaitFor = [nameof(_config), nameof(CreateLogger)])]
public IService CreateService()
{
    // 依赖保证就绪
}

// ❌ 隐式且容易出错
[Provide(ExposedTypes = [typeof(IService)])]
public IService CreateService()
{
    // 希望依赖就绪？
}
```

### 3. I/O 操作优先使用异步

```csharp
// ✅ 网络/文件操作使用异步
[Provide(ExposedTypes = [typeof(IDatabase)])]
public async Task<IDatabase> ConnectAsync()
{
    var db = new DatabaseService();
    await db.ConnectAsync();
    return db;
}

// ❌ 在提供者中阻塞
[Provide(ExposedTypes = [typeof(IDatabase)])]
public IDatabase Connect()
{
    var db = new DatabaseService();
    db.ConnectAsync().Wait(); // 阻塞！
    return db;
}
```

### 4. 保持提供者简单

```csharp
// ✅ 简单专注
[Provide(ExposedTypes = [typeof(IConfig)])]
public IConfig CreateConfig()
{
    return ConfigService.LoadFromFile("config.json");
}

// ❌ 提供者中逻辑过多
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

## 🔮 展望未来

此版本为未来的增强奠定了坚实的基础：

- **未来考虑**：服务生命周期作用域（Transient、Scoped、Singleton）
- **未来考虑**：延迟服务初始化
- **未来考虑**：服务装饰器
- **未来考虑**：同类型的多个服务实例

我们相信基于提供者的架构为这些未来功能提供了所需的灵活性，同时保持了常见用例的简单性。

---

## 📝 总结

v1.1.0-rc.1 代表了重大的架构演进：

**✅ 新架构**：
- 使用 `[Provide]` 的基于提供者的服务定义
- 用于依赖排序的 WaitFor 机制
- 原生 async/await 支持
- 简化的概念模型

**⚠️ 破坏性变更**：
- 移除 `[Singleton]` 特性
- 移除 `[InjectConstructor]` 特性
- 从 `[Modules]` 移除 `Services` 参数
- 不再使用独立服务类

**🚀 优势**：
- 服务创建更大的灵活性
- 与 Node 资源更好的集成
- 更清晰的关注点分离
- 更强大的依赖管理
- 面向未来的架构

---

**迁移工作量**：中等。大多数项目可以通过遵循迁移指南在几个小时内完成迁移。

**我们建议所有新项目使用此版本，并鼓励现有项目在方便时进行迁移。**

---


# v1.0.0-rc.3

> ## 主要新功能
>
> ### ✨ 注入失败回调机制
>
> **RC.3 新功能**：单个注入成员现在可以拥有失败回调以进行细粒度错误处理。
>
> **使用方法**：
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
> GD.PrintErr($"GameManager 注入失败：{error}");
> // 实现回退逻辑
> }
> }
> ```
>
> **优势**：
> * 按依赖项而不是全局处理注入失败
> * 为可选依赖实现回退逻辑
> * 更好的错误处理和用户体验
>
> ---
>
> ### 🎯 注入就绪指示器
>
> **RC.3 新功能**：每个 `[Inject]` 成员现在生成一个对应的 `IsXxxInjectionReady` 布尔指示器。
>
> **使用方法**：
> ```csharp
> [User]
> public partial class PlayerUI : Control
> {
>     [Inject]
>     private IGameManager GameManager { get; set; }
> 
>     public void Update()
>     {
>         // 在运行时检查依赖是否就绪
>         if (IsGameManagerInjectionReady)
>         {
>             GameManager.DoSomething();
>         }
>     }
> }
> ```
>
> **优势**：
> * 运行时检查依赖可用性
> * 处理可选依赖时更安全的代码
> * 基于注入状态更好的控制流
>
> ---
>
> ### 🔄 接口重命名：IServicesReady → IDependenciesResolved
>
> **破坏性变更**：接口已重命名以更好地反映其目的，并更新了方法签名。
>
> **之前（RC.2）**：
>
> ```csharp
> public interface IServicesReady
> {
>     void OnServicesReady();
> }
> ```
>
> **之后（RC.3）**：
> ```csharp
> public interface IDependenciesResolved
> {
>     void OnDependenciesResolved(bool isAllDependenciesReady);
> }
> ```
>
> **需要迁移**：
> * 将 `IServicesReady` 替换为 `IDependenciesResolved`
> * 更新方法签名以接受 `isAllDependenciesReady` 参数
> * 添加逻辑以检查参数并处理部分失败
>
> **迁移示例**：
> ```csharp
> // 旧代码（RC.2）
> [User]
> public partial class PlayerUI : Control, IServicesReady
> {
>     public void OnServicesReady()
>     {
>         Initialize();
>     }
> }
> 
> // 新代码（RC.3）
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
>             GD.PrintErr("部分依赖注入失败");
>         }
>     }
> }
> ```
>
> ---
>
> ## 增强的类型约束
>
> ### 🚫 泛型类型约束
>
> **RC.3 新功能**：所有 DI 角色（Service、Host、User、Scope）都不能是泛型类型。
>
> **理由**：
> * 泛型类型在没有类型参数的情况下无法实例化
> * 泛型类型不能作为稳定的服务标识符
> * 类型安全和依赖图构造需要具体类型
>
> **错误消息**：
>
> * Service："泛型类型不能用作服务实现"
> * Host："泛型类型不能标记为 [Host]"
> * User："泛型类型不能标记为 [User]"
> * Scope："泛型类型不能标记为 [Scope]"
>
> **解决方法**：
> 如果需要使用泛型类型，创建一个继承自泛型类型的具体类：
> ```csharp
> // ❌ 不允许
> [Singleton(typeof(IRepository<Player>))]
> public partial class Repository<T> : IRepository<T> { }
> 
> // ✅ 正确方法
> public interface IPlayerRepository : IRepository<Player> { }
> 
> [Singleton(typeof(IPlayerRepository))]
> public partial class PlayerRepository : Repository<Player>, IPlayerRepository { }
> ```
>
> ---
>
> ## 改进的错误诊断
>
> ### 📊 完整的依赖链显示
>
> **RC.3 增强**：依赖解析失败时，错误消息现在显示完整的依赖链。
>
> **错误消息示例**：
> ```
> 错误：依赖链解析失败：
>   PlayerController（User）
>   → ICombatSystem（Service）
>   → IWeaponFactory（Service）
>   → IResourceLoader（缺失）
> ```
>
> **优势**：
> * 快速识别哪个服务缺失
> * 了解依赖失败的完整上下文
> * 更容易调试复杂的依赖图
>
> ---
>
> ### 🔍 运行时循环依赖检测
>
> **RC.3 优化**：循环依赖检测现在仅在 DEBUG 构建中运行以获得更好的性能。
>
> **检测范围**：
> * 仅检查 Service → Service 构造函数依赖
> * 不标记 User `[Inject]` 成员（它们在构造后解析）
> * 不标记 Host `[Singleton]` 成员
> * 不标记 Host+User 自注入模式
>
> **为什么重要**：
> Host+User 自注入不是循环依赖，因为：
> 1. Host 注册不触发注入
> 2. 服务构造首先完成
> 3. 用户注入随后发生
> 4. 不形成构造函数循环
>
> ---
>
> ### 📝 更清晰的错误消息
>
> **RC.3 改进**：所有错误消息现在包括：
> * 出了什么问题
> * 为什么有问题
> * 适用时的建议修复
> * 完整的依赖链上下文
>
> ---
>
> ## 代码生成改进
>
> ### 🏭 服务工厂优化
>
> **RC.3 变更**：`ServiceFactories` 现在是一个静态集合，以获得更好的内存效率。
>
> **影响**：
> * 减少内存占用
> * 更快的服务工厂查找
> * 在大型依赖图中更好的性能
>
> ---
>
> ### 🏭 服务创建或提供失败也会触发回调
>
> **RC.3 变更**：服务创建失败现在写入服务缓存并触发失败回调。
>
> **影响**：
>
> - 更好的错误传播
> - 防止等待队列挂起在已经明确失败的服务上
> - 更清晰的错误消息
>
> ---
>
> ### 📁 增强的文件命名
>
> **RC.3 改进**：生成的文件现在使用 `Namespace+MetaName` 格式以获得更好的组织。
>
> **示例**：
> * 之前：`PlayerController.DI.g.cs`
> * 之后：`MyGame.Player.PlayerController.DI.g.cs`
>
> **优势**：
> * 避免大型项目中的命名冲突
> * 解决方案资源管理器中更好的文件组织
> * 更容易定位生成的文件
>
> ---
>
> ## 内部错误处理与鲁棒性
>
> ### 🛡️ 全面的异常处理
>
> **RC.3 新功能**：源生成器、分析器和代码修复提供程序现在具有强大的异常处理以确保稳定性。
>
> **改进**：
>
> #### 源生成器
> - **分层异常处理**：代码生成的每个阶段都有独立的错误处理
> - **详细诊断**：新的内部错误诊断（GDI_E001-E101）提供清晰的错误消息
> - **优雅降级**：一个类中的失败不会阻止其他类的生成
> - **用户友好的消息**：错误消息解释失败的原因以及如何修复
>
> **新错误代码**：
> - `GDI_E001`：生成器初始化失败
> - `GDI_E010`：类分析失败
> - `GDI_E011`：符号缓存不可用
> - `GDI_E012`：类验证失败
> - `GDI_E020`：依赖图构建失败
> - `GDI_E021`：图构建阶段失败
> - `GDI_E030`：服务提供者注册失败
> - `GDI_E040`：节点构建失败
> - `GDI_E050`：依赖图验证失败
> - `GDI_E100`：代码生成失败
> - `GDI_E101`：源输出失败
>
> #### 分析器
> - **静默失败**：分析器异常不再崩溃编译
> - **受保护的分析**：每个语法节点独立分析并具有异常保护
> - **取消支持**：正确处理 `OperationCanceledException`
> - **保守方法**：有疑问时，跳过报告而不是崩溃
>
> **受影响的分析器**：
>
> - `GeneratedMemberAccessAnalyzer`：检测对生成成员的手动访问
> - `InjectionFailureCallbackAnalyzer`：检测缺失的失败回调实现
>
> #### 代码修复提供程序
> - **稳定的 IDE 体验**：代码修复失败不再崩溃快速修复菜单
> - **回退机制**：当复杂生成失败时简化代码生成
> - **安全解析**：字符串提取和方法生成受到边缘情况保护
> - **返回原始文档**：失败的修复返回未更改的原始文档
>
> **受影响的提供程序**：
> - `NotificationMethodCodeFixProvider`：添加缺失的 `_Notification` 方法
> - `InjectionFailureCallbackCodeFixProvider`：实现缺失的失败回调
>
> ---
>
> ## 迁移指南
>
> ### 必需的变更
>
> 1. **更新接口实现**：
>   ```csharp
>    // 替换这个
>   public partial class MyClass : Node, IServicesReady
>   {
>       public void OnServicesReady() { }
>   }
> 
>   // 使用这个
>    public partial class MyClass : Node, IDependenciesResolved
>    {
>      public void OnDependenciesResolved(bool isAllDependenciesReady)
>        {
>          if (isAllDependenciesReady)
>            {
>               // 您的初始化代码
>            }
>        }
>    }
>   ```
>
>    2. **检查泛型类型**：
>         * 从任何 Service、Host、User 或 Scope 类中移除泛型类型参数
>    * 如果需要，创建具体的包装类
>    3. **可选：添加失败回调**：
>
>
>    ```csharp
>    [Inject(FailureCallback = true)]
>    private IOptionalService Service { get; set; }
>    
>    partial void OnServiceInjectionFailed(string error)
>    {
>        // 处理失败
>    }
>    ```
>
>
> ---
>
> ## 总结
>
> v1.0.0-rc.3 带来了对错误处理和诊断的重大改进：
>
> ✅ **新功能**：
>    - 用于细粒度错误处理的注入失败回调
>       - 用于运行时检查的注入就绪指示器
> - 带有完整依赖链的更好错误诊断
>
> ⚠️ **破坏性变更**：
> - `IServicesReady` → `IDependenciesResolved`（需要迁移）
> - DI 角色中不再允许泛型类型
>
> 🚀 **性能**：
> - 静态服务工厂集合
> - 仅在 DEBUG 中运行时循环依赖检测
>
> ---
>
> 在进一步完善和打磨整体项目代码后，下一个版本将是 1.0 发布！🎉


# v1.0.0-rc.2

> ## 关键修复
>
> ### ✅ 修复了 `OnServicesReady()` 时序问题
>
> **RC.1 中的问题**：`OnServicesReady()` 可能在 `_Ready()` 之前调用，破坏了节点就绪时所有依赖都可用的保证。
>
> **RC.2 中修复**：
>
> * `OnServicesReady()` 现在保证在 `_Ready()` 之后调用
> * 在回调执行前依赖完全解析
> * 与 Godot 生命周期正确集成
>
> ---
>
> ## 增强的类型验证
>
> ### 新增诊断
>
> * 注入成员不能是常规 Node（错误）
> * 注入成员类型应该是接口（警告）
> 
> * 单例成员类型无效（错误）
> * 单例成员是 Host 类型（警告）
> * 单例成员不能是 User 类型（错误）
> * 单例成员不能是 Scope/常规 Node（错误）
> * 单例成员暴露类型未实现（错误）
> * 单例成员暴露类型应该是接口（警告）
> 
> * 构造函数参数是 Host 类型（警告）
> * 构造函数参数不能是 User 类型（错误）
> * 构造函数参数不能是 Scope 类型（错误）
> * 构造函数参数不能是常规 Node（错误）
> * 构造函数参数应该是接口（警告）
> 
> * 注入成员类型未被任何服务暴露（错误）
> 
> ---
> 
> ## 改进的错误消息
> 
> 所有诊断消息现在提供：
> * 出了什么问题的清晰说明
> * 为什么有问题
> * 适用时的建议修复
> ```csharp
> // 之前（RC.1）：
> // 错误：[Inject] 成员 'IGameState _state' 具有无效类型
> 
> // 之后（RC.2）：
> // 警告 GDI_M041：[Inject] 成员 '_manager' 具有类型 'GameManager'，
> // 这是一个 [Host] 类型。虽然允许，但不建议直接注入 Host 类型
> // - 考虑注入 Host 暴露的接口
> ```
> 
> ---
> 
> ## 资源组织
> 
> ### 标准化资源命名
> 
> 所有诊断消息现在使用带前缀的资源名称：
> * `C_*` - 类级诊断
> * `M_*` - 成员级诊断
> * `S_*` - 构造函数级诊断
> * `D_*` - 依赖图诊断
> * `E_*` - 内部错误诊断
> * `U_*` - 用户行为诊断
> 
> ---
>
> 几乎已经可以投入生产，期待稳定的 1.0 发布！🚀
