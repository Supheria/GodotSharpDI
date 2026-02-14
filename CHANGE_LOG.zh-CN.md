# v1.1.0

---

> ## 为什么是 1.1.0 而不是 1.0.0？
>
> 在发布 1.0.0-rc.3 后，由 Scope 创建并管理纯逻辑服务，DI 容器逻辑和服务生命周期管理的逻辑杂糅在一起，会导致 Scope 生成的代码过于复杂，并且导致 Scope 的语义和职责范围混乱，同时还造成了一些局限性：
>
> 1. **灵活性受限**：创建服务时难以使用 Node 资源或上下文
> 2. **不支持异步**：仅通过构造函数注入无法处理异步初始化
> 
> 1.1.0 中的新**基于提供者的架构**从根本上解决了这些问题，提供了：
>
> - 服务与 Host 内联定义，更好的内聚性
> - 创建服务时直接访问 Node 资源和上下文
> - 原生的 async/await 支持用于服务初始化
> - 通过 WaitFor 机制实现灵活的依赖排序
> 
> **鉴于这些架构改进和破坏性变更的规模，决定将项目版本增加到 1.1.0，而不是发布具有已知架构限制的 1.0.0。** 
> 
>---

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

// ✅ 新方法（1.1.0）
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

**重要提示**：`WaitFor` **只能等待** `[Inject]` 成员，不能等待 `[Provide]` 成员。

**使用方法**：

```csharp
[Host]
public partial class ServiceHost : Node, IDependenciesResolved
{
    [Inject] private IConfig? _config;
    [Inject] private ILogger? _logger;
    
    // 立即提供（无依赖）
    [Provide(ExposedTypes = [typeof(IMetrics)])]
    public IMetrics CreateMetrics()
    {
        return new MetricsService();
    }
    
    // 等待 _config 注入
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public IDatabase CreateDatabase()
    {
        // WaitFor 保证 _config 解析已尝试，但需检查是否成功
        if (!IsConfigInjectionReady || _config == null)
        {
            GD.PrintErr("Config 未就绪，使用内存数据库");
            return new InMemoryDatabase();
        }
        return new DatabaseService(_config.ConnectionString);
    }
    
    // 等待 _config 和 _logger 注入
    [Provide(ExposedTypes = [typeof(IRepository)], 
             WaitFor = [nameof(_config), nameof(_logger)])]
    public IRepository CreateRepository()
    {
        // 检查多个依赖的就绪状态
        if (!IsAllDependenciesReady)
        {
            GD.PrintErr("部分依赖未就绪");
            return new RepositoryWithDefaults();
        }
        // 所有依赖都就绪
        return new Repository(_config!, _logger!);
    }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady) 
    {
        if (!isAllDependenciesReady)
        {
            GD.PrintErr("部分依赖注入失败");
        }
    }
    
    public override partial void _Notification(int what);
}
```

**特性**：
- **只能**等待 `[Inject]` 成员被注入
- 编译时循环依赖检测
- 支持复杂的依赖链
- 同时支持同步和异步提供者
- 即使依赖失败也会继续（需手动检查 `IsXxxInjectionReady`）

---

### ⚡ 异步服务支持

**1.1.0 新功能**：提供者可以返回 `Task<T>` 进行异步初始化。

**使用方法**：

```csharp
[Host]
public partial class AsyncHost : Node, IDependenciesResolved
{
    [Inject] private IConfig? _config;
    
    // 异步提供服务，等待 _config 注入
    [Provide(ExposedTypes = [typeof(IResourceLoader)], WaitFor = [nameof(_config)])]
    public async Task<IResourceLoader> LoadResourcesAsync()
    {
        if (!IsConfigInjectionReady || _config == null)
        {
            return new ResourceLoader();  // 默认加载器
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
            GD.PrintErr("部分依赖未就绪，某些服务将使用降级版本");
        }
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

5. **Host + User 角色共存**：不再允许
   - **迁移**：Host 现在可以直接使用 `[Inject]`，无需 `[User]` 特性
   - **规则**：Host、User、Scope 三个角色不能共存

### 行为变更

1. **服务注册**：现在通过 Host 提供者进行，而不是类声明
2. **服务构造**：完全由提供者方法控制，而不是构造函数
3. **依赖解析**：使用 WaitFor 机制而不是构造函数参数
4. **角色独占**：一个类只能有一个角色（Host、User 或 Scope）
5. **Host 注入**：Host 可以直接注入依赖，无需额外角色标记

---

## 📝 API 变更

### 新特性

#### `[Provide(ExposedTypes = [...], WaitFor = [...])]`

标记属性或方法为服务提供者。

**参数**：
- `ExposedTypes`（必需）：要暴露的类型数组
- `WaitFor`（可选）：提供前要等待的 `[Inject]` 成员名称数组

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

// 带 WaitFor 的异步提供者（只能等待 Inject 成员）
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

**之后（1.1.0）**：

```csharp
[Modules(Hosts = [typeof(Host1), typeof(Host2)])]
```

---

## 📖 迁移指南（从 1.0.0-rc.3）

### 必需的变更

#### 1. 移除 Singleton 特性

```csharp
// ❌ 旧代码（1.0.0-rc.3）
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; }
}

[Modules(Services = [typeof(PlayerStatsService)])]
public partial class GameScope : Node, IScope { }

// ✅ 新代码（1.1.0）
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

// 服务实现不需要任何特性
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

#### 2. 移除 InjectConstructor 特性

```csharp
// ❌ 旧代码
public class ServiceA
{
    [InjectConstructor]
    public ServiceA(IServiceB serviceB) { }
}

// ✅ 新代码
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

#### 3. 更新 Modules 特性

```csharp
// ❌ 旧代码
[Modules(
    Services = [typeof(Service1), typeof(Service2)],
    Hosts = [typeof(Host1)]
)]

// ✅ 新代码
[Modules(Hosts = [typeof(Host1)])]
```

#### 4. 使用 WaitFor 处理服务依赖

**重要**：WaitFor 只能等待 `[Inject]` 成员。

```csharp
[Host]
public partial class ServiceHost : Node, IDependenciesResolved
{
    [Inject] private IConfig? _config;
    [Inject] private ILogger? _logger;
    
    // Metrics 立即创建（无依赖）
    [Provide(ExposedTypes = [typeof(IMetrics)])]
    public IMetrics CreateMetrics()
    {
        return new MetricsService();
    }
    
    // Database 等待 _config 注入
    [Provide(ExposedTypes = [typeof(IDatabase)], WaitFor = [nameof(_config)])]
    public IDatabase CreateDatabase()
    {
        if (!IsConfigInjectionReady || _config == null)
        {
            return new InMemoryDatabase();
        }
        return new DatabaseService(_config.ConnectionString);
    }
    
    // Repository 等待 _config 和 _logger 注入
    [Provide(ExposedTypes = [typeof(IRepository)], 
             WaitFor = [nameof(_config), nameof(_logger)])]
    public IRepository CreateRepository()
    {
        // 检查依赖是否就绪
        if (!IsAllDependenciesReady)
        {
            return new RepositoryWithDefaults();
        }
        
        // 两个依赖都就绪，注入的服务也可通过 scope 获取
        return new Repository(_config!, _logger!);
    }
    
    public void OnDependenciesResolved(bool isAllDependenciesReady) { }
    public override partial void _Notification(int what);
}
```

#### 5. 移除 Host + User 组合

**1.1.0 变更**：Host、User、Scope 角色不能共存。

```csharp
// ❌ 旧代码（1.0.0-rc.3 中可能有效）
[Host, User]
public partial class GameManager : Node
{
    [Inject] private IConfig _config;
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
}

// ✅ 新代码（1.1.0）
[Host]
public partial class GameManager : Node
{
    // Host 可以直接使用 Inject，无需 User
    [Inject] private IConfig? _config;
    
    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;
    
    public override partial void _Notification(int what);
}
```

### 重大变化总结

| 功能 | 1.0.0-rc.3 | 1.1.0 |
|------|------------|------------|
| 服务注册 | `[Singleton]` 特性 | `[Provide]` 在 Host 成员上 |
| 构造函数注入 | `[InjectConstructor]` | WaitFor + 提供者方法参数 |
| 异步初始化 | 不支持 | `Task<T>` 提供者 |
| 依赖排序 | 构造函数参数 | `WaitFor` 机制 |
| Host + User | 可以组合 | 不能组合（Host 可直接 Inject） |
| WaitFor 目标 | N/A | 只能等待 `[Inject]` 成员 |
| Modules 参数 | `Services` + `Hosts` | 只有 `Hosts` |
| 角色共存 | 部分允许 | 完全独占 |

---

## 已知问题和限制

### WaitFor 限制

1. **只能等待 Inject 成员**：不能等待其他 Provide 成员
2. **解析完成不等于成功**：必须检查 `IsXxxInjectionReady`
3. **循环等待**：在编译时检测并报错

### 异步提供者

1. **取消**：不支持取消令牌

---

## 下一步

- 完善文档和示例
- 收集社区反馈
- 进行性能测试
- 修复发现的任何问题

---

# v1.0.0-rc.3

> ## 关键增强
>
> ### 🎯 注入失败回调
>
> **RC.3 新功能**：现在可以为每个 `[Inject]` 成员添加失败回调处理程序。
>
> **使用方法**：
> ```csharp
> [User]
> public partial class PlayerController : Node
> {
>     [Inject(FailureCallback = true)]
>     private IOptionalService OptionalService { get; set; }
> 
>     // 生成的回调方法（在 partial 类中实现）
>     partial void OnOptionalServiceInjectionFailed(string error)
>     {
>         GD.Print($"可选服务不可用：{error}");
>         // 使用回退逻辑
>     }
> }
> ```
>
> **优势**：
> * 优雅地处理可选依赖
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
