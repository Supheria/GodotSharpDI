namespace GodotSharpDI.Abstractions;

/// <summary>
/// 表示依赖解析的结果
/// </summary>
public readonly struct ResolutionResult
{
    /// <summary>
    /// 服务实例引用（解析成功时不为空）
    /// </summary>
    public object? Instance { get; }

    /// <summary>
    /// 错误消息（解析失败时不为空）
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// 指示解析是否成功
    /// </summary>
    public bool IsSuccess => Instance != null && ErrorMessage == null;

    /// <summary>
    /// 指示解析是否失败
    /// </summary>
    public bool IsFailure => Instance == null && ErrorMessage != null;

    /// <summary>
    /// 创建成功的解析结果
    /// </summary>
    public static ResolutionResult Success(object instance)
    {
        if (instance == null)
            throw new System.ArgumentNullException(nameof(instance));
        return new ResolutionResult(instance, null);
    }

    /// <summary>
    /// 创建失败的解析结果
    /// </summary>
    public static ResolutionResult Failure(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            throw new System.ArgumentException("错误消息不能为空", nameof(errorMessage));
        return new ResolutionResult(null, errorMessage);
    }

    private ResolutionResult(object? instance, string? errorMessage)
    {
        Instance = instance;
        ErrorMessage = errorMessage;
    }
}
