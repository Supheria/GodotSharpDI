# 类型约束总表

本文档总结了 GodotSharp.DI 中所有与类型相关的语义约束。

## 1. 角色类型约束

| 角色 | 必须是 class | 是否 Node | 生命周期标记 | 可作为 Service | 可被注入 | 可暴露类型 |
|------|-------------|-----------|--------------|----------------|----------|------------|
| **Service** | ✅ | ❌ 禁止 | ✅ 必须 | ✅ 是 | ✅ 是 | ✅ 必须 |
| **Host** | ✅ | ✅ 必须 | ❌ 禁止 | ❌ 否 | ❌ 否 | ✅ 通过成员 |
| **User** | ✅ | 可选 | ❌ 禁止 | ❌ 否 | ❌ 否 | ❌ 否 |
| **Scope** | ✅ | ✅ 必须 | ❌ 禁止 | ❌ 否 | ❌ 否 | ❌ 否 |

### Service 约束详情

| 约束 | 要求 | 原因 |
|------|------|------|
| 类型 | class | 需要实例化 |
| 继承 | 不能是 Node | Node 生命周期由 Godot 控制 |
| 修饰符 | 不能是 abstract | 需要实例化 |
| 修饰符 | 不能是 static | 需要实例化 |
| 泛型 | 不能是开放泛型 | 需要具体类型 |
| 声明 | 必须是 partial | 源生成器需要扩展 |

### Host 约束详情

| 约束 | 要求 | 原因 |
|------|------|------|
| 类型 | class | 需要实例化 |
| 继承 | 必须是 Node | 需要场景树生命周期 |
| 声明 | 必须是 partial | 源生成器需要扩展 |

### User 约束详情

| 约束 | 要求 | 原因 |
|------|------|------|
| 类型 | class | 需要实例化 |
| 继承 | Node 或普通 class | 灵活支持两种场景 |
| 声明 | 必须是 partial | 源生成器需要扩展 |

### Scope 约束详情

| 约束 | 要求 | 原因 |
|------|------|------|
| 类型 | class | 需要实例化 |
| 继承 | 必须是 Node | 利用场景树实现层级 |
| 接口 | 必须实现 IScope | 框架识别标志 |
| 特性 | 必须有 [Modules] 或 [AutoModules] | 声明管理的服务 |
| 声明 | 必须是 partial | 源生成器需要扩展 |

---

## 2. 注入类型（Inject Type）约束

可以作为 `[Inject]` 成员类型或 Service 构造函数参数类型的类型。

| 类型 | 是否允许 | 说明 |
|------|----------|------|
| interface | ✅ | **推荐方式** |
| class（普通） | ✅ | 允许但不如接口灵活 |
| Node | ❌ | 生命周期由 Godot 控制 |
| Host | ❌ | Host 不是服务 |
| User | ❌ | User 不是服务 |
| Scope | ❌ | Scope 不是服务 |
| abstract class | ❌ | 无法实例化 |
| static class | ❌ | 无法实例化 |
| 开放泛型 | ❌ | 无法实例化 |
| array | ❌ | 不支持 |
| pointer | ❌ | 不支持 |
| delegate | ❌ | 不支持 |
| dynamic | ❌ | 无法静态分析 |

### 代码示例

```csharp
[User]
public partial class MyComponent : Node
{
    [Inject] private IService _service;           // ✅ 接口
    [Inject] private ConcreteClass _concrete;     // ✅ 普通类（不推荐）
    [Inject] private Node _node;                  // ❌ Node
    [Inject] private MyHost _host;                // ❌ Host 类型
    [Inject] private MyUser _user;                // ❌ User 类型
    [Inject] private MyScope _scope;              // ❌ Scope 类型
    [Inject] private AbstractClass _abstract;    // ❌ 抽象类
}
```

---

## 3. 服务实现类型（Service Type）约束

可以标记为 `[Singleton]` 或 `[Transient]` 的类型。

| 类型 | 是否允许 | 说明 |
|------|----------|------|
| class | ✅ | 必须是 class |
| sealed class | ✅ | 推荐 |
| abstract class | ❌ | 无法实例化 |
| static class | ❌ | 无法实例化 |
| Node | ❌ | 生命周期冲突 |
| Host | ❌ | Host 不是 Service |
| User | ❌ | User 不是 Service |
| Scope | ❌ | Scope 不是 Service |
| interface | ❌ | 不能作为实现类型 |
| 开放泛型 | ❌ | 无法实例化 |
| struct | ❌ | 不支持 |

---

## 4. 暴露类型（Exposed Service Type）约束

可以在 `[Singleton(typeof(...))]` 或 `[Transient(typeof(...))]` 中指定的类型。

| 类型 | 是否允许 | 说明 |
|------|----------|------|
| interface | ✅ | **强烈推荐** |
| concrete class | ✅ | 允许（会产生 Warning） |
| sealed class | ✅ | 允许 |
| abstract class | ❌ | 无意义 |
| Node | ❌ | 不允许 |
| Host/User/Scope | ❌ | 不允许 |
| 开放泛型 | ❌ | 不允许 |

### 最佳实践

```csharp
// ✅ 推荐：暴露接口
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats { }

// ⚠️ 允许但产生 Warning：暴露具体类
[Singleton(typeof(GameConfig))]
public partial class GameConfig
{
    public string GameName { get; set; }
}

// ✅ 暴露多个接口
[Singleton(typeof(IReader), typeof(IWriter))]
public partial class FileService : IReader, IWriter { }
```

---

## 5. User Inject 成员约束

`[User]` 类中使用 `[Inject]` 标记的成员的约束。

| 约束 | 要求 | 诊断代码 |
|------|------|----------|
| 成员类型 | 必须是有效的 Inject Type | GDI_M050 |
| 成员类型 | 不能是 Host 类型 | GDI_M051 |
| 成员类型 | 不能是 User 类型 | GDI_M052 |
| 成员类型 | 不能是 Scope 类型 | GDI_M053 |
| static | 不允许 | GDI_M054 |
| 字段 | 允许（不能是 readonly） | GDI_M020 |
| 属性 | 必须有 setter | GDI_M020 |

### 代码示例

```csharp
[User]
public partial class MyUser : Node
{
    // ✅ 正确
    [Inject] private IService _service;
    [Inject] public IConfig Config { get; set; }
    
    // ❌ 错误
    [Inject] private readonly IService _readonly;     // readonly
    [Inject] public IConfig ReadOnly { get; }         // 无 setter
    [Inject] private static IService _static;         // static
    [Inject] private MyHost _host;                    // Host 类型
}
```

---

## 6. Host Singleton 成员约束

`[Host]` 类中使用 `[Singleton]` 标记的成员的约束。

| 约束 | 要求 | 诊断代码 |
|------|------|----------|
| 成员类型 | 可以是任意类型（包括 Host 自身） | - |
| 成员类型 | 不能是标记为 Service 的类型 | GDI_M060 |
| 暴露类型 | 必须是有效的 Exposed Type | - |
| 暴露类型 | 推荐使用 interface | GDI_M070 (Warning) |
| static | 不允许 | GDI_M055 |
| 字段 | 允许 | - |
| 属性 | 必须有 getter | GDI_M030 |

### 代码示例

```csharp
[Host]
public partial class GameManager : Node, IGameState
{
    // ✅ 正确：暴露自身
    [Singleton(typeof(IGameState))]
    private IGameState Self => this;
    
    // ✅ 正确：暴露持有的普通对象
    [Singleton(typeof(IWorldConfig))]
    private WorldConfig _config = new();
    
    // ❌ 错误：成员类型是 Service
    [Singleton(typeof(IDataService))]
    private DataService _data = new();  // DataService 有 [Singleton] 标记
    
    // ❌ 错误：static 成员
    [Singleton(typeof(IStatic))]
    private static IStatic _static;
}
```

---

## 7. 暴露类型冲突规则

| 情况 | 是否允许 | 诊断代码 |
|------|----------|----------|
| 同一接口由多个 Service 注册 | ❌ | GDI_D050 |
| 同一接口由多个 Host Singleton 注册 | ❌ | GDI_D050 |
| 同一接口同时由 Service 和 Host 注册 | ❌ | GDI_D050 |
| 不同接口由不同 Service/Host 注册 | ✅ | - |

### 代码示例

```csharp
// ❌ 冲突
[Singleton(typeof(IService))]
public partial class ServiceA : IService { }

[Singleton(typeof(IService))]
public partial class ServiceB : IService { }

[Modules(Instantiate = [typeof(ServiceA), typeof(ServiceB)])]
public partial class MyScope : Node, IScope { }
// 错误：IService 被 ServiceA 和 ServiceB 同时提供

// ✅ 正确
[Singleton(typeof(IServiceA))]
public partial class ServiceA : IServiceA { }

[Singleton(typeof(IServiceB))]
public partial class ServiceB : IServiceB { }

[Modules(Instantiate = [typeof(ServiceA), typeof(ServiceB)])]
public partial class MyScope : Node, IScope { }
// 正确：各自提供不同的接口
```

---

## 8. 生命周期依赖规则

| 依赖方 | 被依赖方 | 是否允许 | 诊断代码 |
|--------|----------|----------|----------|
| Singleton | Singleton | ✅ | - |
| Singleton | Transient | ❌ | GDI_D040 |
| Transient | Singleton | ✅ | - |
| Transient | Transient | ✅ | - |

**原因**：Singleton 在 Scope 生命周期内唯一，如果依赖 Transient，语义不清（应该缓存还是每次获取新实例？）。

### 代码示例

```csharp
// ❌ 错误
[Transient(typeof(ITransient))]
public partial class TransientService : ITransient { }

[Singleton(typeof(ISingleton))]
public partial class SingletonService : ISingleton
{
    public SingletonService(ITransient t) { }  // Singleton 依赖 Transient
}

// ✅ 正确：使用工厂模式
[Singleton(typeof(ITransientFactory))]
public partial class TransientFactory : ITransientFactory
{
    public ITransient Create() => new TransientService();
}

[Singleton(typeof(ISingleton))]
public partial class SingletonService : ISingleton
{
    private readonly ITransientFactory _factory;
    
    public SingletonService(ITransientFactory factory)
    {
        _factory = factory;
    }
    
    public void DoWork()
    {
        var transient = _factory.Create();  // 需要时创建
    }
}
```

---

## 9. 构造函数约束

Service 构造函数的约束。

| 约束 | 要求 | 诊断代码 |
|------|------|----------|
| 可见性 | 至少有一个 public 构造函数 | GDI_S020 |
| 多构造函数 | 必须用 [InjectConstructor] 指定 | GDI_S021 |
| 参数类型 | 必须是有效的 Inject Type | GDI_S030 |

### 代码示例

```csharp
// ✅ 正确：单个构造函数
[Singleton(typeof(IService))]
public partial class ServiceA : IService
{
    public ServiceA(IDep1 dep1, IDep2 dep2) { }
}

// ✅ 正确：多构造函数 + InjectConstructor
[Singleton(typeof(IService))]
public partial class ServiceB : IService
{
    [InjectConstructor]
    public ServiceB(IDep1 dep1) { }
    
    public ServiceB(IDep1 dep1, IDep2 dep2) { }
}

// ❌ 错误：多构造函数但未指定
[Singleton(typeof(IService))]
public partial class ServiceC : IService
{
    public ServiceC(IDep1 dep1) { }
    public ServiceC(IDep1 dep1, IDep2 dep2) { }  // 歧义
}

// ❌ 错误：参数类型无效
[Singleton(typeof(IService))]
public partial class ServiceD : IService
{
    public ServiceD(Node node) { }  // Node 不是有效的 Inject Type
}
```

---

## 10. 语义总结

| 概念 | 定义 |
|------|------|
| **Service** | 非 Node 的 class，标记 [Singleton] 或 [Transient]，暴露接口（推荐）或 class |
| **Host** | Node，通过成员上的 [Singleton] 暴露服务，成员值可以是 Host 自身或持有的对象 |
| **User** | Node 或非 Node，通过 [Inject] 成员接收注入，不提供服务 |
| **Scope** | Node，实现 IScope，管理服务生命周期，不可被注入 |
| **Inject Type** | interface 或 class（非 Node/Host/User/Scope/abstract/static） |
| **Exposed Type** | 推荐 interface，允许 concrete class（会产生 Warning） |
| **Singleton** | Scope 内唯一，随 Scope 销毁而释放 |
| **Transient** | 每次请求创建新实例，调用者负责释放 |
