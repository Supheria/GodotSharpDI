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

