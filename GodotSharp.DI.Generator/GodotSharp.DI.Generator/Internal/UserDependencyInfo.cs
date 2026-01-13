using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed class UserDependencyInfo
{
    public bool IsNode { get; }
    public INamedTypeSymbol UserType { get; }
    public List<(string Name, INamedTypeSymbol Type)> Dependencies { get; }
    public bool IsServiceAware { get; }

    public UserDependencyInfo(
        bool isNode,
        INamedTypeSymbol userType,
        List<(string, INamedTypeSymbol)> dependencies,
        bool isServiceAware
    )
    {
        IsNode = isNode;
        UserType = userType;
        Dependencies = dependencies;
        IsServiceAware = isServiceAware;
    }
}
