using System;

namespace GodotSharpDI.Abstractions;

/// <summary>
/// Represents a DI scope node in the Godot scene tree.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>
///     <see cref="ProvideService{TImpl}"/> is called by <c>[Host]</c> nodes to register a
///     service instance. Pass <c>null</c> to signal that service creation failed.
///   </item>
///   <item>
///     <see cref="ResolveDependency{TExposed}"/> is called by <c>[User]</c> and <c>[Host]</c>
///     nodes to request a service. The callback receives <c>null</c> when the service is
///     unavailable.
///   </item>
/// </list>
/// </remarks>
public interface IScope
{
    /// <summary>
    /// Registers a service instance with the scope.
    /// </summary>
    /// <typeparam name="TImpl">The concrete implementation type of the service.</typeparam>
    /// <param name="instance">
    /// The service instance, or <c>null</c> if service creation failed.
    /// </param>
    /// <param name="providerType">
    /// Name of the type providing the service, used for diagnostics.
    /// </param>
    void ProvideService<TImpl>(TImpl? instance, string providerType)
        where TImpl : class;

    /// <summary>
    /// Requests a service from the scope, invoking <paramref name="onResult"/> when available.
    /// </summary>
    /// <typeparam name="TExposed">The exposed (interface) type to resolve.</typeparam>
    /// <param name="onResult">
    /// Callback invoked with the resolved instance, or <c>null</c> if the service is unavailable.
    /// </param>
    /// <param name="requestorType">Name of the requesting type, used for diagnostics.</param>
    void ResolveDependency<TExposed>(Action<TExposed?> onResult, string requestorType)
        where TExposed : class;
}
