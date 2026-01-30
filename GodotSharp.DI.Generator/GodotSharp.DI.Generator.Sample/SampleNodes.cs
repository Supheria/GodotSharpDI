using System;
using System.Collections.Generic;
using Godot;
using GodotSharp.DI.Abstractions;

namespace GodotSharp.DI.Generator.Sample;

public interface IChunkGetter;

public interface IChunkGenerator;

public interface ICellGenerator;

[Host]
public partial class ChunkManager : Node, IChunkGenerator, IChunkGetter
{
    [Singleton(typeof(IChunkGetter), typeof(IChunkGenerator))]
    private ChunkManager Self => this; // ChunkManager 没有实现 IChunkGetter、IChunkGenerator
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

[Singleton(typeof(IDataWriter), typeof(IDataReader))] // DataBase 没有实现 IDataWriter、IDataReader
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
