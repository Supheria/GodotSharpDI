using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

public interface IChunkGetter;

public interface IChunkGenerator;

public interface ICellGenerator;

[Host]
public partial class ChunkManager : Node, IChunkGenerator, IChunkGetter
{
    [Singleton(typeof(IChunkGetter), typeof(IChunkGenerator))]
    private ChunkManager Self => this;
}

public class CellService : ICellGenerator, IChunkGenerator;

[Host, User]
public partial class CellManager : Node, ICellGenerator, IServicesReady
{
    [Singleton(typeof(ICellGenerator))]
    private CellManager Self => this;

    [Inject]
    private IChunkGenerator _chunkGenerator;

    [Inject]
    private IChunkGetter _chunkGetter;

    public void OnServicesReady() { }
}

public interface IDataWriter;

public interface IDataReader;

[Singleton(typeof(IDataWriter), typeof(IDataReader))]
public partial class DataBase : IDataWriter, IDataReader { }

public interface IFinder;

public interface ISearcher;

[Singleton(typeof(IFinder), typeof(ISearcher))]
public partial class PathFinderFactory : IFinder, ISearcher
{
    [InjectConstructor]
    private PathFinderFactory(IDataWriter writer, IDataReader reader) { }

    private PathFinderFactory(IDataWriter writer) { }
}

[Modules(
    Services = [typeof(DataBase), typeof(PathFinderFactory)],
    Hosts = [typeof(ChunkManager), typeof(CellManager)]
)]
public partial class Scope : Node, IScope
{
    // [Inject]
    // private IChunkGenerator _chunkGenerator;
    //
    // [Inject]
    // private IChunkGetter _chunkGetter;
    public Scope() { }
}

// [User]
// public partial class OtherUser { }
//
// [User]
// public partial class MyUser : Node
// {
//     [Inject]
//     private OtherUser _other = new();
// }

// [User]
// public partial class TestUser : Node
// {
//     public void BadMethod()
//     {
//         var scope = _serviceScope;
//     }
// }

// [Modules(Services = [typeof(DataBase), typeof(PathFinderFactory)])]
// public partial class TestScope : Node, IScope
// {
//     public void BadMethod()
//     {
//         var count = _services.Count;
//     }
// }
//
// [User]
// public partial class TestUser : Node
// {
//     public void BadMethod()
//     {
//         AttachToScope();
//     }
// }
