# v1.0.0-rc.3

> ## 主要新功能
>
> ### ✨ 注入失败回调机制
>
> **RC.3 新增**：现在每个 Inject 成员都可以独立地失败回调，用于更细粒度的错误处理。
>
> **用法示例**：
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
> GD.PrintErr($"GameManager 注入失败: {error}");
> // 实现降级逻辑
> }
> }
> ```
>
> **优势**：
>
> - 针对每个依赖单独处理注入失败，而不是全局处理
> - 为可选依赖实现更灵活的降级逻辑
> - 更好的错误处理与用户体验
>
> ---
>
>
> ### 🎯 注入就绪状态指示器
>
> **RC.3 新增**：每个 `[Inject]` 成员现在都会生成一个对应的 `IsXxxInjectionReady` 布尔指示器。
>
> **用法示例**：
>
> ```csharp
> [User]
> public partial class PlayerUI : Control
> {
>     [Inject]
>     private IGameManager GameManager { get; set; }
> 
>     public void Update()
>     {
>         // 在运行时检查依赖是否已就绪
>         if (IsGameManagerInjectionReady)
>         {
>             GameManager.DoSomething();
>         }
>     }
> }
> ```
>
> **优势**：
>
> - 运行时检查依赖是否可用
> - 在处理可选依赖时更安全
> - 根据注入状态更好地控制流程
>
> ---
>
> ### 🔄 接口重命名：IServicesReady → IDependenciesResolved
>
> **破坏性变更**：接口已重命名以更准确地表达其用途，并更新了方法签名。
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
>
> ```csharp
> public interface IDependenciesResolved
> {
>    void OnDependenciesResolved(bool isAllDependenciesReady);
> }
> ```
>
> **迁移要求**：
>
> - 将 `IServicesReady` 替换为 `IDependenciesResolved`
> - 更新方法签名，增加 `isAllDependenciesReady` 参数
> - 根据参数值处理部分依赖失败的情况
>
> **迁移示例**：
>
> ```csharp
> // 旧代码（RC.2）
> [User]
> public partial class PlayerUI : Control, IServicesReady
> {
>    public void OnServicesReady()
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
> ### 🚫 泛型类型限制
>
> **RC.3 新增**：所有 DI 角色（Service、Host、User、Scope）都不能是泛型类型。
>
> **原因**：
>
> - 泛型类型在没有类型参数时无法实例化
> - 泛型类型不能作为稳定的服务标识符
> - 类型安全与依赖图构建需要具体类型
>
> **错误信息示例**：
>
> - Service: “泛型类型不能作为服务实现”
> - Host: “泛型类型不能标记为 [Host]”
> - User: “泛型类型不能标记为 [User]”
> - Scope: “泛型类型不能标记为 [Scope]”
>
> **解决方案**： 如果你需要使用泛型，请创建继承自泛型类型的具体类：
>
> ```csharp
> // ❌ 不允许
> [Singleton(typeof(IRepository<Player>))]
> public partial class Repository<T> : IRepository<T> { }
> 
> // ✅ 正确方式
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
> ### 📊 完整依赖链展示
>
> **RC.3 增强**：当依赖解析失败时，错误信息现在会展示完整的依赖链。
>
> **示例错误信息**：
>
> ```
> Error: Failed to resolve dependency chain:
>   PlayerController (User)
> → ICombatSystem (Service)
>   → IWeaponFactory (Service)
>   → IResourceLoader (missing)
> ```
>
> **优势**：
>
> - 快速定位缺失的服务
> - 理解依赖失败的完整上下文
> - 更容易调试复杂依赖图
>
> ---
>
> ### 🔍 运行时循环依赖检测
>
> **RC.3 优化**：循环依赖检测现在仅在 DEBUG 构建中运行，以提升性能。
>
> **检测范围**：
>
> - 仅检查 Service → Service 的构造函数依赖
> - 不检查 User 的 `[Inject]` 成员（它们在构造后解析）
> - 不检查 Host 的 `[Singleton]` 成员
> - 不检查 Host+User 的自注入模式
>
> **原因说明**： Host+User 自注入不属于循环依赖，因为：
>
> 1. Host 注册不会触发注入
> 2. Service 构造先完成
> 3. User 注入随后进行
> 4. 不会形成构造函数循环
>
> ---
>
> ### 📝 更清晰的错误信息
>
> **RC.3 改进**：所有错误信息现在包含：
>
> - 出了什么问题
> - 为什么这是个问题
> - 可行的修复建议
> - 完整依赖链上下文
>
> ---
>
> ## 代码生成改进
>
> ### 🏭 服务工厂优化
>
> **RC.3 变更**：`ServiceFactories` 现在是静态集合，以提升内存效率。
>
> **影响**：
>
> - 更低的内存占用
> - 更快的服务工厂查找
> - 在大型依赖图中性能更佳
>
> ---
>
> ### 🏭 服务创建或提供失败同样回调处理
>
> **RC.3 变更**：服务创建失败现在会写入服务缓存并出发失败回调。
>
> **影响**：
>
> - 更好的错误传播
> - 防止等待队列始终等待已经明确失败的服务
> - 更清晰的错误信息
>
> ---
>
> ### 📁 文件命名增强
>
> **RC.3 改进**：生成的文件现在使用 `Namespace+MetaName` 格式以提升组织性。
>
> **示例**：
>
> - 之前：`PlayerController.DI.g.cs`
> - 现在：`MyGame.Player.PlayerController.DI.g.cs`
>
> **优势**：
>
> - 避免大型项目中的命名冲突
> - 在解决方案资源管理器中更易查找
> - 更清晰的文件组织结构
>
> ---
>>
> ## 内部错误处理与健壮性
>
> ### 🛡️ 全面的异常处理
>
> **RC.3 新增**：源生成器、分析器和代码修复提供器现在具有健壮的异常处理机制以确保稳定性。
>
> **改进内容**：
>
> #### 源生成器
> - **分层异常处理**：代码生成的每个阶段都有独立的错误处理
> - **详细诊断**：新增内部错误诊断（GDI_E001-E101）提供清晰的错误消息
> - **优雅降级**：一个类的失败不会阻止其他类的生成
> - **用户友好消息**：错误信息解释了失败原因和修复方法
>
> **新增错误代码**：
> - `GDI_E001`: 生成器初始化失败
> - `GDI_E010`: 类分析失败
> - `GDI_E011`: 符号缓存不可用
> - `GDI_E012`: 类验证失败
> - `GDI_E020`: 依赖图构建失败
> - `GDI_E021`: 图构建阶段失败
> - `GDI_E030`: 服务提供者注册失败
> - `GDI_E040`: 节点构建失败
> - `GDI_E050`: 依赖图验证失败
> - `GDI_E100`: 代码生成失败
> - `GDI_E101`: 源码输出失败
>
> #### 分析器
> - **静默失败**：分析器异常不再导致编译崩溃
> - **受保护的分析**：每个语法节点都独立分析并带有异常保护
> - **取消支持**：正确处理 `OperationCanceledException`
> - **保守策略**：如有疑问，跳过报告而非崩溃
>
> **受影响的分析器**：
> - `GeneratedMemberAccessAnalyzer`: 检测对生成成员的手动访问
> - `InjectionFailureCallbackAnalyzer`: 检测缺失的失败回调实现
>
> #### 代码修复提供器
> - **稳定的 IDE 体验**：代码修复失败不再导致快速修复菜单崩溃
> - **后备机制**：当复杂生成失败时使用简化的代码生成
> - **安全解析**：字符串提取和方法生成受边缘情况保护
> - **返回原文档**：修复失败时返回未修改的原始文档
>
> **受影响的提供器**：
> - `NotificationMethodCodeFixProvider`: 添加缺失的 `_Notification` 方法
> - `InjectionFailureCallbackCodeFixProvider`: 实现缺失的失败回调
>
> ---
> 
> ## 迁移指南
> 
> ### 必须修改的内容
>
> 1. **更新接口实现**：
> 
> ``` csharp
>// 将此代码
> public partial class MyClass : Node, IServicesReady
> {
>     public void OnServicesReady() { }
> }
> 
>// 替换为
> public partial class MyClass : Node, IDependenciesResolved
>{
>     public void OnDependenciesResolved(bool isAllDependenciesReady)
>    {
>         if (isAllDependenciesReady)
>        {
>             // 初始化逻辑
>         }
>     }
> }
> ```
> 
> 2. **检查泛型类型**：
> - 移除所有 Service、Host、User、Scope 类上的泛型参数
>  - 如有需要，创建具体包装类
> 
> 3. **可选：添加失败回调**：
> 
>   ```csharp
>[Inject(FailureCallback = true)]
> private IOptionalService Service { get; set; }
> 
> partial void OnServiceInjectionFailed(string error)
>{
>     // 处理失败
>}
>   ```
>
> ---
> 
> ## 总结
> 
> v1.0.0-rc.3 带来了显著的错误处理与诊断增强：
> 
> ✅ **新功能**：
> 
> - 注入失败回调，提供更细粒度的错误处理
> - 注入就绪指示器，支持运行时检查依赖状态
> - 完整依赖链展示，诊断更清晰
> 
> ⚠️ **破坏性变更**：
> 
> - `IServicesReady` → `IDependenciesResolved`
> - DI 角色不再允许泛型类型
> 
> 🚀 **性能优化**：
> 
>- 静态服务工厂集合
> - 循环依赖检测仅在 DEBUG 中运行
> 
> 
>
> 在进一步完善并修整项目整体的代码后，下个版本就是 1.0 正式版！🎉

# v1.0.0-rc.2

> ## 关键修复
>
> ### ✅ 修复 `OnServicesReady()` 调用时机问题
>
> **RC.1 的问题**：`OnServicesReady()` 可能在 `_Ready()` 之前调用，破坏了节点就绪时依赖已可用的保证。
>
> **RC.2 修复**：
>
> - `OnServicesReady()` 现在保证在 `_Ready()` 之后调用
> - 所有依赖在回调执行前已完全解析
> - 与 Godot 生命周期更好地集成
>
> 
>
> ## 增强的类型验证
>
> ### 新增诊断
>
> - Inject 成员不能是普通 Node（错误）
> - Inject 成员类型应为接口（警告）
> - Singleton 成员类型无效（错误）
> - Singleton 成员是 Host 类型（警告）
> - Singleton 成员不能是 User 类型（错误）
> - Singleton 成员不能是 Scope/普通 Node（错误）
> - Singleton 成员暴露的类型未实现（错误）
> - Singleton 成员暴露的类型应为接口（警告）
> - 构造函数参数是 Host 类型（警告）
> - 构造函数参数不能是 User 类型（错误）
> - 构造函数参数不能是 Scope 类型（错误）
> - 构造函数参数不能是普通 Node（错误）
> - 构造函数参数应为接口（警告）
> - Inject 成员类型未被任何服务暴露（错误）
>
> 
>
> ## 改进的错误信息
>
> 所有诊断消息现在提供：
>
> - 清晰的错误说明
> - 为什么这是个问题
> - 可行的修复建议
>
> csharp
>
> ```
> // 之前（RC.1）：
> // Error: [Inject] member 'IGameState _state' has invalid type
> 
> // 之后（RC.2）：
> // Warning GDI_M041: [Inject] member '_manager' 的类型为 'GameManager'，
> // 它是一个 [Host] 类型。虽然允许，但不推荐直接注入 Host 类型，
> // 建议注入 Host 暴露的接口
> ```
>
> 
>
> ## 资源组织
>
> ### 标准化资源命名
>
> 所有诊断消息现在使用前缀资源名：
>
> - `C_*` - 类级诊断
> - `M_*` - 成员级诊断
> - `S_*` - 构造函数级诊断
> - `D_*` - 依赖图诊断
> - `E_*` - 内部错误诊断
> - `U_*` - 用户行为诊断
>
> 
>
> 它已经非常接近生产可用状态，期待稳定的 1.0 发布！🚀