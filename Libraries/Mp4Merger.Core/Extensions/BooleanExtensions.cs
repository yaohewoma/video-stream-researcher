namespace Mp4Merger.Core.Extensions;

/// <summary>
/// 布尔扩展方法
/// </summary>
public static class BooleanExtensions
{
    /// <summary>
    /// 如果为true则执行操作
    /// </summary>
    /// <param name="condition">条件</param>
    /// <param name="action">要执行的操作</param>
    public static void IfTrue(this bool condition, Action action)
    {
        if (condition)
            action();
    }

    /// <summary>
    /// 如果为true则执行异步操作
    /// </summary>
    /// <param name="condition">条件</param>
    /// <param name="action">要执行的异步操作</param>
    public static async Task IfTrueAsync(this bool condition, Func<Task> action)
    {
        if (condition)
            await action();
    }

    /// <summary>
    /// 如果为false则执行操作
    /// </summary>
    /// <param name="condition">条件</param>
    /// <param name="action">要执行的操作</param>
    public static void IfFalse(this bool condition, Action action)
    {
        if (!condition)
            action();
    }

    /// <summary>
    /// 如果为false则执行异步操作
    /// </summary>
    /// <param name="condition">条件</param>
    /// <param name="action">要执行的异步操作</param>
    public static async Task IfFalseAsync(this bool condition, Func<Task> action)
    {
        if (!condition)
            await action();
    }
}
