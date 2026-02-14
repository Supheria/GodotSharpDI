namespace GodotSharpDI.Abstractions;

/// <summary>
/// 表示依赖解析的结果
/// </summary>
/// <typeparam name="T">服务类型</typeparam>
public readonly struct ResolutionResult<T>
    where T : class
{
    /// <summary>
    /// 服务实例引用（解析成功时不为空）
    /// </summary>
    public T? Instance { get; }

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
    public static ResolutionResult<T> Success(T instance)
    {
        if (instance == null)
            throw new System.ArgumentNullException(nameof(instance));
        return new ResolutionResult<T>(instance, null);
    }

    /// <summary>
    /// 创建失败的解析结果
    /// </summary>
    public static ResolutionResult<T> Failure(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            throw new System.ArgumentException("错误消息不能为空", nameof(errorMessage));
        return new ResolutionResult<T>(null, errorMessage);
    }

    private ResolutionResult(T? instance, string? errorMessage)
    {
        Instance = instance;
        ErrorMessage = errorMessage;
    }
}
