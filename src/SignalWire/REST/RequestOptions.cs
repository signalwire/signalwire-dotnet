using System.Collections.Generic;

namespace SignalWire.REST;

/// <summary>
/// The REST request-options envelope (plan 4.2): a single immutable value object
/// controlling per-request transport behaviour — timeout, retries (with an
/// idempotency-aware retry policy + exponential backoff), and cooperative
/// cancellation. Supplied at two levels:
///
/// <list type="bullet">
///   <item><b>Client default</b>: <c>new RestClient(..., requestOptions: ...)</c>
///   (or <c>new HttpClient(..., requestOptions: ...)</c>) stored on the transport
///   and applied to every request.</item>
///   <item><b>Per-request override</b>: each verb accepts an optional
///   <c>requestOptions</c> that <i>shallow-overrides</i> the client default for
///   that one call — an unset (<c>null</c>) field falls back to the client
///   default, then the built-in default.</item>
/// </list>
///
/// <para>The timeout + retry semantics are the reference-pinned, wire-observable
/// contract (the mock sees N attempts and honours the backoff ordering). The
/// <see cref="AbortSignal"/> fidelity is per-port idiom: .NET threads the native
/// <see cref="System.Threading.CancellationToken"/> straight to the underlying
/// <c>System.Net.Http.HttpClient</c> send, so a set token cancels the in-flight
/// socket read (true in-flight cancellation, not merely a between-attempts
/// check). Mirrors Python <c>signalwire.rest._request_options.RequestOptions</c>.</para>
///
/// <para>All fields are optional (<c>null</c> = inherit); resolution is
/// per-request over client-default over built-in.</para>
/// </summary>
public sealed record RequestOptions
{
    /// <summary>
    /// Max wall-clock seconds per attempt; on exceed the request raises the
    /// transport-error type (a <see cref="SignalWireRestError"/> with
    /// <see cref="SignalWireRestError.StatusCode"/> 0). Built-in default 30.0.
    /// <c>null</c> = inherit.
    /// </summary>
    public double? Timeout { get; init; }

    /// <summary>
    /// Number of RETRY attempts (total attempts = <c>Retries + 1</c>) on a
    /// retryable failure. Built-in default 0 (opt-in resilience — the no-retry
    /// behaviour stays the default; a caller opts into retries). <c>null</c> =
    /// inherit.
    /// </summary>
    public int? Retries { get; init; }

    /// <summary>
    /// HTTP statuses that trigger a retry for an idempotent method. Built-in
    /// <c>{429, 500, 502, 503, 504}</c>. <c>null</c> = inherit.
    /// </summary>
    public IReadOnlyCollection<int>? RetryOnStatus { get; init; }

    /// <summary>
    /// Base seconds for exponential backoff between retries
    /// (<c>backoff * 2^(attempt-1)</c>), honouring <c>Retry-After</c> when
    /// present. Built-in default 0.5. <c>null</c> = inherit.
    /// </summary>
    public double? RetryBackoff { get; init; }

    /// <summary>
    /// Cooperative-cancellation primitive: the native
    /// <see cref="System.Threading.CancellationToken"/> threaded straight to the
    /// HTTP send for true in-flight cancellation. Checked before each attempt and
    /// honoured mid-send. Built-in default
    /// <see cref="System.Threading.CancellationToken.None"/>. <c>null</c> =
    /// inherit.
    /// </summary>
    public System.Threading.CancellationToken? AbortSignal { get; init; }

    /// <summary>
    /// Return <c>this</c> with any set (non-<c>null</c>) field of
    /// <paramref name="over"/> applied — the per-request-over-client-default
    /// shallow merge. An unset field on <paramref name="over"/> leaves
    /// <c>this</c>'s value intact. Mirrors Python <c>RequestOptions.merge</c>.
    /// </summary>
    public RequestOptions Merge(RequestOptions? over)
    {
        if (over is null) return this;
        return new RequestOptions
        {
            Timeout = over.Timeout ?? Timeout,
            Retries = over.Retries ?? Retries,
            RetryOnStatus = over.RetryOnStatus ?? RetryOnStatus,
            RetryBackoff = over.RetryBackoff ?? RetryBackoff,
            AbortSignal = over.AbortSignal ?? AbortSignal,
        };
    }
}

/// <summary>
/// A <see cref="RequestOptions"/> with every field resolved to a concrete value
/// (no <c>null</c> remains), so the request loop reads concrete values without
/// re-checking defaults on every attempt. Produced by
/// <see cref="RequestOptionsSupport.Resolve"/>. The cross-language audit maps this
/// to Python's private <c>_EffectiveOptions</c> (an implementation detail of the
/// resolve/retry helpers, not a public surface type).
/// </summary>
public sealed class EffectiveRequestOptions
{
    public double Timeout { get; }
    public int Retries { get; }
    public IReadOnlyCollection<int> RetryOnStatus { get; }
    public double RetryBackoff { get; }
    public System.Threading.CancellationToken AbortSignal { get; }

    internal EffectiveRequestOptions(
        double timeout, int retries, IReadOnlyCollection<int> retryOnStatus,
        double retryBackoff, System.Threading.CancellationToken abortSignal)
    {
        Timeout = timeout;
        Retries = retries;
        RetryOnStatus = retryOnStatus;
        RetryBackoff = retryBackoff;
        AbortSignal = abortSignal;
    }
}

/// <summary>
/// The request-options resolution + retry-policy helpers. .NET has no
/// module-level free functions, so these live as <c>public static</c> methods on
/// this facade; the cross-language audit projects them to Python's module-level
/// <c>signalwire.rest._request_options.resolve</c> /
/// <c>status_is_retryable</c> free functions.
/// </summary>
public static class RequestOptionsSupport
{
    // The built-in defaults (the contract floor). A null RequestOptions field
    // means "inherit"; these are what an unset field resolves to at apply-time.
    internal const double DefaultTimeout = 30.0;
    internal const int DefaultRetries = 0;
    internal const double DefaultRetryBackoff = 0.5;
    internal static readonly IReadOnlyCollection<int> DefaultRetryOnStatus =
        new HashSet<int> { 429, 500, 502, 503, 504 };

    // Methods with no server-side side effect — safe to retry on any retryable
    // status. POST/PATCH are excluded: they may create/mutate, so they retry
    // ONLY on a transport error or 429/503 (throttles), never blindly on
    // 500/502/504, to avoid duplicate side effects. This asymmetry is part of
    // the pinned contract.
    private static readonly HashSet<string> IdempotentMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "PUT", "DELETE", "HEAD", "OPTIONS" };

    /// <summary>
    /// Resolve the effective options: per-request over client-default over
    /// built-in. <c>null</c> on any field inherits the next level down; the
    /// built-in defaults are the floor. Mirrors Python <c>resolve</c>.
    /// </summary>
    public static EffectiveRequestOptions Resolve(
        RequestOptions? clientDefault, RequestOptions? perRequest)
    {
        var merged = (clientDefault ?? new RequestOptions()).Merge(perRequest);
        return new EffectiveRequestOptions(
            merged.Timeout ?? DefaultTimeout,
            merged.Retries ?? DefaultRetries,
            merged.RetryOnStatus ?? DefaultRetryOnStatus,
            merged.RetryBackoff ?? DefaultRetryBackoff,
            merged.AbortSignal ?? System.Threading.CancellationToken.None);
    }

    /// <summary>
    /// Whether an HTTP <paramref name="status"/> for <paramref name="method"/>
    /// should trigger a retry. Idempotent methods (GET/PUT/DELETE) retry on the
    /// full <see cref="EffectiveRequestOptions.RetryOnStatus"/> set. Non-idempotent
    /// methods (POST/PATCH) retry only on 429/503 (the Retry-After-bearing
    /// throttles), never on 500/502/504, to avoid replaying a side effect that may
    /// have partially applied. Mirrors Python <c>status_is_retryable</c>.
    /// </summary>
    public static bool StatusIsRetryable(string method, int status, EffectiveRequestOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        if (!opts.RetryOnStatus.Contains(status)) return false;
        if (IdempotentMethods.Contains(method)) return true;
        // Non-idempotent: only the throttle statuses (which carry Retry-After
        // and mean "the request was NOT processed, back off").
        return status == 429 || status == 503;
    }
}
