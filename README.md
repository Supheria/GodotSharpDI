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
