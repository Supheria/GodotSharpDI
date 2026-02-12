using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Helpers;

/// <summary>
/// 缓存常用的符号引用
/// </summary>
internal sealed class CachedSymbols
{
    // DI
    public INamedTypeSymbol? HostAttribute { get; }
    public INamedTypeSymbol? UserAttribute { get; }
    public INamedTypeSymbol? InjectAttribute { get; }
    public INamedTypeSymbol? ProvideAttribute { get; }
    public INamedTypeSymbol? ModulesAttribute { get; }
    public INamedTypeSymbol? IScope { get; }
    public INamedTypeSymbol? IDependenciesResolved { get; }
    public INamedTypeSymbol? GodotNode { get; }

    // System
    public INamedTypeSymbol? IDisposable { get; }
    public INamedTypeSymbol? GenericTask { get; }
    public INamedTypeSymbol? GenericValueTask { get; }

    public CachedSymbols(Compilation compilation)
    {
        // DI
        ProvideAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.ProvideAttribute);
        HostAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.HostAttribute);
        UserAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.UserAttribute);
        InjectAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.InjectAttribute);
        ModulesAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.ModulesAttribute);
        IScope = compilation.GetTypeByMetadataName(TypeNamesFull.IScope);
        IDependenciesResolved = compilation.GetTypeByMetadataName(
            TypeNamesFull.IDependenciesResolved
        );
        GodotNode = compilation.GetTypeByMetadataName(TypeNamesFull.GodotNode);

        // System
        IDisposable = compilation.GetTypeByMetadataName(TypeNamesFull.IDisposable);
        GenericTask = compilation.GetTypeByMetadataName(TypeNamesFull.GenericTask);
        GenericValueTask = compilation.GetTypeByMetadataName(TypeNamesFull.GenericValueTask);
    }

    public bool IsNode(ITypeSymbol type)
    {
        if (GodotNode is null)
            return false;
        // 使用 SymbolExtensions 的 InheritsFrom 方法
        return SymbolEqualityComparer.Default.Equals(type, GodotNode)
            || type.InheritsFrom(GodotNode);
    }

    public bool ImplementsIScope(ITypeSymbol type)
    {
        if (IScope is null)
            return false;
        // 使用 SymbolExtensions 的 ImplementsInterface 方法
        return type.ImplementsInterface(IScope);
    }

    public bool ImplementsIDependenciesResolved(ITypeSymbol type)
    {
        if (IDependenciesResolved is null)
            return false;
        // 使用 SymbolExtensions 的 ImplementsInterface 方法
        return type.ImplementsInterface(IDependenciesResolved);
    }

    public bool IsHostType(ITypeSymbol type)
    {
        // 使用 SymbolExtensions 的 HasAttribute 方法
        return type.HasAttribute(HostAttribute);
    }

    public bool IsUserType(ITypeSymbol type)
    {
        // 使用 SymbolExtensions 的 HasAttribute 方法
        return type.HasAttribute(UserAttribute);
    }

    /// <summary>
    /// 检查类型是否是 Task&lt;T&gt; (ValueTask&lt;T&gt;)
    /// </summary>
    public bool IsAsyncType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
            return false;

        // 比较原始定义（不受泛型参数影响）
        var original = named.OriginalDefinition;

        return SymbolEqualityComparer.Default.Equals(original, GenericTask)
            || SymbolEqualityComparer.Default.Equals(original, GenericValueTask);
    }
}
