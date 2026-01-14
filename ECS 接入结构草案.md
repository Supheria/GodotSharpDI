# ECS 接入结构草案

## **目标**

- 用 DI 管理 ECS System / World
- 用 Node 驱动 ECS 更新与渲染

## **定义 ECS System / World 为 Service**

```c#
[Singleton]
public partial class MovementSystem { ... }

[Singleton]
public partial class GameWorld
{
    public GameWorld(MovementSystem movement) { ... }
    public void Update(double delta) { ... }
}
```

## **在 Scope 中声明模块**

```c#
[Modules(Instantiate = [typeof(MovementSystem), typeof(GameWorld)])]
public partial class GameScope : Node, IScope { }
```

在 Node 中注入 ECS World

```c#
[User]
public partial class GameScene : Node, IServicesReady
{
    [Inject] private GameWorld _world;

    public void OnServicesReady() { }

    public override void _Process(double delta)
    {
        _world.Update(delta);
    }
}
```

## 原则总结

- System / World = Service
- Entity / Component = 纯数据，不进 DI
- Node = User，负责输入与渲染