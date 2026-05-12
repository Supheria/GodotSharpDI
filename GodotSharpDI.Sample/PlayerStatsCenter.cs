// =============================================================================
// PlayerStatsCenter.cs
//
// 演示功能：
//   1. [Host] — 简单的服务提供者
//   2. [Provide] 字段级别 — v1.3.0 新特性，将 [Export] 字段直接作为服务暴露
//   3. [Provide] 属性级别 — 同步提供自身实例
// =============================================================================

using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

[Host]
public sealed partial class PlayerStatsCenter : Node
{
    // ── [Provide] 属性 — 同步提供自身 ─────────────────────────
    // 将 this 暴露为 PlayerStatsCenter 类型，供其他 Host/User 注入消费。

    [Provide]
    public PlayerStatsCenter Self => this;

    // ── [Provide] 字段级别 — v1.3.0 新特性 ────────────────────
    // 框架支持在字段上直接标注 [Provide]，无需通过属性或方法包装。
    // 这对于 [Export] 导出的子节点特别有用，可以直接作为服务暴露。

    [Provide(ExposedTypes = [typeof(IAudioService)])]
    private AudioService _audioService = new();

    public override partial void _Notification(int what);
}

/// <summary>
/// 演示：字段级别 [Provide] 暴露的服务接口
/// </summary>
public interface IAudioService
{
    void PlaySound(string name);
}

/// <summary>
/// 演示：字段级别 [Provide] 暴露的服务实现
/// </summary>
public sealed class AudioService : IAudioService
{
    public void PlaySound(string name)
    {
        GD.Print($"[AudioService] Playing sound: {name}");
    }
}
