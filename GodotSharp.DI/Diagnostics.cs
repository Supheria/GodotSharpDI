namespace GodotSharp.DI;

//
// GDI001（错误）
//

[Singleton(typeof(IDataWriter))] // GDI001：Problem01A 没有继承或实现 [Singleton] 所指定的 IDataWriter。
public sealed partial class Problem01A { }

[Transient(typeof(IDataReader))] // GDI001：Problem01B 没有继承或实现 [Transient] 所指定的 IDataReader。
public sealed partial class Problem01B { }

[Host]
public sealed partial class Problem01C
{
    [Singleton(typeof(IDataWriter))] // GDI001：Problem01C 没有继承或实现 [Singleton] 所指定的 IDataWriter。
    private Problem01C Self => this;

    [Singleton(typeof(IDataWriter))] // GDI001：IDataReader 没有继承或实现 [Singleton] 所指定的 IDataWriter。
    private IDataReader _reader;
}

//
// GDI002（错误）
//

[Singleton]
public sealed partial class Problem02A // GDI002：Problem02A 标记为 [Singleton] 且定义了构造函数，但没有将任何构造函数标记为 [InjectConstructor]。
{
    public Problem02A() { }
}

[Transient]
public sealed partial class Problem02B // GDI002：Problem02B 标记为 [Singleton] 且定义了构造函数，但没有将任何构造函数标记为 [InjectConstructor]。
{
    public Problem02B() { }
}

[Singleton]
public sealed partial class Problem02C { } // 不会触发 GDI002

[Transient]
public sealed partial class Problem02D { } // 不会触发 GDI002

//
// GDI003（错误）
//

[Singleton]
[Transient]
public sealed partial class Problem03A { } // GDI003：Problem03A 不能同时标记为 [Singleton] 和 [Transient]。

[Host]
[Singleton]
public sealed partial class Problem03B { } // GDI003：Problem03B 不能同时标记为 [Host] 和 [Singleton]。

[Host]
[Transient]
public sealed partial class Problem03C { } // GDI003：Problem03C 不能同时标记为 [Host] 和 [Transient]。

[User]
[Singleton]
public sealed partial class Problem03D { } // GDI003：Problem03D 不能同时标记为 [User] 和 [Singleton]。

[User]
[Transient]
public sealed partial class Problem03E { } // GDI003：Problem03E 不能同时标记为 [User] 和 [Transient]。

//
// GDI004（错误）
//

[Singleton]
public sealed class Problem04A { } // GDI004：Problem04A 必须是 partial 才能标记为 [Singleton]。

[Transient]
public sealed class Problem04B { } // GDI004：Problem04B 必须是 partial 才能标记为 [Transient]。

[Host]
public sealed class Problem04C { } // GDI004：Problem04C 必须是 partial 才能标记为 [Host]。

[User]
public sealed class Problem04D { } // GDI004：Problem04D 必须是 partial 才能标记为 [User]。

//
// GDI005（警告）
//

[Host]
public sealed class Problem05A { } // GDI005：Problem05A 标记为 [Host] 但从未调用过 AttachHostServices()。

[User]
public sealed class Problem05B { } // GDI005：Problem05B 标记为 [User] 但从未调用过 AttachUserDependencies()。

//
// GDI006（警告）
//

[Host]
public sealed class Problem06A { } // GDI006：Problem06A 标记为 [Host] 但未将任何字段或属性标记为 [Singleton]。

[User]
public sealed class Problem06B { } // GDI006：Problem06B 标记为 [User] 但未将任何字段或属性标记为 [Inject]。

//
// GDI007（错误）
//

public sealed class Problem07A
{
    [Singleton] // GDI007：Problem07A 必须标记为 [Host] 才能将字段或属性标记为 [Singleton]
    private Problem07A Self => this;
}

public sealed class Problem07B
{
    [Inject] // GDI007：Problem07B 必须标记为 [User] 才能将字段或属性标记为 [Inject]
    private IDataReader _reader;
}

//
// GDI008（警告）（针对节点类型 user）
//

[User]
public sealed class Problem08A : Godot.Node
{
    [Inject]
    private IDataReader _reader;

    public override void _Ready()
    {
        base._Ready();

        _reader.Read(); // GDI008：避免在 _Ready() 中使用标记为 [Inject] 的字段或属性。实现 IServicesReady 可以确保在 OnServicesReady() 中所有依赖的字段或属性都已经完成初始化。
    }
}

//
// GDI009（错误）
//

[Host]
public sealed partial class Host09;

public sealed partial class Problem09A
{
    private Host09 _tools; // GDI009：Problem09A 需要标记为 [Host] 才能包含标记为 [Host] 的类型（[Host]具有传播性）。
}

[User]
public sealed partial class User09;

public sealed partial class Problem09B
{
    private User09 _tools; // GDI009：Problem09B 需要标记为 [User] 才能包含标记为 [User] 的类型（[User]具有传播性）。
}

//
// GDI010（错误）
//

[Host]
public sealed partial class Host10 : Godot.Node;

public sealed partial class Problem10A
{
    private Host09 _tools; // GDI010：不能包含标记为 [Host] 的节点类型。
}

[User]
public sealed partial class User10 : Godot.Node;

public sealed partial class Problem10B
{
    private User09 _tools; // GDI010：不能包含标记为 [User] 的节点类型。
}

//
// GDI011（错误）
//

[Host]
public sealed partial class Host11;

[Host]
public sealed partial class Problem1A
{
    private Host11 _tools; // GDI011：从未实例化过标记为 [Host] 的类型，这会引发 null 异常。
}

[User]
public sealed partial class User11;

[User]
public sealed partial class Problem1B
{
    private User11 _tools; // GDI011：从未实例化过标记为 [User] 的类型，这会引发 null 异常。
}

//
// GDI012 (错误）
//

[AutoModules]
public sealed class Problem12A : Godot.Node, IScope { } // GDI012：Problem12A 必须是 partial 才能实现接口 IScope。

//
// GDI013 (错误）
//

[AutoModules]
public sealed partial class Problem13A : IScope { } // GDI013：Problem13A 必须是 Node 类型或任何 Node 派生类型才能实现 IScope

//
// GDI014 (错误）
//
public sealed partial class Problem14A : IScope { } // GDI014：Problem14A 实现接口 IScope, 必须标记为 [Modules] 或 [AutoModules]

//
// GDI015 (警告）
//

[Host]
public sealed partial class Host15 { }

public sealed partial class NodeHost15
{
    private Host15 _tools = new();
}

[Modules(Expect = [typeof(NodeHost15)])] // GDI015：Problem15A 约束了 Host 类型 NodeHost15 ，但未约束其内部 Host 类型 Host15
public sealed partial class Problem15A { }

//
// GDI016（错误）
//

[Singleton] // GDI016：Problem16A 实现接口 IScope, 不能标记为 [Singleton]
[Transient] // GDI016：Problem16A 实现接口 IScope, 不能标记为 [Transient]
[Host] // GDI016：Problem16A 实现接口 IScope, 不能标记为 [Host]
[User] // GDI016：Problem16A 实现接口 IScope, 不能标记为 [User]
[AutoModules]
public sealed partial class Problem16A : IScope { }

//
// GDI017（错误）
//

// 首先存在
[Singleton(typeof(IDataWriter))]
public sealed partial class Service17A : IDataWriter { }

// 然后存在
[Singleton(typeof(IDataWriter))]
public sealed partial class Service17B : IDataWriter { }

// 或者
[Singleton]
public sealed partial class Service17C : IDataWriter { }

// 或者
[Host]
public sealed partial class Host17A : IDataWriter
{
    [Singleton(typeof(IDataWriter))]
    private Host17A Self => this;
}

// 或者
[Host]
public sealed partial class Host17B : IDataWriter
{
    [Singleton]
    private IDataWriter Self => this;
}

// 或者
[Host]
public sealed partial class Host17C : IDataWriter
{
    [Singleton]
    private Host17C Self => this;
}

[AutoModules] // GDI017：同一命名空间中存在多个服务声明为 IDataWriter 类型
public sealed partial class Scope17 : IScope { }

//
// GDI018（警告）
//
public sealed partial class Problem18A : IServicesReady { } // GDI018：Problem18A 实现了 IServicesReady 但没有标记为 [User]
