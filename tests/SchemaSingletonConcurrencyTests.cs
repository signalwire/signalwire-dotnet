/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using System.Threading;
using System.Threading.Tasks;
using SignalWire.SWML;
using Xunit;

namespace SignalWire.Tests;

/// <summary>
/// Parallel-safety pin for <see cref="Schema.Instance"/>: a reader is never handed
/// <see langword="null"/> while another thread calls <see cref="Schema.Reset"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is a REGRESSION PIN on already-correct code, not the test for a fix. The
/// current getter snapshots the field into a local and returns that local on the
/// fast path, and on the slow path returns the VALUE OF THE ASSIGNMENT EXPRESSION
/// evaluated inside the lock. Neither return can observe a field that a concurrent
/// <c>Reset()</c> has nulled, so the non-nullable property cannot hand back null.
/// </para>
/// <para>
/// It is worth pinning because the shape is easy to "simplify" into a broken one.
/// Assigning under the lock and then returning the FIELD outside it —
/// <c>lock (Lock) { _instance ??= new Schema(); } return _instance;</c> — reopens
/// exactly the window this test closes, and additionally costs every reader a lock
/// acquisition by dropping the lock-free fast path. That rewrite was proposed and
/// rejected here.
/// </para>
/// <para>
/// <b>Known limitation — this test does not currently discriminate.</b> It passes
/// against BOTH getter shapes, measured: reverting to the return-the-field-outside
/// -the-lock form leaves it green (net8.0, 1/1). The reason is construction cost.
/// The <see cref="Schema"/> constructor parses the ~400-line embedded schema and
/// compiles a Draft 2020-12 validator WHILE HOLDING THE LOCK, so any reader that
/// finds the field null starves the resetter — which needs the same lock — and the
/// window effectively never lands. Holding ctor cost as the only variable in a
/// standalone probe, null observations fell 15 → 1 → 1 per 200k as the ctor went
/// free → ~50us → ~500us.
/// </para>
/// <para>
/// So treat this as a cheap smoke test of the invariant, NOT as proof the invariant
/// is enforced. Making it discriminate needs a seam that lets the test substitute a
/// trivially-constructed instance; until then, the guard against the broken rewrite
/// is this comment and code review.
/// </para>
/// <para>
/// Deliberately carries NO <c>[Collection]</c>: the property must hold under full
/// parallelism, and serialising the test would defeat its purpose.
/// </para>
/// </remarks>
public class SchemaSingletonConcurrencyTests
{
    [Fact]
    public void InstanceNeverReturnsNullWhileAnotherThreadResets()
    {
        // Warm the singleton so the path under test is the CHEAP one (field
        // already populated). The expensive construct-under-lock path is what
        // masks a null-return window, so it must not dominate the run.
        Assert.NotNull(Schema.Instance);

        using var cts = new CancellationTokenSource();
        var resetter = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                Schema.Reset();
            }
        });

        var nulls = 0;
        var nullDerefs = 0;
        Parallel.For(0, 200_000, _ =>
        {
            // Assert on the REFERENCE the getter hands back. Schema.Instance is
            // typed non-nullable, so a null return would be a contract break —
            // checking it here does not depend on a member read being expensive
            // enough to lose the race.
            var instance = Schema.Instance;
            if (instance is null)
            {
                Interlocked.Increment(ref nulls);
                return;
            }

            // Also exercise a real dereference — the shape a caller would use
            // (e.g. ConstructionReadbackTests reading Schema.Instance.SchemaPath).
            try
            {
                if (string.IsNullOrEmpty(instance.SchemaPath))
                {
                    Interlocked.Increment(ref nulls);
                }
            }
            catch (NullReferenceException)
            {
                Interlocked.Increment(ref nullDerefs);
            }
        });

        cts.Cancel();
        resetter.Wait();

        Assert.Equal(0, nulls);
        Assert.Equal(0, nullDerefs);
    }
}
