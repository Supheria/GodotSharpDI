using System;
using System.Collections.Generic;
using Godot;
using GodotSharp.DI.Abstractions;

namespace GodotSharp.DI.Generator.Sample;

public interface IChunkGetter;

public interface IChunkGenerator;

public interface ICellGenerator;

[Host]
[User]
public partial class ChunkManager : Node, IChunkGetter, IChunkGenerator
{
    [Singleton]
    private IChunkGetter Self => this;

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

    public void OnServicesReady()
    {
        throw new System.NotImplementedException();
    }
}

public interface IDataWriter;

public interface IDataReader;

[Singleton(typeof(IDataWriter), typeof(IDataReader))]
public partial class DataBase : IDataWriter, IDataReader { }

public interface IFinder;

public interface ISearcher;

[Singleton(typeof(IFinder), typeof(ISearcher))]
public partial class PathFinder : IFinder, ISearcher
{
    [InjectConstructor]
    private PathFinder(IDataWriter writer, IDataReader reader) { }

    // private PathFinder(IDataWriter writer) { }
}

[Modules(
    Instantiate = [typeof(DataBase), typeof(PathFinder)],
    Expect = [typeof(ChunkManager), typeof(CellManager)]
)]
public partial class Scope : Node, IScope
{
    // [Inject]
    // private IChunkGenerator _chunkGenerator;
    //
    // [Inject]
    // private IChunkGetter _chunkGetter;
}
