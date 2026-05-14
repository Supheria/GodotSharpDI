// =============================================================================
// PlayerStatsCenter.cs
//
// Demonstrated features:
//   1. [Host] — Simple service provider
//   2. [Provide] field level — v1.3.0 new feature, exposes [Export] field directly as service
//   3. [Provide] property level — Synchronously provides self instance
// =============================================================================

using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

[Host]
public sealed partial class PlayerStatsCenter : Node
{
    // ── [Provide] property — Synchronously provides self ─────────────────────────
    // Exposes this as PlayerStatsCenter type for other Hosts/Users to inject and consume.

    [Provide]
    public PlayerStatsCenter Self => this;

    // ── [Provide] field level — v1.3.0 new feature ────────────────────
    // Framework supports directly annotating [Provide] on fields, no need for property or method wrapping.
    // This is especially useful for [Export] exported child nodes, can be directly exposed as services.

    [Provide(ExposedTypes = [typeof(IAudioService)])]
    private AudioService _audioService = new();

    public override partial void _Notification(int what);
}

/// <summary>
/// Demonstrates: Service interface exposed by field-level [Provide]
/// </summary>
public interface IAudioService
{
    void PlaySound(string name);
}

/// <summary>
/// Demonstrates: Service implementation exposed by field-level [Provide]
/// </summary>
public sealed class AudioService : IAudioService
{
    public void PlaySound(string name)
    {
        GD.Print($"[AudioService] Playing sound: {name}");
    }
}
