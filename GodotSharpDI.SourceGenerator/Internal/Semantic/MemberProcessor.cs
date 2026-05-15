using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.Shared;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Semantic;

/// <summary>
/// Member processor - responsible for processing and validating class members
/// </summary>
internal sealed class MemberProcessor
{
    private readonly RawClassSemanticInfo _raw;
    private readonly TypeRole _role;
    private readonly CachedSymbols _symbols;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics;

    public MemberProcessor(
        RawClassSemanticInfo raw,
        TypeRole role,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        _raw = raw;
        _role = role;
        _symbols = symbols;
        _diagnostics = diagnostics;
    }

    public ImmutableArray<MemberInfo> Process()
    {
        var members = ImmutableArray.CreateBuilder<MemberInfo>();

        foreach (var member in _raw.Members)
        {
            var hasInject = member.HasAttribute(_symbols.InjectAttribute);
            var hasProvide = member.HasAttribute(_symbols.ProvideAttribute);

            if (!hasInject && !hasProvide)
                continue;

            // Check for conflicting attribute combinations
            if (hasInject && hasProvide)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.MemberConflictWithProvideAndInject,
                        member.Locations.FirstOrDefault() ?? _raw.Location,
                        member.Name
                    )
                );
                continue;
            }

            if (hasInject && _role != TypeRole.User && _role != TypeRole.Host)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.InjectMemberNotInServiceHostOrUser,
                        member.Locations.FirstOrDefault() ?? _raw.Location,
                        member.Name
                    )
                );
                continue;
            }

            if (hasProvide && _role != TypeRole.Host)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ProvideMemberNotInServiceOrHost,
                        member.Locations.FirstOrDefault() ?? _raw.Location,
                        member.Name
                    )
                );
                continue;
            }

            var memberInfo = ProcessSingleMember(member, hasInject, hasProvide);
            if (memberInfo != null)
                members.Add(memberInfo);
        }

        CheckMembersEmpty(members);

        // Validate WaitFor dependencies
        var validator = new WaitForValidator(members.ToImmutable(), _diagnostics);
        validator.ValidateAll();

        return members.ToImmutable();
    }

    private MemberInfo? ProcessSingleMember(ISymbol member, bool hasInject, bool hasProvide)
    {
        var location = member.Locations.FirstOrDefault() ?? Location.None;

        // Check static members
        if (member.IsStatic)
        {
            if (hasInject)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.InjectMemberIsStatic,
                        location,
                        member.Name
                    )
                );
                return null;
            }
            if (hasProvide)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ProvideMemberIsStatic,
                        location,
                        member.Name
                    )
                );
                return null;
            }
        }

        INamedTypeSymbol? memberType = null;
        MemberKind kind = MemberKind.None;
        bool isAsync = false;

        // Determine member type and kind
        if (member is IFieldSymbol field && field.Type is INamedTypeSymbol)
        {
            memberType = (INamedTypeSymbol)field.Type;

            if (hasInject)
            {
                if (field.IsReadOnly)
                {
                    _diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.InjectMemberNotAssignable,
                            location,
                            member.Name
                        )
                    );
                    return null;
                }
                kind = MemberKind.InjectField;
            }
            else if (hasProvide)
            {
                kind = MemberKind.ProvideField;
            }
        }
        else if (member is IPropertySymbol property && property.Type is INamedTypeSymbol)
        {
            memberType = (INamedTypeSymbol)property.Type;

            if (hasInject)
            {
                if (property.SetMethod == null)
                {
                    _diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.InjectMemberNotAssignable,
                            location,
                            member.Name
                        )
                    );
                    return null;
                }
                kind = MemberKind.InjectProperty;
            }
            else if (hasProvide)
            {
                if (property.GetMethod == null)
                {
                    _diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.ProvidePropertyNotAccessible,
                            location,
                            member.Name
                        )
                    );
                    return null;
                }
                kind = MemberKind.ProvideProperty;

                // Check if it is Task<T>
                isAsync = _symbols.IsAsyncType(property.Type);
                if (isAsync && property.Type is INamedTypeSymbol taskType && taskType.IsGenericType)
                {
                    // The T in Task<T> is the actual type
                    memberType = taskType.TypeArguments[0] as INamedTypeSymbol;
                }
            }
        }
        else if (member is IMethodSymbol method && hasProvide)
        {
            // [Provide] can be used on methods
            if (method.ReturnsVoid)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ProvideMethodReturnVoid,
                        location,
                        member.Name
                    )
                );
                return null;
            }

            if (method.Parameters.Length > 0)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ProvideMethodNotParameterless,
                        location,
                        member.Name
                    )
                );
                return null;
            }

            kind = MemberKind.ProvideMethod;
            isAsync = _symbols.IsAsyncType(method.ReturnType);

            if (method.ReturnType is INamedTypeSymbol returnType)
            {
                memberType = returnType;
                if (isAsync && returnType.IsGenericType)
                {
                    // The T in Task<T> is the actual type
                    memberType = returnType.TypeArguments[0] as INamedTypeSymbol;
                }
            }
        }

        if (memberType == null)
            return null;

        // Validate Inject members
        bool hasFailureCallback = false;
        bool hasReadyCallback = false;
        if (hasInject)
        {
            if (!ValidateInjectMemberType(memberType, member, location))
                return null;
            // Read FailureCallback and ReadyCallback attributes
            var injectAttr = member.GetAttribute(_symbols.InjectAttribute);
            foreach (var namedArg in injectAttr!.NamedArguments)
            {
                if (
                    namedArg.Key == ShortNames.FailureCallback
                    && namedArg.Value.Value is bool failureCallbackValue
                )
                {
                    hasFailureCallback = failureCallbackValue;
                }
                else if (
                    namedArg.Key == ShortNames.ReadyCallback
                    && namedArg.Value.Value is bool readyCallbackValue
                )
                {
                    hasReadyCallback = readyCallbackValue;
                }
            }
        }

        // Validate and extract Provide members
        var exposedTypes = ImmutableArray<INamedTypeSymbol>.Empty;
        var waitFor = ImmutableArray<string>.Empty;

        if (hasProvide)
        {
            if (!ValidateProvideMemberType(memberType, member, location))
                return null;

            // Extract information from Provide attribute
            var provideAttr = member.GetAttribute(_symbols.ProvideAttribute);
            if (provideAttr != null)
            {
                // Extract ServiceType
                exposedTypes = AttributeHelper.GetMemberExposedTypes(member, _symbols);

                // Extract WaitFor
                foreach (var namedArg in provideAttr.NamedArguments)
                {
                    if (
                        namedArg.Key == ShortNames.WaitFor
                        && namedArg.Value.Kind == TypedConstantKind.Array
                    )
                    {
                        var waitForList = ImmutableArray.CreateBuilder<string>();
                        foreach (var element in namedArg.Value.Values)
                        {
                            if (
                                element.Value is string fieldName
                                && !string.IsNullOrWhiteSpace(fieldName)
                            )
                            {
                                waitForList.Add(fieldName);
                            }
                        }
                        if (waitForList.Count > 0)
                        {
                            waitFor = waitForList.ToImmutable();
                        }
                    }
                }
            }

            ValidateProvideMemberExposedTypes(memberType, member, location, exposedTypes);
        }

        return new MemberInfo(
            Symbol: member,
            Location: location,
            Kind: kind,
            MemberType: memberType,
            ExposedTypes: exposedTypes,
            HasFailureCallback: hasFailureCallback,
            HasReadyCallback: hasReadyCallback,
            WaitFor: waitFor,
            IsAsync: isAsync
        );
    }

    private bool ValidateInjectMemberType(ITypeSymbol memberType, ISymbol member, Location location)
    {
        // Must be an interface or concrete class
        if (!memberType.IsInterfaceOrConcreteClass())
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectMemberTypeIsInvalid,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // Cannot be an open generic type (closed generics are allowed, e.g., List<int>)
        if (memberType is INamedTypeSymbol namedType && namedType.IsUnboundGenericType())
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectMemberTypeCannotBeGeneric,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // Can be a Host type, but not recommended and produces warning
        if (_symbols.IsHostType(memberType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectMemberIsHostType,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return true;
        }

        // Cannot be User type
        if (_symbols.IsUserType(memberType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectMemberIsUserType,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // Cannot be Scope type
        if (_symbols.ImplementsIScope(memberType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectMemberIsScopeType,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // Can be non-interface, but not recommended and produces warning
        if (memberType.TypeKind != TypeKind.Interface)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectMemberTypeShouldBeInterface,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
        }

        return true;
    }

    private bool ValidateProvideMemberType(
        ITypeSymbol memberType,
        ISymbol member,
        Location location
    )
    {
        // Must be an interface or concrete class
        if (!memberType.IsInterfaceOrConcreteClass())
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ProvideMemberTypeIsInvalid,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // Cannot be an open generic type (closed generics are allowed, e.g., List<int>)
        if (memberType is INamedTypeSymbol namedType && namedType.IsUnboundGenericType())
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ProvideMemberTypeCannotBeGeneric,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // Check Host type
        if (_symbols.IsHostType(memberType))
        {
            // Host types other than self are not allowed
            if (!SymbolEqualityComparer.Default.Equals(memberType, _raw.Symbol))
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ProvideMemberIsHostType,
                        location,
                        member.Name,
                        memberType.ToDisplayString()
                    )
                );
                return false;
            }
            return true;
        }

        // Cannot be User type
        if (_symbols.IsUserType(memberType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ProvideMemberIsUserType,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // Cannot be Scope type
        if (_symbols.ImplementsIScope(memberType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ProvideMemberIsScopeType,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        return true;
    }

    private void ValidateProvideMemberExposedTypes(
        ITypeSymbol memberType,
        ISymbol member,
        Location location,
        ImmutableArray<INamedTypeSymbol> exposedTypes
    )
    {
        foreach (var exposedType in exposedTypes)
        {
            // Cannot be an open generic type (closed generics are allowed, e.g., IList<int>)
            if (exposedType.IsUnboundGenericType())
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ProvideMemberExposedTypeCannotBeGeneric,
                        location,
                        member.Name,
                        exposedType.ToDisplayString()
                    )
                );
            }

            // Can be non-interface, but not recommended and produces warning
            if (exposedType.TypeKind != TypeKind.Interface)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ProvideMemberExposedTypeShouldBeInterface,
                        location,
                        exposedType.ToDisplayString()
                    )
                );
            }

            // Check if the exposed interface is implemented
            if (exposedType.TypeKind == TypeKind.Interface)
            {
                if (!memberType.ImplementsInterface(exposedType))
                {
                    _diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.ProvideMemberExposedTypeNotImplemented,
                            location,
                            member.Name,
                            exposedType.ToDisplayString(),
                            memberType.ToDisplayString()
                        )
                    );
                }
            }
            // Check if it is an inheritance relationship
            else if (exposedType.TypeKind == TypeKind.Class)
            {
                if (
                    !SymbolEqualityComparer.Default.Equals(memberType, exposedType)
                    && !memberType.InheritsFrom(exposedType)
                )
                {
                    _diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.ProvideMemberExposedTypeNotImplemented,
                            location,
                            member.Name,
                            exposedType.ToDisplayString(),
                            memberType.ToDisplayString()
                        )
                    );
                }
            }
        }
    }

    private void CheckMembersEmpty(ImmutableArray<MemberInfo>.Builder memberInfos)
    {
        if (_role == TypeRole.Host)
        {
            // Host needs at least one Provide member
            var provideMembers = memberInfos.Where(m => m.IsProvideMember).ToArray();
            if (provideMembers.Length == 0)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.HostMissingProvideMember,
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
            }
        }

        if (_role == TypeRole.User)
        {
            var injectMembers = memberInfos.Where(m => m.IsInjectMember).ToArray();
            if (injectMembers.Length == 0)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.UserMissingInjectMember,
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
            }
        }
    }
}
