- # 📘 GodotSharp.DI

  **A developer‑friendly dependency injection framework for Godot C#**

  GodotSharp.DI 让 Godot C# 拥有真正的依赖注入体验： 无需反射、无需运行时扫描、无需手写容器，所有内容都由 **Source Generator** 自动生成。

  它的目标是：

  - **简单易用**
  - **高性能（零反射）**
  - **强静态分析（编译期错误）**
  - **与 Godot 生命周期完美融合**
  - **适合游戏开发者**

  # 📑 目录

  1. Why GodotSharp.DI?
  2. QuickStart
  3. How it works
  4. Roles: Host / User / Service / Scope
  5. Lifecycle Model
  6. Thread Safety
  7. Code Generation
  8. Diagnostics
  9. Examples
  10. Roadmap / TODO

  # 1. **Why GodotSharp.DI?**

  Godot C# 缺少一个真正适合游戏开发的 DI 框架。 常见问题包括：

  - 反射太慢
  - 生命周期难以管理
  - Node 之间依赖混乱
  - 服务初始化顺序不可控
  - 多 Scope 难以实现
  - 线程安全问题难以排查

  GodotSharp.DI 解决了这些问题：

  - **零反射**（全部编译期生成）
  - **强语义角色系统**（Host / User / Service / Scope）
  - **自动注入**（绑定 Godot 生命周期）
  - **自动服务注册**
  - **自动依赖图验证**
  - **自动生成代码**
  - **自动线程安全检查（Debug 模式）**

  # 2. **QuickStart**

  ### 1. 定义 Service

  csharp

  ```
  [Singleton(typeof(IConfig))]
  public partial class ConfigService : IConfig { }
  ```

  ### 2. 定义 Host（必须是 Node）

  csharp

  ```
  [Host]
  public partial class GameHost : Node
  {
      [Singleton(typeof(IConfig))]
      private ConfigService Config { get; } = new();
  }
  ```

  ### 3. 定义 Scope（必须是 Node）

  csharp

  ```
  [Modules(Instantiate = [typeof(ConfigService)], Expect = [typeof(GameHost)])]
  public partial class GameScope : Node, IScope { }
  ```

  ### 4. 定义 User

  csharp

  ```
  [User]
  public partial class PlayerUI : Control, IServicesReady
  {
      [Inject] private IConfig _config;
  
      public void OnServicesReady()
      {
          GD.Print(_config.SomeValue);
      }
  }
  ```

  # 3. **How it works**

  GodotSharp.DI 使用 Source Generator 自动生成：

  - Service 构造函数工厂
  - Host Attach/Unattach
  - User 注入逻辑
  - Scope 生命周期
  - 成员级递归注入
  - 依赖图验证
  - 线程安全断言（Debug）

  你写的只是标记（Attributes）， 框架会自动生成所有 DI 代码。

  # 4. **Roles: Host / User / Service / Scope**

  ## 🟥 Service

  - 由 Host 注册
  - 构造函数注入
  - 必须是非 Node
  - 生命周期由 Scope 管理

  ## 🟦 User

  - 消费服务
  - 字段/属性注入
  - 可以是 Node 或非 Node
  - 注入由宿主 Node 自动触发
  - 不影响 Scope

  ### 非 Node User 注入机制

  ```mermaid
  flowchart TD
      A[Node.EnterTree] --> B[AttachToScope]
      B --> C[ResolveUserDependencies]
      C --> D[Attach Member Users]
  ```

  ## 🟥 Host

  - **必须是 Node**
  - 注册服务
  - 生命周期绑定 EnterTree / ExitTree
  - 不允许作为成员嵌套
  - 不允许构造函数注入

  ## 🟩 Host + User

  - 必须是 Node
  - 先注册服务，再注入依赖
  - OnServicesReady 在依赖全部就绪后触发

  ## 🟧 Scope

  - 必须是 Node
  - 管理服务生命周期
  - 构造 Service
  - 注入 User
  - 注册 Host

  # 5. **Lifecycle Model**

  mermaid

  ```
  flowchart TD
      A[Node.EnterTree] --> B[AttachHostServices]
      B --> C[ResolveUserDependencies]
      C --> D[OnServicesReady]
      D --> E[Node.Ready]
      E --> F[Node.ExitTree]
      F --> G[UnattachHostServices]
  ```

  # 6. **Thread Safety**

  GodotSharp.DI 是 **主线程 DI 框架**。

  ### ❌ 以下方法绝不能在后台线程调用：

  - ResolveDependency
  - RegisterService / UnregisterService
  - GetService
  - InstantiateScopeSingletons
  - DisposeScopeSingletons
  - ResolveUserDependencies
  - CreateService
  - OnDependencyResolved
  - OnServicesReady

  ### ✔ 正确模式

  csharp

  ```
  Task.Run(() =>
  {
      var data = ProcessData();
      CallDeferred(nameof(RegisterServiceOnMainThread), data);
  });
  ```

  # 7. **Code Generation**

  生成器自动生成：

  - Service 构造函数工厂
  - Host Attach/Unattach
  - User 注入逻辑
  - Scope 生命周期
  - 成员级递归 Attach/Unattach
  - Debug 信息（可选）

  生成器流程：

  代码

  ```
  ClassTypeValidator → TypeInfo → DiGraph → Generators
  ```

  # 8. **Diagnostics**

  ## ❌ 禁止手动注入

  ### **GDI-U-004：禁止手动调用 AttachToScope()**

  代码

  ```
  禁止手动调用 AttachToScope。注入应由宿主 Node 的生命周期自动触发。
  ```

  ### **GDI-U-005：禁止手动调用 ResolveUserDependencies()**

  代码

  ```
  禁止手动调用 ResolveUserDependencies。依赖注入必须由框架自动执行。
  ```

  # 9. **Examples**

  ## Host + User

  csharp

  ```
  [Host, User]
  public partial class GameManager : Node
  {
      [Inject] private IConfig _config;
  
      public IGameState CreateGameState() => new GameState();
  
      public void OnServicesReady()
      {
          GD.Print("GameManager ready");
      }
  }
  ```

  # 10.**Rules Table**

  | 角色      | Inject | InjectConstructor | Singleton | Transient | Host | User | Modules | 非 Node | Node |
  | --------- | ------ | ----------------- | --------- | --------- | ---- | ---- | ------- | ------- | ---- |
  | Service   | ❌      | ✔                 | ✔         | ✔         | ❌    | ❌    | ❌       | ✔       | ❌    |
  | User      | ✔      | ❌                 | ❌         | ❌         | ❌    | ✔    | ❌       | ✔       | ✔    |
  | Host      | ❌      | ❌                 | ✔(成员)   | ❌         | ✔    | ❌    | ❌       | ❌       | ✔    |
  | Host+User | ✔      | ❌                 | ✔(成员)   | ❌         | ✔    | ✔    | ❌       | ❌       | ✔    |
  | Scope     | ❌      | ❌                 | ❌         | ❌         | ❌    | ❌    | ✔       | ❌       | ✔    |

  # 11. **Roadmap / TODO**

  - 文档完善
  - 多语言支持（.resx）
  - 完整 Diagnostics.md
  - 性能优化
  - Scope 继承
  - Debug 调试工具
  - 示例项目
  - ECS 集成示例
