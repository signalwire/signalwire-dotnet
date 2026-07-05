/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System;
using System.Linq;
using System.Reflection;
using SignalWire.Relay;
using Xunit;

namespace SignalWire.Tests;

/// <summary>
/// Pins the RELAY action control surface to the reference oracle contract
/// (porting-sdk projects Stoppable/Pausable/Volume onto the CONCRETE actions):
///   Play    -> stop, pause, resume, volume
///   Record  -> stop, pause, resume            (NO volume)
///   Collect -> stop, pause, resume, volume    (+ start_input_timers)
///   others  -> stop
/// Every action stops via the shared Action.Stop() base method; pause/resume/
/// volume are declared on the concrete subclasses per this map.
/// </summary>
[Trait("Category", "RelayUnit")]
public class RelayActionControlSurfaceTests
{
    private static bool HasPublic(Type t, string name) =>
        t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
         .Any(m => m.Name == name);

    [Fact]
    public void EveryConcreteAction_ExposesStop()
    {
        foreach (var t in new[]
        {
            typeof(PlayAction), typeof(RecordAction), typeof(CollectAction),
            typeof(StandaloneCollectAction), typeof(AIAction), typeof(DetectAction),
            typeof(FaxAction), typeof(PayAction), typeof(StreamAction),
            typeof(TapAction), typeof(TranscribeAction),
        })
        {
            Assert.True(HasPublic(t, "Stop"), $"{t.Name} must expose Stop()");
        }
    }

    [Fact]
    public void PlayAction_ExposesStopPauseResumeVolume()
    {
        Assert.True(HasPublic(typeof(PlayAction), "Stop"));
        Assert.True(HasPublic(typeof(PlayAction), "Pause"));
        Assert.True(HasPublic(typeof(PlayAction), "Resume"));
        Assert.True(HasPublic(typeof(PlayAction), "Volume"));
    }

    [Fact]
    public void RecordAction_ExposesStopPauseResume_ButNoVolume()
    {
        Assert.True(HasPublic(typeof(RecordAction), "Stop"));
        Assert.True(HasPublic(typeof(RecordAction), "Pause"));
        Assert.True(HasPublic(typeof(RecordAction), "Resume"));
        Assert.False(HasPublic(typeof(RecordAction), "Volume"),
            "RecordAction must NOT expose Volume (reference has no record.volume)");
    }

    [Fact]
    public void CollectAction_ExposesStopPauseResumeVolume_AndStartInputTimers()
    {
        Assert.True(HasPublic(typeof(CollectAction), "Stop"));
        Assert.True(HasPublic(typeof(CollectAction), "Pause"));
        Assert.True(HasPublic(typeof(CollectAction), "Resume"));
        Assert.True(HasPublic(typeof(CollectAction), "Volume"));
        Assert.True(HasPublic(typeof(CollectAction), "StartInputTimers"));
    }

    [Fact]
    public void StoppableOnlyActions_DoNotExposePauseResumeVolume()
    {
        foreach (var t in new[]
        {
            typeof(AIAction), typeof(DetectAction), typeof(FaxAction),
            typeof(PayAction), typeof(StreamAction), typeof(TapAction),
            typeof(TranscribeAction), typeof(StandaloneCollectAction),
        })
        {
            Assert.False(HasPublic(t, "Pause"), $"{t.Name} must not expose Pause()");
            Assert.False(HasPublic(t, "Resume"), $"{t.Name} must not expose Resume()");
            Assert.False(HasPublic(t, "Volume"), $"{t.Name} must not expose Volume()");
        }
    }

    [Fact]
    public void PauseMethods_AcceptOptionalBehaviorString()
    {
        foreach (var t in new[] { typeof(PlayAction), typeof(RecordAction), typeof(CollectAction) })
        {
            var pause = t.GetMethod("Pause", BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(string) }, null);
            Assert.True(pause is not null,
                $"{t.Name}.Pause must accept an optional behavior string");
            var p = pause!.GetParameters().Single();
            Assert.True(p.IsOptional, $"{t.Name}.Pause behavior parameter must be optional");
        }
    }
}
