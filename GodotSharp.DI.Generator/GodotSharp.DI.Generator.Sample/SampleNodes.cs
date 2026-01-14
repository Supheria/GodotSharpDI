using System;
using System.Collections.Generic;
using Godot;
using GodotSharp.DI.Abstractions;

namespace GodotSharp.DI.Generator.Sample;

public interface IChunkGetter;

public interface IChunkGenerator;

[Host]
[User]
public partial class ChunkManager : Node, IChunkGetter, IChunkGenerator
{
    [Singleton(typeof(IChunkGetter), typeof(IChunkGenerator))]
    private ChunkManager Self => this;

    [Inject]
    private CellManager _cellManager;
}

[Host]
[User]
public partial class CellManager : Node, IServicesReady
{
    [Singleton]
    private CellManager Self => this;

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

[Transient(typeof(IFinder), typeof(ISearcher))]
public partial class PathFinder : IFinder, ISearcher;

[Modules(
    Instantiate = [typeof(DataBase), typeof(PathFinder)],
    Expect = [typeof(ChunkManager), typeof(CellManager)]
)]
public partial class Scope : Node, IScope
{
    [Inject]
    private IChunkGenerator _chunkGenerator;

    [Inject]
    private IChunkGetter _chunkGetter;

    public void RegisterService<T>(T instance)
        where T : notnull
    {
        throw new NotImplementedException();
    }

    public void ResolveService<T>(Action<T> onResolved)
        where T : notnull
    {
        throw new NotImplementedException();
    }
}
