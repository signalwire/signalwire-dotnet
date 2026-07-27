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
/// Regression test for the <see cref="Schema.Instance"/> null-return race.
/// </summary>
/// <remarks>
/// <para>
/// The getter used to assign inside the lock and then <c>return _instance;</c>
/// OUTSIDE it, so a concurrent <see cref="Schema.Reset"/> — which nulls the field
/// under the same lock — could land in that window and make a non-nullable
/// property hand back <see langword="null"/>. It surfaced as a 1-in-2008 net10.0
/// NullReferenceException in <c>ConstructionReadbackTests</c> reading
/// <c>Schema.Instance.SchemaPath</c>, because ~10 sibling classes call
/// <c>Schema.Reset()</c> in their ctor/Dispose and
/// <c>xunit.runner.json</c> sets <c>parallelizeTestCollections</c>.
/// </para>
/// <para>
/// The fix (Schema.cs) observes the instance UNDER the lock and returns that
/// observation. This test pins it: the correct behaviour is that a reader is
/// never handed null no matter how a concurrent writer interleaves — which is a
/// property of the singleton itself, not of which tests happen to run alongside
/// it. Deliberately carries NO <c>[Collection]</c> attribute: it must hold under
/// full parallelism, and serialising it would defeat its purpose.
/// </para>
/// </remarks>
public class SchemaSingletonConcurrencyTests
{
    [Fact]
    public void InstanceNeverReturnsNullWhileAnotherThreadResets()
    {
        using var cts = new CancellationTokenSource();
        var resetter = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                Schema.Reset();
            }
        });

        var nulls = 0;
        Parallel.For(0, 200_000, _ =>
        {
            // Pre-fix this threw NullReferenceException on the dereference.
            var path = Schema.Instance.SchemaPath;
            if (string.IsNullOrEmpty(path))
            {
                Interlocked.Increment(ref nulls);
            }
        });

        cts.Cancel();
        resetter.Wait();

        Assert.Equal(0, nulls);
    }
}
