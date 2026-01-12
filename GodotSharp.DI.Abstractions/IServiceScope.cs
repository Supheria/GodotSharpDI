namespace GodotSharp.DI.Abstractions;

public interface IServiceScope
{
    void RegisterService<T>(T instance)
        where T : notnull;
    void ResolveService<T>(Action<T> onResolved)
        where T : notnull;
}
