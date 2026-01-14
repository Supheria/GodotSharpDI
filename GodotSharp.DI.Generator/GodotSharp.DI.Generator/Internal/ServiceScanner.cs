using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class ServiceScanner
{
    public static ServiceTypeInfo? Analyze(INamedTypeSymbol type, SymbolCache symbolCache)
    {
        var isSingleton = SymbolHelper.HasAttribute(type, symbolCache.SingletonAttribute);
        var isTransient = SymbolHelper.HasAttribute(type, symbolCache.TransientAttribute);
        if (isSingleton && isTransient)
        {
            return null;
        }
        if (!isSingleton && !isTransient)
        {
            return null;
        }
        var attr = isSingleton
            ? symbolCache.SingletonAttribute
            : symbolCache.TransientAttribute;
        var exposedTypes = SymbolHelper.GetServiceTypesFromAttribute(type, attr);
        return new ServiceTypeInfo(exposedTypes, isSingleton, isTransient);
    }
}
