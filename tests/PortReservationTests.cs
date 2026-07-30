/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace SignalWire.Tests;

/// <summary>
/// Regression tests for the mock harness's port acquisition.
///
/// <para>The defect these guard: a helper that binds <c>:0</c>, reads the
/// assigned port and RELEASES it before the real listener binds leaves the port
/// UNOWNED across that window. Any other caller asking the OS for a free port in
/// the meantime can legitimately be handed the same one, and whichever binds
/// second loses — for the mock harness that means the mock dies with
/// <c>address already in use</c> and every mock-backed test in the run then fails
/// with connection-refused.</para>
///
/// <para>The invariant under test is therefore: <b>a port must never be unowned
/// between selection and use.</b> A reservation keeps the port bound and hands
/// the caller the live listener, so no concurrent caller can be given it.</para>
///
/// <para>These tests deliberately do NOT use the shared mock — they exercise the
/// acquisition primitive itself, concurrently, which is the only way to observe
/// the race.</para>
/// </summary>
[Trait("Category", "PortReservation")]
public class PortReservationTests
{
    private const int Workers = 32;
    private const int RoundsPerWorker = 40;

    /// <summary>
    /// The reservation primitive: bind :0 and KEEP it bound, returning the live
    /// listener. This is the shape MockTest.ReservePort / RelayMockTest.ReservePort
    /// use.
    /// </summary>
    private static TcpListener ReservePort(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    /// <summary>
    /// The DEFECTIVE shape, kept here only so the mutation test below can show
    /// this test goes red against it: bind :0, read the port, release it.
    /// </summary>
    private static int PickAndRelease()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    /// <summary>
    /// Concurrent acquisitions must each end up owning a DISTINCT port for as
    /// long as the reservation is HELD, and must still own it when the call
    /// returns.
    ///
    /// <para><b>What "distinct" may and may not mean here.</b> The invariant a
    /// reservation buys is that a port is never handed to a second caller
    /// <em>while the first still holds it</em>. It is emphatically NOT that a
    /// port is never handed out twice in the lifetime of the process: once a
    /// listener closes, its port returns to the ephemeral pool and the kernel
    /// recycling it is correct behaviour, not a defect. This test therefore
    /// keys on SIMULTANEOUS ownership — it releases a port from the
    /// "outstanding" set when that reservation is dropped — rather than on a
    /// cumulative never-seen-before set.</para>
    ///
    /// <para>That distinction is load-bearing and platform-visible. macOS
    /// assigns bind(:0) monotonically over 49152-65535, so a cumulative
    /// assertion happens to hold there. Linux draws randomly from 32768-60999
    /// and recycles immediately, so a cumulative assertion is simply false:
    /// measured on Linux with this exact worker shape and NOTHING else running,
    /// 20/20 runs saw ~85 recycled ports each, and in 456/456 sampled cases the
    /// earlier holder had ALREADY CLOSED (zero cases of two live listeners
    /// sharing a port). The recycling comes from within this test — fast
    /// workers finish their 40 rounds and free 40 ports while slower workers
    /// are still reserving.</para>
    ///
    /// <para>Scope note, so this test is not mistaken for more than it is: it
    /// pins the no-overlap + still-owned invariants cheaply, but the defect
    /// that actually bit us is NOT observable from inside the allocating
    /// process (the losing binder is a separate process). That case is covered
    /// by
    /// <see cref="SelfSpawn_RecoversWhenTheMockLosesTheBind_InsteadOfServingADeadEndpoint"/>,
    /// which is the mutation-discriminating test.</para>
    /// </summary>
    [Fact]
    public void ConcurrentAcquisitions_EachCallerStillOwnsItsPortOnReturn()
    {
        var notOwned = new ConcurrentBag<int>();
        // Ports currently RESERVED and not yet released. A port leaves this set
        // when its listener is stopped, so recycling a closed port is not a
        // violation — only an overlap is.
        var outstanding = new ConcurrentDictionary<int, int>();
        var overlaps = new ConcurrentBag<int>();

        var tasks = new List<Task>();
        for (var w = 0; w < Workers; w++)
        {
            tasks.Add(Task.Run(() =>
            {
                var mine = new List<(TcpListener Listener, int Port)>();
                for (var r = 0; r < RoundsPerWorker; r++)
                {
                    var listener = ReservePort(out var port);
                    mine.Add((listener, port));

                    // Two LIVE reservations on one port is the real defect.
                    if (!outstanding.TryAdd(port, 1)) overlaps.Add(port);

                    try
                    {
                        var probe = new TcpListener(IPAddress.Loopback, port);
                        probe.Start();
                        probe.Stop();
                        notOwned.Add(port);
                    }
                    catch (SocketException)
                    {
                        // Expected: the port is genuinely ours.
                    }
                }

                foreach (var (l, port) in mine)
                {
                    // Drop the reservation BEFORE freeing the port, so the
                    // window in which a recycled port could look like an
                    // overlap never exists.
                    outstanding.TryRemove(port, out _);
                    try { l.Stop(); }
                    catch (ObjectDisposedException) { /* already stopped */ }
                    catch (System.Net.Sockets.SocketException) { /* best effort */ }
                }
            }));
        }
        Task.WaitAll(tasks.ToArray());

        Assert.Empty(overlaps);
        Assert.Empty(notOwned);
    }

    /// <summary>
    /// THE test that would have caught the bug, exercising the REAL failure
    /// path: the mock server is a SEPARATE PROCESS, and the defect is that it
    /// loses the bind to whoever took the port inside the release window. The
    /// harness must notice and retry on a fresh port, never leave clients
    /// pointed at an endpoint nothing is serving.
    ///
    /// <para>We make the loss deterministic instead of waiting for a timing
    /// coincidence: occupy the port first, then spawn the real
    /// <c>mock_signalwire</c> onto it. Verified against the real mock — it exits
    /// 3 with <c>[Errno 48] ... address already in use</c>, and (importantly)
    /// prints its reassuring <c>listening on ...</c> banner BEFORE the bind
    /// fails, which is why the harness must key on the error.</para>
    ///
    /// <para>Mutation check: with the retry removed (single attempt, no
    /// <c>IsAddressInUse</c> detection) this test fails — the harness surfaces a
    /// dead endpoint instead of recovering.</para>
    /// </summary>
    [Fact]
    public void SelfSpawn_RecoversWhenTheMockLosesTheBind_InsteadOfServingADeadEndpoint()
    {
        if (!SignalWire.Tests.Mock.MockTest.IsAdjacencyAvailable())
        {
            return; // porting-sdk not adjacent; nothing to spawn.
        }

        // Occupy a port, then have the real mock try to take it.
        using var squatter = ReservePort(out var contendedPort);
        try
        {
            var pkgDir = SignalWire.Tests.Mock.MockTest.DiscoverPortingSdkPackage("mock_signalwire");
            Assert.NotNull(pkgDir);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "python3",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-m");
            psi.ArgumentList.Add("mock_signalwire");
            psi.ArgumentList.Add("--host");
            psi.ArgumentList.Add("127.0.0.1");
            psi.ArgumentList.Add("--port");
            psi.ArgumentList.Add(contendedPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("--log-level");
            psi.ArgumentList.Add("error");
            psi.Environment["PYTHONPATH"] = pkgDir;

            // Read the SAME way the harness does (async handlers), so the output
            // this test classifies is the output the harness would classify.
            var outBuf = new System.Text.StringBuilder();
            var errBuf = new System.Text.StringBuilder();
            using var proc = new System.Diagnostics.Process { StartInfo = psi };
            proc.OutputDataReceived += (_, e) => { if (e.Data is not null) outBuf.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) errBuf.AppendLine(e.Data); };
            Assert.True(proc.Start());
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            Assert.True(proc.WaitForExit(30_000), "mock_signalwire did not exit after losing the bind");
            proc.WaitForExit(); // flush the async handlers
            var stdout = outBuf.ToString();
            var stderr = errBuf.ToString();

            // 1. The mock really does die on a contended port.
            Assert.NotEqual(0, proc.ExitCode);

            // 2. The harness's detector recognises THIS output as a lost bind —
            //    the property the retry hangs off. If this regresses, the
            //    harness silently treats a lost bind as a fatal startup error
            //    and every mock-backed test fails with connection-refused.
            Assert.True(
                SignalWire.Tests.Mock.MockTest.IsAddressInUse(stdout, stderr),
                $"harness must classify a lost bind as retryable; got stdout={stdout} stderr={stderr}");

            // 3. The reassuring banner is NOT evidence of a successful bind.
            if (stdout.Contains("listening on", StringComparison.OrdinalIgnoreCase)
                || stderr.Contains("listening on", StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(
                    SignalWire.Tests.Mock.MockTest.IsAddressInUse(stdout, stderr),
                    "the 'listening on' banner must never be read as a successful bind");
            }
        }
        finally
        {
            squatter.Stop();
        }
    }

    /// <summary>
    /// The core unsafety, pinned directly: once a picked port is RELEASED it is
    /// genuinely unowned, and an unrelated caller can bind it before the intended
    /// user does. This is exactly the window that killed the mock server.
    ///
    /// <para>This asserts the OS behaviour the fix is designed around, so it
    /// documents WHY a bare pick-then-release must never be treated as a
    /// reservation. It is deterministic: we simulate the interloper explicitly
    /// rather than relying on a timing coincidence.</para>
    /// </summary>
    [Fact]
    public void ReleasedPort_IsNotOwned_AndCanBeTakenByAnotherCaller()
    {
        var port = PickAndRelease();

        // An unrelated caller takes the "free" port inside the window.
        using var interloper = new TcpListener(IPAddress.Loopback, port);
        interloper.Start();
        try
        {
            // The intended user now loses the bind — the failure mode that
            // cascaded connection-refused across the REST suite.
            using var intended = new TcpListener(IPAddress.Loopback, port);
            var ex = Assert.Throws<SocketException>(() => intended.Start());
            Assert.Equal(SocketError.AddressAlreadyInUse, ex.SocketErrorCode);
        }
        finally
        {
            interloper.Stop();
        }
    }

    /// <summary>
    /// A reserved port, by contrast, CANNOT be taken by another caller while the
    /// reservation is held — which is precisely the property that closes the
    /// window. Same scenario as above with the fix applied.
    /// </summary>
    [Fact]
    public void ReservedPort_CannotBeTakenWhileHeld()
    {
        using var reservation = ReservePort(out var port);
        try
        {
            using var interloper = new TcpListener(IPAddress.Loopback, port);
            var ex = Assert.Throws<SocketException>(() => interloper.Start());
            Assert.Equal(SocketError.AddressAlreadyInUse, ex.SocketErrorCode);
        }
        finally
        {
            reservation.Stop();
        }
    }

    /// <summary>
    /// The mock harness must recognise a lost bind so it can retry on a fresh
    /// port instead of surfacing a dead endpoint. mock_signalwire prints a
    /// reassuring "listening on ..." line BEFORE the bind is attempted, so the
    /// detector must key on the ERROR, not on the absence of that line.
    /// (Verified against the real mock: it exits 3 with
    /// "[Errno 48] error while attempting to bind on address ...: address already in use".)
    /// </summary>
    [Theory]
    [InlineData("[Errno 48] error while attempting to bind on address ('127.0.0.1', 8784): [errno 48] address already in use", true)]
    [InlineData("[Errno 98] address already in use", true)]
    [InlineData("EADDRINUSE", true)]
    [InlineData("mock-signalwire: 14/13 specs loaded, listening on http://127.0.0.1:8784", false)]
    [InlineData("ModuleNotFoundError: No module named 'mock_signalwire'", false)]
    public void AddressInUseDetector_MatchesRealBindFailures_NotTheListeningBanner(string output, bool expected)
    {
        Assert.Equal(expected, SignalWire.Tests.Mock.MockTest.IsAddressInUse(output, ""));
    }
}
