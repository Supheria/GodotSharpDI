# GodotSharp.DI

一个专为 Godot 引擎设计的编译时依赖注入框架，通过 C# Source Generator 实现零反射、高性能的 DI 支持。

## 🎯 设计理念

GodotSharp.DI 的核心设计理念是**将 Godot 的场景树生命周期与传统 DI 容器模式融合**：

- **场景树即容器层级**：利用 Godot 的场景树结构实现作用域（Scope）层级
- **Node 生命周期集成**：服务的创建和销毁与 Node 的进入/退出场景树事件绑定
- **编译时安全**：通过 Source Generator 在编译期完成依赖分析和代码生成，提供完整的编译时错误检查

## 📦 安装

```xml
<PackageReference Include="GodotSharp.DI" Version="x.x.x" />
<PackageReference Include="GodotSharp.DI.Generator" Version="x.x.x" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## 🚀 快速开始

### 1. 定义服务

```csharp
// 定义服务接口
public interface IPlayerStats
{
    int Health { get; set; }
    int Mana { get; set; }
}

// 实现服务（Singleton 生命周期）
[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;
}
```

### 2. 定义 Scope（DI 容器）

```csharp
[Modules(
    Services = [typeof(PlayerStatsService)],
    Hosts = [typeof(GameManager)]
)]
public partial class GameScope : Node, IScope
{
    // 框架自动生成 IScope 实现
}
```

### 3. 定义 Host（服务提供者）

```csharp
[Host]
public partial class GameManager : Node, IGameState
{
    // 将自己暴露为 IGameState 服务
    [Singleton(typeof(IGameState))]
    private IGameState Self => this;
    
    public GameState CurrentState { get; set; }
}
```

### 4. 定义 User（服务消费者）

```csharp
[User]
public partial class PlayerUI : Control, IServicesReady
{
    [Inject] private IPlayerStats _stats;
    [Inject] private IGameState _gameState;
    
    // 所有依赖注入完成后调用
    public void OnServicesReady()
    {
        UpdateUI();
    }
}
```

### 5. 场景树结构

```
GameScope (IScope)
├── GameManager (Host)
├── Player
│   └── PlayerUI (User) ← 自动接收注入
└── Enemies
```

## 📚 核心概念

### 四种角色类型

| 角色        | 说明                                         | 约束                     |
| ----------- | -------------------------------------------- | ------------------------ |
| **Service** | 纯逻辑服务，由 Scope 创建和管理              | 必须是非 Node 的 class   |
| **Host**    | 场景级资源提供者，将 Node 资源桥接到 DI 世界 | 必须是 Node              |
| **User**    | 依赖消费者，接收注入                         | Node 或普通 class        |
| **Scope**   | DI 容器，管理服务生命周期                    | 必须是 Node，实现 IScope |

### 服务生命周期

| 生命周期      | 说明                              | 使用场景             |
| ------------- | --------------------------------- | -------------------- |
| **Singleton** | 在 Scope 内唯一，Scope 销毁时释放 | 状态管理、配置、缓存 |
| **Transient** | 每次请求创建新实例                | 无状态服务、工厂产品 |

## 📖 详细文档

- [角色详解](https://claude.ai/chat/docs/ROLES.md) - 四种角色的详细说明和使用指南
- [生命周期管理](https://claude.ai/chat/docs/LIFECYCLE.md) - 服务生命周期和 Scope 层级
- [最佳实践](https://claude.ai/chat/docs/BEST_PRACTICES.md) - 推荐的使用模式和常见陷阱
- [API 参考](https://claude.ai/chat/docs/API.md) - 完整的 API 文档
- [诊断代码](https://claude.ai/chat/docs/DIAGNOSTICS.md) - 编译时错误和警告说明
- [类型约束总表](https://claude.ai/chat/docs/TYPE_CONSTRAINTS.md) - 完整的类型约束规则

## ⚠️ 重要注意事项

### 非 Node User 的使用

非 Node 类型的 User 需要手动触发依赖解析：

```csharp
[User]
public partial class MyService  // 非 Node
{
    [Inject] private IConfig _config;
    
    // 调用者需要提供 Scope 并手动调用
    // myService.ResolveDependencies(scope);
}
```

### Transient 服务的生命周期

Transient 服务的实例不由 Scope 跟踪，**调用者负责释放**：

```csharp
// 如果 Transient 服务实现了 IDisposable
// 调用者需要自行管理释放
```

## 🔧 编译时诊断

框架提供完整的编译时错误检查：

```
GDI_C001: 类型不能同时标记为 [Singleton] 和 [Transient]
GDI_M050: [Inject] 成员类型无效
GDI_D020: 检测到循环依赖
...
```

完整诊断代码列表请参阅 [诊断文档](https://claude.ai/chat/docs/DIAGNOSTICS.md)。

## 📄 许可证

MIT License
