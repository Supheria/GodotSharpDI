using System;
using System.Collections.Generic;
using Godot;
using GodotSharp.DI.Abstractions;

namespace GodotSharp.DI.Generator.Sample;

public interface IChunkGetter;

public interface IChunkGenerator;

public partial class ChunkManager : Node, IChunkGetter, IServiceHost, IChunkGenerator
{
    [SingletonService(typeof(IChunkGetter), typeof(IChunkGenerator))]
    private ChunkManager Self => this;

    [Dependency]
    private CellManager _cellManager;
}

public partial class CellManager : Node, IServiceHost, IServiceUser, IServiceAware
{
    [SingletonService]
    private CellManager Self => this;

    [Dependency]
    private IChunkGenerator _chunkGenerator;

    [Dependency]
    private IChunkGetter _chunkGetter;

    public void OnServicesReady()
    {
        throw new System.NotImplementedException();
    }
}

public interface IDataWriter;

public interface IDataReader;

[SingletonService(typeof(IDataWriter), typeof(IDataReader))]
public class DataBase : IDataWriter, IDataReader { }

public interface IFinder;

public interface ISearcher;

[TransientService(typeof(IFinder), typeof(ISearcher))]
public class PathFinder : IFinder, ISearcher;

// [ServiceModule(
//     Instantiate = [typeof(DataBase), typeof(PathFinder)],
//     Expect = [typeof(ChunkManager), typeof(CellManager)]
// )]
public partial class Scope : Node, IServiceScope
{
    [Dependency]
    private IChunkGenerator _chunkGenerator;

    [Dependency]
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
