using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed class UserDescriptor
{
    public bool IsNode { get; }
    public INamedTypeSymbol UserType { get; }
    public List<(string Name, INamedTypeSymbol Type)> Dependencies { get; }
    public bool IsServiceAware { get; }

    public UserDescriptor(
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
