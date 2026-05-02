/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

// Disable parallelism. The mock-backed tests share a single
// mock_signalwire HTTP server (slot 8784) and rely on per-test
// journal Reset(). Parallel execution causes journal entries from
// other tests to leak into Last() assertions.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
