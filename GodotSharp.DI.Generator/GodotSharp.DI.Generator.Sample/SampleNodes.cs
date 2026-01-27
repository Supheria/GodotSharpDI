using System;
using System.Collections.Generic;
using Godot;
using GodotSharp.DI.Abstractions;

namespace GodotSharp.DI.Generator.Sample;

public interface IChunkGetter;

public interface IChunkGenerator;

public interface ICellGenerator;

[User]
public partial class PureUser : IServicesReady
{
    [Inject]
    private ICellGenerator _cellManager;

    public void Test() { }

    public void OnServicesReady()
    {
        throw new NotImplementedException();
    }
}

[Host]
[User]
public partial class ChunkManager : Node, IChunkGenerator, IChunkGetter
{
    [Singleton]
    private IChunkGetter Self => this;

    [Singleton]
    private IChunkGenerator Self2 => this;

    [Inject]
    private ICellGenerator _cellManager;
}

[Host, User]
public partial class CellManager : Node, ICellGenerator, IServicesReady
{
    [Singleton]
    private ICellGenerator Self => this;

    [Inject]
    private IChunkGenerator _chunkGenerator;

    [Inject]
    private IChunkGetter _chunkGetter;

    private readonly PureUser _pureUser = new();

    public void OnServicesReady() { }
}

public interface IDataWriter;

public interface IDataReader;

[Singleton(typeof(IDataWriter), typeof(IDataReader))]
public partial class DataBase : IDataWriter, IDataReader { }

public interface IFinder;

public interface ISearcher;

[Transient(typeof(IFinder), typeof(ISearcher))]
public partial class PathFinder : IFinder, ISearcher
{
    [InjectConstructor]
    private PathFinder(IDataWriter writer, IDataReader reader) { }

    private PathFinder(IDataWriter writer) { }
}

[Modules(
    Services = [typeof(DataBase), typeof(PathFinder)],
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

[User]
public partial class OtherUser { }

[User]
public partial class MyUser : Node
{
    [Inject]
    private OtherUser _other = new();
}
