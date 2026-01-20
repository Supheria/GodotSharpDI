using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Generator.Internal.Validation;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.DiBuild;

internal static class ScopeInfoCreator
{
    public static ScopeInfoCreateResult Create(
        ClassTypeInfo typeInfo,
        IReadOnlyDictionary<INamedTypeSymbol, ClassTypeInfo> typeInfoMap
    )
    {
        var creatorAndValidator = new CreatorAndValidator(typeInfo, typeInfoMap);
        var scopeInfo = creatorAndValidator.CreateScopeInfo();
        var diagnostics = creatorAndValidator.GetDiagnostics();
        var hasErrors = diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
        return new ScopeInfoCreateResult(hasErrors ? null : scopeInfo, diagnostics);
    }

    private class CreatorAndValidator
    {
        private readonly ImmutableArray<Diagnostic>.Builder _diagnostics;
        private readonly ClassTypeInfo _typeInfo;
        private readonly IReadOnlyDictionary<INamedTypeSymbol, ClassTypeInfo> _typeInfoMap;

        public CreatorAndValidator(
            ClassTypeInfo typeInfo,
            IReadOnlyDictionary<INamedTypeSymbol, ClassTypeInfo> typeInfoMap
        )
        {
            _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
            _typeInfo = typeInfo;
            _typeInfoMap = typeInfoMap;
        }

        public ImmutableArray<Diagnostic> GetDiagnostics()
        {
            return _diagnostics.ToImmutable();
        }

        private void Report(
            DiagnosticDescriptor descriptor,
            Location[]? additionalLocations,
            params object?[]? messageArgs
        )
        {
            _diagnostics.Add(
                Diagnostic.Create(
                    descriptor,
                    _typeInfo.IdentifierLocation,
                    additionalLocations,
                    messageArgs
                )
            );
        }

        private Location[] GetAttributesLocation(params AttributeData[] attributes)
        {
            var locations = new List<Location>(attributes.Length);
            foreach (var attr in attributes)
            {
                locations.Add(
                    AttributeHelper.GetAttributeLocation(attr, _typeInfo.IdentifierLocation)
                );
            }
            return locations.ToArray();
        }

        private ImmutableArray<ScopeServiceDescriptor> CollectScopeModules(AttributeData modules)
        {
            var scopeServices = ImmutableArray.CreateBuilder<ScopeServiceDescriptor>();

            var instantiateTypes = AttributeHelper.GetTypeArrayArgument(modules, "Instantiate");
            if (instantiateTypes.Length < 1)
            {
                Report(
                    DiagnosticDescriptors.ScopeModulesInstantiateEmpty,
                    GetAttributesLocation(modules),
                    _typeInfo.Symbol.Name
                );
            }
            else
            {
                foreach (var implType in instantiateTypes)
                {
                    if (
                        !_typeInfoMap.TryGetValue(implType, out var implInfo) || !implInfo.IsService
                    )
                    {
                        Report(
                            DiagnosticDescriptors.ScopeInstantiateMustBeService,
                            GetAttributesLocation(modules),
                            _typeInfo.Symbol.Name,
                            implType.Name
                        );
                        continue;
                    }
                    scopeServices.Add(
                        new ScopeServiceDescriptor(
                            ImplementType: implType,
                            ExposedServiceTypes: implInfo.ServiceExposedTypes,
                            Lifetime: implInfo.ServiceLifetime,
                            IsHostProvided: false
                        )
                    );
                }
            }

            var expectValues = AttributeHelper.GetTypeArrayArgument(modules, "Expect");
            if (expectValues.Length < 1)
            {
                Report(
                    DiagnosticDescriptors.ScopeModulesExpectEmpty,
                    GetAttributesLocation(modules),
                    _typeInfo.Symbol.Name
                );
            }
            else
            {
                foreach (var hostType in expectValues)
                {
                    if (!_typeInfoMap.TryGetValue(hostType, out var hostInfo) || !hostInfo.IsHost)
                    {
                        Report(
                            DiagnosticDescriptors.ScopeExpectMustBeHost,
                            GetAttributesLocation(modules),
                            _typeInfo.Symbol.Name,
                            hostType.Name
                        );
                        continue;
                    }
                    foreach (var providedService in hostInfo.HostSingletonServices)
                    {
                        scopeServices.Add(
                            new ScopeServiceDescriptor(
                                ImplementType: hostType,
                                ExposedServiceTypes: providedService.ExposedServiceTypes,
                                Lifetime: ServiceLifetime.Singleton,
                                IsHostProvided: true
                            )
                        );
                    }
                }
            }

            return scopeServices.ToImmutable();
        }

        private ImmutableArray<ScopeServiceDescriptor> CollectScopeModulesAuto()
        {
            var builder = ImmutableArray.CreateBuilder<ScopeServiceDescriptor>();
            return builder.ToImmutable();
        }

        public ScopeInfo? CreateScopeInfo()
        {
            if (_typeInfo.Modules is not null)
            {
                var services = CollectScopeModules(_typeInfo.Modules);
                return new ScopeInfo(_typeInfo.Symbol, _typeInfo.Namespace, services);
            }
            if (_typeInfo.AutoModules is not null)
            {
                var services = CollectScopeModulesAuto();
                return new ScopeInfo(_typeInfo.Symbol, _typeInfo.Namespace, services);
            }
            Report(
                DiagnosticDescriptors.ScopeLosesAttributeUnexpectedly,
                null,
                _typeInfo.Symbol.Name
            );
            return null;
        }
    }
}
