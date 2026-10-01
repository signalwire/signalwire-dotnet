using System.Reflection;

namespace SignalWire.Core;

/// <summary>
/// Running synchronous user code without holding up other requests.
/// </summary>
/// <remarks>
/// <para>The built-in HTTP servers (<c>SWMLService.Run</c> / <c>AgentServer.Run</c>)
/// serve each request on a thread-pool worker, so a synchronous tool handler,
/// per-request configuration callback or routing callback that blocks (an HTTP
/// call, say) no longer holds up every other request until it returns — handlers
/// for different calls run concurrently, so they must guard shared state.</para>
/// <para>Setting <c>SWML_SYNC_HANDLERS_INLINE</c> to <c>1</c>, <c>true</c> or
/// <c>yes</c> serves requests one at a time on the listener thread instead, as
/// earlier releases did.</para>
/// </remarks>
public static class SyncHandlers
{
    /// <summary>True when <c>SWML_SYNC_HANDLERS_INLINE</c> asks for one-at-a-time
    /// (inline) handling.</summary>
    public static bool SyncHandlersInline()
    {
        var value = (Environment.GetEnvironmentVariable("SWML_SYNC_HANDLERS_INLINE") ?? "").Trim();
        return value.Equals("1", StringComparison.Ordinal)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True for an asynchronous callable: a delegate whose invocation returns a
    /// <see cref="Task"/> / <see cref="ValueTask"/> (calling it only starts the
    /// work), or an object whose <c>Invoke</c> method does.
    /// </summary>
    /// <param name="obj">The callable to inspect.</param>
    public static bool IsAsyncCallable(object? obj)
    {
        var method = obj switch
        {
            null => null,
            Delegate d => d.Method,
            _ => obj.GetType().GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance),
        };
        if (method is null)
        {
            return false;
        }
        var ret = method.ReturnType;
        return typeof(Task).IsAssignableFrom(ret)
            || ret == typeof(ValueTask)
            || (ret.IsGenericType && ret.GetGenericTypeDefinition() == typeof(ValueTask<>));
    }

    /// <summary>
    /// Call <paramref name="func"/> with <paramref name="args"/> on a thread-pool
    /// worker, or inline when <see cref="SyncHandlersInline"/> is set. An
    /// exception the callable throws surfaces from the returned task unwrapped.
    /// </summary>
    /// <param name="func">The synchronous callable.</param>
    /// <param name="args">Its arguments.</param>
    /// <returns>The callable's result.</returns>
    public static Task<object?> RunSyncHandler(Delegate func, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(func);
        if (SyncHandlersInline())
        {
            return Task.FromResult(Invoke(func, args));
        }
        return Task.Run(() => Invoke(func, args));
    }

    /// <summary>Typed form of <see cref="RunSyncHandler(Delegate, object?[])"/>.</summary>
    /// <param name="func">The synchronous callable.</param>
    public static Task<T> RunSyncHandler<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return SyncHandlersInline() ? Task.FromResult(func()) : Task.Run(func);
    }

    /// <summary>Serve one unit of work: inline when so configured, else on a
    /// thread-pool worker (fire-and-forget; <paramref name="work"/> owns its own
    /// error handling).</summary>
    internal static void Dispatch(Action work)
    {
        if (SyncHandlersInline())
        {
            work();
        }
        else
        {
            _ = Task.Run(work);
        }
    }

    private static object? Invoke(Delegate func, object?[] args)
    {
        try
        {
            return func.DynamicInvoke(args);
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw; // unreachable
        }
    }
}
