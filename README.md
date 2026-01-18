# GodotSharp.DI

## **What is GodotSharp.DI?**

- Godot C# 原生 DI 框架
- 支持构造函数注入 / Node 注入 / 多 Scope

## **Core concepts**

- Service（`[Singleton]` / `[Transient]`）
- Host（`[Host]`）
- User（`[User]` + `[Inject]`）
- Scope（`IScope` + `[Modules]` / `[AutoModules]`）

## **Godot integration**

- Scope 作为 Node
- Host / User 作为 Node
- 生命周期：EnterTree / Ready / ExitTree / Predelete

## **Advanced**

- 构造函数注入
- 多 Scope（父子 Scope）
- 自动扫描模块
- 与 ECS 协作

## **线程安全说明**

GodotSharp.DI 是基于 Godot 主线程模型设计的依赖注入框架：

- 所有 `IScope` 方法（如 `ResolveDependency`、`RegisterService`）必须在主线程调用
- 不支持从后台线程直接访问 Scope 或 Service
- 如果需要在后台线程处理数据，请使用：

csharp

```
Task.Run(() =>
{
    var data = ProcessData();
    CallDeferred(nameof(RegisterServiceOnMainThread), data);
});
```

在 Debug 模式下，框架可以启用主线程断言（AssertMainThread），帮助你在开发阶段发现错误调用。

**以下方法绝不能在后台线程调用：**

- ResolveDependency
- RegisterService / UnregisterService
- GetService
- InstantiateScopeSingletons
- DisposeScopeSingletons
- ResolveUserDependencies
- CreateService
- OnDependencyResolved
- OnServicesReady

**后台线程只应该做纯计算，** **所有 DI 操作必须通过 CallDeferred 回到主线程执行。**

## **QuickStart**

1. 定义服务

   ```c#
   [Singleton(typeof(IConfig))]
   public partial class ConfigService : IConfig { }
   ```

2. 定义 Host

   ```c#
   [Host]
   public partial class GameHost
   {
       [Singleton(typeof(IConfig))]
       private ConfigService Config { get; } = new();
   }
   ```

3. 定义 Scope

   ```c#
   [Modules(Instantiate = [typeof(ConfigService)], Expect = [typeof(GameHost)])]
   public partial class GameScope : Node, IScope { }
   ```

4. 在 Node 中使用

   ```c#
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

## 特性和结构使用规则

| 角色            | 标记 Inject                   | 标记 InjectConstructor | 标记 Singleton                      | 标记 Transient                      | 标记 Host | 标记 User | 标记 Modules / AutoModules | 非 Node 类型 | Node 类型 |
| --------------- | ----------------------------- | ---------------------- | ----------------------------------- | ----------------------------------- | --------- | --------- | -------------------------- | ------------ | --------- |
| **Service**     | **禁止**                      | **允许（唯一）**       | **仅类型级别（与 Transient 互斥）** | **仅类型级别（与 Singleton 互斥）** | **禁止**  | **禁止**  | **禁止**                   | 允许         | **禁止**  |
| **User**        | **允许**                      | **禁止**               | **禁止**                            | **禁止**                            | **禁止**  | **允许**  | **禁止**                   | 允许         | **允许**  |
| **Host**        | **禁止**                      | **禁止**               | **仅成员级别**                      | **禁止**                            | **允许**  | **禁止**  | **禁止**                   | 允许         | **允许**  |
| **Host + User** | **允许（与 Singleton 互斥）** | **禁止**               | **仅成员级别（与 Inject 互斥）**    | **禁止**                            | **允许**  | **允许**  | **禁止**                   | 允许         | **允许**  |
| **Scope**       | **禁止**                      | **禁止**               | **禁止**                            | **禁止**                            | **禁止**  | **禁止**  | **必须有（二选一）**       | 禁止         | 允许      |



## 源生成器流程图

```
// 类级构建和验证

ClassTypeValidator
 ├─ ValidateRoles
 ├─ ValidateRoleConflicts
 ├─ ValidateConstructors (仅选择，不含参数验证)
 ├─ ValidateMembers (仅标记规则，不含类型验证)
 ├─ ValidateScopeRequirements (仅标记规则)
 └─ 输出 diagnostics

ClassTypeInfoFactory
 ├─ 调用 Validator
 ├─ 如果有错误 → Failure
 ├─ 根据 Roles 构建 TypeInfo
 └─ 返回 TypeInfoBuildResult

// 图级构建和验证

DiGraphBuilder
 ├─ BuildTypeInfoMap
 ├─ BuildScopes (with {})
 └─ ValidateGraph
      ├─ ValidateConstructorParameters
      ├─ ValidateMemberTypes
      ├─ ValidateLifetimes
      ├─ ValidateCircularDependencies
      ├─ ValidateScopeModules
      └─ ValidateAutoModules

// 生成代码
Generators
 ├─ ServiceGenerator
 ├─ HostGenerator
 ├─ UserGenerator
 └─ ScopeGenerator

```

## 诊断 id 

| 类别  | 含义                           | 示例                          |
| ----- | ------------------------------ | ----------------------------- |
| **C** | Class-level（类型级错误）      | 标记冲突、角色冲突            |
| **S** | Service-level（服务语义错误）  | 生命周期、构造函数参数        |
| **M** | Member-level（成员注入错误）   | Inject/Singleton 成员错误     |
| **P** | Scope-level（Scope 语义错误）  | Scope.Instantiate/Expect 错误 |
| **D** | Dependency-level（依赖图错误） | 循环依赖、不可解析依赖        |
| **G** | Generator-level（生成器错误）  | 生成失败、内部错误            |

| 类别 | 范围    | 用途           |
| ---- | ------- | -------------- |
| C    | 001–099 | 类型级错误     |
| S    | 200–299 | 服务语义错误   |
| M    | 300–399 | 成员注入错误   |
| P    | 400–499 | Scope 语义错误 |
| D    | 500–599 | 依赖图错误     |
| G    | 900–999 | 生成器内部错误 |

Service 注入构造函数参数（User 注入成员）类型验证

| 参数是 Service 接口类型    | ✔    | 推荐                 |
| -------------------------- | ---- | -------------------- |
| 参数是 Service 实现类型    | ✖    | 必须使用接口         |
| 参数是 Host 提供的服务类型 | ✔    | 通过 Scope.Expect    |
| 参数是普通类型             | ✖    | 无法解析             |
| 参数是 Node 类型           | ✖    | 生命周期不受 DI 管理 |
| 参数是 Scope 类型          | ✖    | 循环依赖             |
| 参数是 User 类型           | ✖    | User 不是服务        |
| 参数是集合类型             | ✖    | 不支持多实现注入     |
| 参数是开放泛型             | ✖    | 无法静态分析         |
| 参数是泛型闭包             | ✔    | 只要是服务类型       |
| 参数重复                   | ✖    | 不支持多实例         |

TodoList

# 📘 GodotSharp.DI — Roadmap / TODO List

GodotSharp.DI 正在持续演进中，以下是框架的未来规划与待办事项。 本清单按模块划分，涵盖文档、代码生成器、Scope 系统、Diagnostics、测试、多语言支持等核心领域。

# 🧭 1. 文档与示例（Documentation & Samples）

- [ ] **Quick Start**：从零开始使用 DI 的完整示例
- [ ] **四大角色指南**：Service / Host / User / Scope
- [ ] **生命周期图**：Godot 生命周期 vs DI 生命周期
- [ ] **线程安全说明**：CallDeferred 模式、主线程限制
- [ ] **完整示例项目**（Sample Project）
- [ ] **FAQ**：常见错误与解决方案

# 🧩 2. 代码生成器（Source Generator）

- [ ] **统一文件头模板**（auto-generated + thread safety）
- [ ] **统一 XML 文档注释模板**
- [ ] **生成器多语言支持（.resx）**
- [ ] **生成器配置（.editorconfig）**
  - [ ] 是否生成调试信息
  - [ ] 是否生成线程安全注释
  - [ ] 注释语言（zh-Hans / en-US）
- [ ] **性能优化**（减少重复扫描、减少字符串拼接）
- [ ] **生成器诊断**（重复服务、循环依赖等）

# 🧱 3. Service 构造函数注入（Service Factory）

- [ ] **最终版 CreateService 模板**（remaining--）
- [ ] 支持 **0 参数 / N 参数构造函数**
- [ ] 生成 **构造函数参数的 XML 注释**
- [ ] 生成 **构造函数参数的调试信息**
- [ ] 多语言错误提示（服务未找到、构造失败）

# 🧩 4. User 注入（User Injection）

- [ ] **最终版 ResolveUserDependencies 模板**（无锁 HashSet）
- [ ] 支持字段 / 属性注入
- [ ] 生成注入成员的 XML 注释
- [ ] 自动触发 OnServicesReady
- [ ] 多语言错误提示（未解析依赖）

# 🌲 5. Scope 系统（Scope Lifecycle）

- [ ] **最终版 Scope 生命周期模板**（NotificationReady / Predelete）
- [ ] 父子 Scope 自动继承
- [ ] ScopeSingleton 生命周期管理
- [ ] Scope 类 XML 文档注释（线程安全说明）
- [ ] DEBUG 模式下的 AssertMainThread
- [ ] 多语言错误提示（服务未找到、重复注册）

# 🧪 6. Diagnostics（Analyzer + Validator）

- [ ] 完成 Diagnostics ID 规范（GDI-M-xxx / GDI-U-xxx / GDI-S-xxx）
- [ ] 完成 Diagnostics.md  文档
- [ ] 诊断规则：
  - [ ] 循环依赖
  - [ ] 未注册服务
  - [ ] 重复注册
  - [ ] 无法解析的构造函数参数
  - [ ] 无法注入的成员
- [ ] 多语言诊断消息（.resx）

# 🧰 7. 工具与辅助模块（Helpers）

- [ ] SourceGenHelpers（文件头 + XML 注释 + 多语言）
- [ ] TypeNameFormatter（统一类型格式化）
- [ ] CodeFormatter（缩进与换行优化）
- [ ] DebugHelpers（可选：打印 DI 调试信息）

# 🧪 8. 测试（Testing）

- [ ] 单元测试：Service 构造函数注入
- [ ] 单元测试：User 注入
- [ ] 单元测试：Scope 生命周期
- [ ] 单元测试：父子 Scope 继承
- [ ] 单元测试：Diagnostics
- [ ] 集成测试：完整场景树注入流程

# 🌐 9. 多语言支持（Localization）

- [ ] 运行时错误信息使用 .resx
- [ ] Diagnostics 使用 .resx
- [ ] 文件头注释支持多语言（可选）
- [ ] XML 文档注释支持多语言（可选）
- [ ] 生成器根据 `.editorconfig` 选择语言

# 🧭 10. 未来扩展（Future Work）

- [ ] 后台服务容器（非 Godot Node）
- [ ] 主线程调度器（Dispatcher）
- [ ] 延迟服务（Lazy<T>）
- [ ] 条件服务（Conditional Service）
- [ ] 模块系统（AutoModule）
- [ ] 服务标签（Service Tags）
