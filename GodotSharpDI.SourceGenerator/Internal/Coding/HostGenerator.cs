using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.Shared;
using GodotSharpDI.SourceGenerator.Internal.Coding.Shared;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Host code generator (refactored version)
/// Supports independent WaitFor remaining count for each Provide member
/// </summary>
internal static class HostGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        // Generate base DI file
        NodeLifeCycleGenerator.Generate(context, node.ValidatedTypeInfo);

        // Generate dependency injection code
        InjectionGenerator.Generate(context, node);

        // Generate Host specific code
        GenerateHostSpecific(context, node);
    }

    /// <summary>
    /// Generate Host specific code (ProvideServices)
    /// </summary>
    public static void GenerateHostSpecific(SourceProductionContext context, TypeNode node)
    {
        var validatedType = node.ValidatedTypeInfo;

        // Separate inject members and provide members
        var injectMembers = validatedType.Members.Where(m => m.IsInjectMember).ToImmutableArray();
        var provideMembers = validatedType.Members.Where(m => m.IsProvideMember).ToImmutableArray();

        var f = new CodeFormatter();

        f.BeginClassDeclaration(validatedType, out var fileName);
        {
            GenerateProvideServices(f, validatedType, injectMembers, provideMembers);
            f.AppendLine();

            // Generate async provider methods
            var asyncMembers = provideMembers.Where(m => m.IsAsync).ToImmutableArray();
            if (!asyncMembers.IsEmpty)
            {
                ServiceProvisionPhase.GenerateAsyncProviderMethods(
                    f,
                    asyncMembers,
                    validatedType.Symbol.Name
                );
            }
        }
        f.EndClassDeclaration();

        context.AddSource($"{fileName}.DI.Provide.g.cs", f.ToString());
    }

    /// <summary>
    /// Generate ProvideServices method
    /// Core logic:
    /// 1. If there are Inject members and IDependenciesResolved is implemented, inject dependencies first (without waiting for completion)
    /// 2. Process each Provide member independently:
    ///    - If it has WaitFor, wait for WaitFor dependencies
    ///    - Otherwise, provide service directly
    /// </summary>
    private static void GenerateProvideServices(
        CodeFormatter f,
        ValidatedTypeInfo validatedType,
        ImmutableArray<MemberInfo> injectMembers,
        ImmutableArray<MemberInfo> provideMembers
    )
    {
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void ProvideServices()");
        f.BeginBlock();
        {
            f.AppendLine($"var {GlobalNames.LocalScope} = GetParentScope();");
            f.AppendLine($"if ({GlobalNames.LocalScope} is null)");
            f.BeginBlock();
            {
                f.AppendLine(
                    $"{GlobalNames.ErrorReporter}.ReportParentScopeNotFound("
                    + $"\"{validatedType.Symbol.Name}\");"
                );
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            // Core logic: Determine processing method based on whether there are Inject members and whether IDependenciesResolved is implemented
            if (!injectMembers.IsEmpty && validatedType.ImplementsIDependenciesResolved)
            {
                // Has Inject members and implements IDependenciesResolved - use dependency tracking
                GenerateWithDependencyTracking(f, validatedType, injectMembers, provideMembers);
            }
            else
            {
                // No Inject members - provide services directly
                GenerateDirectProvision(
                    f,
                    validatedType.Members,
                    provideMembers,
                    validatedType.Symbol.Name
                );
            }
        }
        f.EndBlock();
    }

    /// <summary>
    /// Case with dependency tracking (implements IDependenciesResolved)
    /// WaitFor uses TCS (instance field) mechanism to wait for Inject members to be ready, no need to register ResolveDependency here.
    /// Phase 1 removed: Inject members are handled uniformly by ResolveDependencies(), avoiding duplicate callbacks.
    /// </summary>
    private static void GenerateWithDependencyTracking(
        CodeFormatter f,
        ValidatedTypeInfo validatedType,
        ImmutableArray<MemberInfo> injectMembers,
        ImmutableArray<MemberInfo> provideMembers
    )
    {
        // WaitFor dependencies communicate with ResolveDependencies() through TCS instance field,
        // only need to generate Provide phase code here
        f.AppendLine(GeneratedStrings.Phase23Comment);
        GenerateDirectProvision(
            f,
            validatedType.Members,
            provideMembers,
            validatedType.Symbol.Name
        );
    }

    /// <summary>
    /// Provide services directly (each Provide member is processed independently, may have WaitFor)
    /// </summary>
    private static void GenerateDirectProvision(
        CodeFormatter f,
        ImmutableArray<MemberInfo> allMembers,
        ImmutableArray<MemberInfo> provideMembers,
        string providerTypeName
    )
    {
        var waitForMembers = new List<(MemberInfo member, Action callback)>();

        foreach (var member in provideMembers)
        {
            f.AppendLine(string.Format(GeneratedStrings.MemberSeparatorFmt, member.Symbol.Name));

            if (member.HasWaitFor)
            {
                // Collect members that need to generate local functions
                Action callback = () =>
                {
                    ServiceProvisionPhase.GenerateMemberProvide(
                        f,
                        member,
                        GlobalNames.LocalScope,
                        providerTypeName,
                        instancePrefix: "",
                        inAsyncContext: true
                    );
                };

                waitForMembers.Add((member, callback));

                // Only generate listener code, not local functions
                WaitForPhase.GenerateForMember(
                    f,
                    member,
                    allMembers,
                    GlobalNames.LocalScope,
                    onAllResolved: callback
                );
            }
            else
            {
                // Provide directly
                ServiceProvisionPhase.GenerateMemberProvide(
                    f,
                    member,
                    GlobalNames.LocalScope,
                    providerTypeName,
                    instancePrefix: "",
                    inAsyncContext: false
                );
            }

            f.AppendLine();
        }

        // Add a unified return at the end of the method (optional)
        if (waitForMembers.Any())
        {
            f.AppendLine("return;");
            f.AppendLine();

            // Generate all local function definitions
            foreach (var (member, callback) in waitForMembers)
            {
                WaitForPhase.GenerateLocalFunction(f, member, callback);
            }
        }
    }
}
