# 📘 DI 类型约束总表（修订版）

这张表格总结了 GodotSharp.DI 中所有与类型相关的语义约束。每个角色的职责、允许/禁止的类型、生命周期规则都在此一目了然。

## 🟦 1. 角色类型约束（Service / Host / User / Scope）

| 角色        | 必须是 | 是否 Node | 是否允许标记 | 是否可作为 Service | 是否可被注入           | 是否可暴露类型 | 说明                           |
| ----------- | ------ | --------- | ------------ | ------------------ | ---------------------- | -------------- | ------------------------------ |
| **Service** | class  | ❌ 否      | 无           | ✔ 是               | ✔ 是（按 Inject 规则） | ✔ 必须暴露接口 | 纯逻辑服务，由 Scope 创建      |
| **Host**    | class  | ✔ 是      | Host ✔       | ❌ 否               | ❌ 否                   | ✔ 是           | 场景级资源提供者，提供单例服务 |
| **User**    | class  | 任意      | User ✔       | ❌ 否               | ✔ 是（按 Inject 规则） | ❌ 否           | 依赖消费者，由 Scope 注入      |
| **Scope**   | class  | ✔ 是      | Scope ✔      | ❌ 否               | ❌ 否                   | ❌ 否           | DI 容器根节点，管理生命周期    |

**关键修正**：
- Scope **必须是 Node**（原表格错误）
- Host **可以暴露类型**（通过 [Singleton] 成员）

## 🟩 2. 注入类型（Inject Type）约束

| 条目                       | 是否允许 | 说明                          |
| -------------------------- | -------- | ----------------------------- |
| interface                  | ✔        | 推荐方式                      |
| class                      | ✔        | 但不能是 Node/Host/User/Scope |
| Node                       | ❌        | 生命周期由 Godot 控制         |
| Host                       | ❌        | Host 不可被注入               |
| User                       | ❌        | User 不可被注入               |
| Scope                      | ❌        | Scope 不可被注入              |
| abstract class             | ❌        | 无法实例化                    |
| static class               | ❌        | 无法实例化                    |
| array / pointer / delegate | ❌        | 不支持                        |
| dynamic                    | ❌        | 不可分析                      |
| 开放泛型                   | ❌        | 不可实例化                    |

## 🟧 3. Service 实现类型（Service Type）约束

| 条目                       | 是否允许 | 说明               |
| -------------------------- | -------- | ------------------ |
| class                      | ✔        | 必须是 class       |
| sealed class               | ✔        | 推荐               |
| abstract class             | ❌        | 无法实例化         |
| static class               | ❌        | 无法实例化         |
| Node                       | ❌        | 生命周期冲突       |
| Host                       | ❌        | Host 不是 Service  |
| User                       | ❌        | User 不是 Service  |
| Scope                      | ❌        | Scope 不是 Service |
| interface                  | ❌        | 不能作为实现类型   |
| 开放泛型                   | ❌        | 无法实例化         |
| array / pointer / delegate | ❌        | 不支持             |
| dynamic                    | ❌        | 不可分析           |

## 🟦 4. 暴露类型（Exposed Service Type）约束 ⭐ 修订

| 条目                       | 是否允许 | 说明                                    |
| -------------------------- | -------- | --------------------------------------- |
| interface                  | ✔        | **强烈推荐**（最佳实践）                |
| concrete class             | ✔        | 允许但不推荐（用于 DTO/配置类等场景）   |
| abstract class             | ❌        | 无法实例化，无意义                      |
| Node                       | ❌        | 不允许                                  |
| Host/User/Scope            | ❌        | 不允许                                  |
| sealed class               | ✔        | 允许（用于无需多态的场景）              |
| 开放泛型                   | ❌        | 不允许                                  |
| array / pointer / delegate | ❌        | 不允许                                  |
| dynamic                    | ❌        | 不允许                                  |

**DI 最佳实践分析**：

✅ **推荐使用 interface**：
```csharp
[Singleton(typeof(IConfig))]  // ✅ 最佳实践
public partial class ConfigService : IConfig { }
```

**原因**：
- 依赖倒置原则（DIP）
- 易于测试（Mock）
- 降低耦合
- 支持多实现

⚠️ **允许 concrete class（有限场景）**：
```csharp
// 场景 1: DTO/数据类
[Singleton(typeof(GameConfig))]  // ⚠️ 允许但不推荐
public partial class GameConfig 
{ 
    public string Name { get; set; }
    public int Level { get; set; }
}

// 场景 2: 配置类（无需抽象）
[Singleton(typeof(AppSettings))]  // ⚠️ 可接受
public sealed partial class AppSettings 
{
    public readonly string Version = "1.0";
}
```

**允许理由**：
- 某些 DTO 不需要接口
- 配置类通常不需要多态
- sealed class 明确表达不可继承意图

**建议**：
- 默认使用 interface
- 仅在明确不需要抽象时使用 class
- 如果使用 class，建议标记为 sealed

## 🟩 5. User Inject 成员约束 ⭐ 修订

| 条目                      | 是否允许 | 说明          |
| ------------------------- | -------- | ------------- |
| 成员类型满足 Inject Type  | ✔        | 必须          |
| 字段                      | ✔        | 推荐          |
| 属性（带 setter）         | ✔        | 必须有 setter |
| 属性（无 setter）         | ❌        | 无法注入      |
| **static 成员**           | ❌        | **不允许**    |
| Node/Host/User/Scope 类型 | ❌        | 不允许        |

**新增约束**：static 成员不允许注入

## 🟧 6. Host Singleton 成员约束 ⭐ 重新设计

| 条目                               | 是否允许 | 说明                     |
| ---------------------------------- | -------- | ------------------------ |
| 成员类型可以是任意类型             | ✔        | 包括 Host 自身           |
| 暴露类型满足 Exposed Type          | ✔        | 必须                     |
| 字段                               | ✔        | 推荐                     |
| 属性（带 getter）                  | ✔        | 必须有 getter            |
| 属性（无 getter）                  | ❌        | 无法读取实例             |
| **static 成员**                    | ❌        | **不允许**               |
| **成员值是 Host 自身（this）**     | ✔        | **常见用法，必须允许**   |
| **成员值是新建实例**               | ✔        | **常见用法，必须允许**   |
| **成员值的类型不需要生命周期标记** | ✔        | **成员值不是 Service**   |

**DI 最佳实践分析**：

### 场景 1：Host 暴露自身为服务 ✅ **最佳实践**

```csharp
[Host]
public partial class ChunkManager : Node, IChunkGetter, IChunkGenerator
{
    [Singleton(typeof(IChunkGetter), typeof(IChunkGenerator))]
    private IChunkGetter Self => this;  // ✅ Host 暴露自己
}
```

**理由**：
- Host 是 Godot Node，有复杂的生命周期
- Host 管理场景资源（Chunk、Cell 等）
- 其他服务需要访问这些资源
- Host 暴露接口给其他组件使用

**正确性**：
- `Self => this` 的类型是 `IChunkGetter`（接口）
- Host 本身实现了这个接口
- 不需要在 Host 类上标记 `[Singleton]`（那是 Service 的标记）
- 只在**成员**上标记 `[Singleton]` 表示暴露服务

### 场景 2：Host 持有并暴露其他实例 ✅ **允许**

```csharp
[Host]
public partial class WorldManager : Node
{
    [Singleton(typeof(IWorldData))]
    private WorldData _worldData = new();  // ✅ Host 持有独立实例
}

// WorldData 不是 Service，只是普通类
public class WorldData : IWorldData
{
    public string WorldName { get; set; }
}
```

**理由**：
- Host 可以持有和管理其他对象
- 这些对象不需要标记为 Service
- Host 负责它们的生命周期

### 场景 3：❌ **错误用法** - 不应该出现

```csharp
// ❌ 错误：成员类型本身标记为 Service
[Host]
public partial class BadHost : Node
{
    [Singleton(typeof(IConfig))]
    private ConfigService _config = new();  // ❌ ConfigService 已经是 [Singleton]
}

[Singleton(typeof(IConfig))]  // ❌ 冲突！
public partial class ConfigService : IConfig { }
```

**为什么错误**：
- ConfigService 应该由 Scope 创建和管理
- Host 不应该持有 Service 类型的实例
- 这会导致生命周期冲突

**正确做法**：
```csharp
[Host, User]  // Host + User 组合
public partial class GoodHost : Node
{
    [Singleton(typeof(ISelf))]
    private ISelf Self => this;  // ✅ 暴露自己
    
    [Inject]
    private IConfig _config;  // ✅ 注入 Service
}
```

## 🟦 7. 暴露类型冲突规则 ⭐ 修订

| 情况                                            | 是否允许 | 说明             |
| ----------------------------------------------- | -------- | ---------------- |
| 同一个接口由多个 Service 注册                   | ❌        | 必须报错（冲突） |
| 同一个接口由多个 Host Singleton 注册            | ❌        | 必须报错（冲突） |
| 同一个接口同时由 Service 和 Host Singleton 注册 | ❌        | 必须报错（冲突） |
| 不同接口由不同 Service/Host 注册                | ✔        | 合法             |

**补充说明**：任何服务接口在一个 Scope 内只能有唯一的提供者。

## 🟩 8. 最终语义总结 ⭐ 修订

- **Service**：class，非 Node，暴露接口（推荐）或 class，由 Scope 创建和管理
- **Host**：Node，通过成员暴露服务，成员值可以是 Host 自身（this）或持有的实例
- **User**：Node 或非 Node，注入依赖，不提供服务
- **Scope**：Node，容器根，不可注入，管理所有服务生命周期
- **Inject Type**：interface 或 class（非 Node/Host/User/Scope/abstract/static）
- **Exposed Type**：推荐 interface，允许 concrete class（用于特殊场景）
- **Host Singleton**：成员上的标记，表示暴露服务，成员值可以是任意对象（包括 this）
- **User Inject**：成员类型必须是 Inject Type，成员不能是 static

## 🎯 三个争议规则的最佳实践分析

### 争议 1：暴露类型必须是 interface？

**原规则**：暴露类型必须是 interface

**DI 最佳实践**：

#### ✅ 推荐：使用 interface（占 95% 场景）

```csharp
// 最佳实践
[Singleton(typeof(IUserService))]
public partial class UserService : IUserService { }
```

**优点**：
- ✅ 依赖倒置原则（DIP）
- ✅ 易于单元测试（Mock）
- ✅ 支持多实现
- ✅ 降低耦合

#### ⚠️ 允许：使用 concrete class（占 5% 场景）

```csharp
// 场景 1: DTO/数据传输对象
[Singleton(typeof(GameConfig))]
public sealed partial class GameConfig 
{
    public string Name { get; set; }
    public int MaxPlayers { get; set; }
}

// 场景 2: 纯数据容器
[Singleton(typeof(PlayerStats))]
public sealed partial class PlayerStats
{
    public int Health;
    public int Mana;
}

// 场景 3: 不需要多态的工具类
[Singleton(typeof(MathUtils))]
public sealed partial class MathUtils
{
    public float Clamp(float value, float min, float max) => ...;
}
```

**允许理由**：
- 某些类型本质上不需要抽象（如纯数据结构）
- sealed class 明确表达"不可扩展"的意图
- 减少不必要的接口文件

**建议规则**：
```markdown
✅ 强烈推荐 interface
✅ 允许 sealed class（特殊场景）
⚠️ 不推荐 non-sealed class（容易误用）
❌ 禁止 abstract class
```

**实施方案**：
- 编译期：允许 interface 和 concrete class
- 分析器：如果暴露 non-sealed class，产生 Warning（不是 Error）
- 文档：明确推荐 interface

### 争议 2：Host Singleton 成员的实现类型不能带生命周期标记？

**原规则**：实现类型带 [Singleton]/[Transient] 必须禁止

**DI 最佳实践**：

#### 场景分析

```csharp
// 场景 A: Host 成员是普通类（没有 Service 标记）✅ 正确
[Host]
public partial class ChunkManager : Node, IChunkGetter
{
    [Singleton(typeof(IChunkGetter))]
    private IChunkGetter Self => this;  // ✅ this 是 Host 自己
}

// 场景 B: Host 成员是 Service 类 ❌ 冲突
[Host]
public partial class BadHost : Node
{
    [Singleton(typeof(IConfig))]
    private ConfigService _config = new();  // ❌ ConfigService 是 Service
}

[Singleton(typeof(IConfig))]  // ← 已经标记为 Service
public partial class ConfigService : IConfig { }
```

**问题**：
- 场景 A：`this` 不是 Service，是 Host 自己 → ✅ **应该允许**
- 场景 B：`ConfigService` 已经是 Service → ❌ **应该禁止**

**正确规则**：
```markdown
❌ Host Singleton 成员的值，如果是一个**类型的实例**，该类型不能带 [Singleton]/[Transient] 标记
✅ Host Singleton 成员的值可以是 Host 自身（this）
✅ Host Singleton 成员的值可以是普通类的实例
```

**最佳实践**：

```csharp
// ✅ 正确：Host 暴露自己
[Host]
public partial class WorldManager : Node, IWorldData
{
    [Singleton(typeof(IWorldData))]
    private IWorldData Self => this;
}

// ✅ 正确：Host 持有普通对象
[Host]
public partial class LevelManager : Node
{
    [Singleton(typeof(ILevelData))]
    private LevelData _data = new();  // LevelData 是普通类
}

public class LevelData : ILevelData  // 无 Service 标记
{
    public int LevelId { get; set; }
}

// ❌ 错误：Host 持有 Service
[Host]
public partial class BadHost : Node
{
    [Singleton(typeof(IConfig))]
    private ConfigService _config = new();  // ❌ ConfigService 是 Service
}
```

**实施方案**：
1. 允许成员值是 `this`（Host 自身）
2. 允许成员值是普通类实例
3. 禁止成员值的**类型**带有 `[Singleton]` 或 `[Transient]`
4. 添加诊断：GDI_M060

### 争议 3：Host Singleton 成员的实现类型不能是 Host 自身？

**原规则**：实现类型是 Host 自身不允许

**DI 最佳实践分析**：

这个规则与实际使用**完全冲突**，应该**删除**。

#### 实际使用场景（常见且正确）

```csharp
[Host]
public partial class ChunkManager : Node, IChunkGetter, IChunkGenerator
{
    [Singleton(typeof(IChunkGetter), typeof(IChunkGenerator))]
    private ChunkManager Self => this;  // ✅ 暴露自己，完全合理
}
```

**为什么必须允许**：

1. **Godot Node 的特性**：
   - Node 由 Godot 引擎管理生命周期
   - 不能通过 DI 容器创建
   - 必须在场景树中存在

2. **Host 的设计目的**：
   - 就是为了让 Node 能够作为服务提供者
   - Host 暴露自己是最核心的用法

3. **实际应用**：
   ```csharp
   // 游戏管理器暴露自己
   [Host]
   public partial class GameManager : Node, IGameState
   {
       [Singleton(typeof(IGameState))]
       private IGameState Self => this;
       
       public int Score { get; set; }
   }
   
   // 其他组件注入使用
   [User]
   public partial class UI : Control
   {
       [Inject] private IGameState _gameState;
       
       void UpdateScore() => Label.Text = _gameState.Score.ToString();
   }
   ```

**正确规则**：
```markdown
✅ Host Singleton 成员的值**可以且应该**是 Host 自身（this）
✅ 这是 Host 最主要和最常见的使用方式
❌ 只需要禁止成员值的**类型**是标记为 Service 的类
```

**实施方案**：
- 完全允许 `Self => this` 模式
- 检查成员值的类型是否标记为 Service（禁止）
- 不检查成员值是否是 Host 自身（允许）

## 📋 修订后的诊断需求清单

### 需要新增的诊断

#### 1. GDI_M051: Inject 成员不能是 Host 类型
```csharp
[User]
public partial class BadUser : Node
{
    [Inject] private ChunkManager _host;  // ❌ Host 不可注入
}
```

#### 2. GDI_M052: Inject 成员不能是 User 类型
```csharp
[User]
public partial class BadUser : Node
{
    [Inject] private OtherUser _user;  // ❌ User 不可注入
}
```

#### 3. GDI_M053: Inject 成员不能是 Scope 类型
```csharp
[User]
public partial class BadUser : Node
{
    [Inject] private MyScope _scope;  // ❌ Scope 不可注入
}
```

#### 4. GDI_M054: Inject 成员不能是 static
```csharp
[User]
public partial class BadUser : Node
{
    [Inject] private static IService _service;  // ❌ static 不允许
}
```

#### 5. GDI_M055: Host Singleton 成员不能是 static
```csharp
[Host]
public partial class BadHost : Node
{
    [Singleton(typeof(IService))]
    private static IService _service;  // ❌ static 不允许
}
```

#### 6. GDI_M060: Host Singleton 成员值的类型不能是 Service
```csharp
[Host]
public partial class BadHost : Node
{
    [Singleton(typeof(IConfig))]
    private ConfigService _config = new();  // ❌ ConfigService 是 Service
}

[Singleton(typeof(IConfig))]
public partial class ConfigService : IConfig { }
```

#### 7. GDI_D050: 服务类型冲突检测
```csharp
[Singleton(typeof(IService))]
public partial class ServiceA : IService { }

[Singleton(typeof(IService))]  // ❌ 冲突
public partial class ServiceB : IService { }

[Modules(
    Instantiate = [typeof(ServiceA), typeof(ServiceB)]  // ❌ 两个都提供 IService
)]
public partial class Scope : Node, IScope { }
```

#### 8. GDI_W001: 暴露类型建议使用 interface (Warning)
```csharp
[Singleton(typeof(ConfigService))]  // ⚠️ 建议使用 interface
public partial class ConfigService { }
```

### 需要修改的现有检查

#### CachedSymbols.IsValidInjectType
需要添加检查：不能是 Host/User/Scope

#### ClassPipeline.ProcessSingleMember
需要添加检查：
1. static 成员检查
2. Host Singleton 成员值类型的 Service 标记检查

## 🎯 最终建议

### 修订后的核心规则（简化版）

1. **Service**：
   - 必须：非 Node 的 class
   - 推荐：暴露 interface
   - 允许：暴露 sealed class（特殊场景）

2. **Host Singleton 成员**：
   - 允许：成员值是 `this`（Host 自身）
   - 允许：成员值是普通类实例
   - 禁止：成员值的类型标记为 Service
   - 禁止：static 成员
   - 推荐：暴露 interface

3. **User Inject 成员**：
   - 允许：interface 或普通 class
   - 禁止：Node/Host/User/Scope
   - 禁止：static 成员

4. **服务唯一性**：
   - 同一接口在一个 Scope 内只能有一个提供者
   - Service 和 Host 不能提供同一接口
   - 多个 Service 不能提供同一接口

### 实施优先级

**P0 - 立即修复**：
1. ✅ 已修复：Scope 收集 Host 提供的服务
2. ✅ 已修复：[Singleton] 无参数时使用成员类型

**P1 - 高优先级**（影响正确性）：
3. 添加 GDI_M060: Host Singleton 成员值不能是 Service
4. 添加 GDI_D050: 服务类型冲突检测

**P2 - 中优先级**（完善约束）：
5. 添加 GDI_M051-053: Inject 类型不能是 Host/User/Scope
6. 添加 GDI_M054-055: 成员不能是 static

**P3 - 低优先级**（代码质量提示）：
7. 添加 GDI_W001: 暴露类型建议使用 interface (Warning)

这样的约束体系既保证了类型安全，又保持了足够的灵活性。
