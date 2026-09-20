using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main
{
    private async void RunPerformanceSample()
    {
        try
        {
            StartGame("QNG");
            var views = OS.GetCmdlineUserArgs().Contains("--leader-profile")
                ? new[] { "leader-full", "leader-face", "map-portrait" }
                : new[] { "map-paused", "map-12x", "suzhou-city", "construction-500" };
            foreach (string view in views)
            {
                if (view == "leader-full") { OpenLeader("QNG"); _leader.SetFraming("full", true); }
                if (view == "leader-face") { _leader.SetFraming("face", true); _leader.TriggerGesture("talk"); }
                if (view == "map-portrait") { CloseCity(); ShowTab("Overview"); }
                if (view == "map-12x") SetSpeed(12);
                if (view == "suzhou-city") { SetSpeed(0); OpenCityDetail("QNG_suzhou"); }
                if (view == "construction-500")
                {
                    CloseCity();
                    foreach (var city in _engine.Player.Cities)
                    foreach (var industry in Catalog.Industries)
                    {
                        for (int i = 0; i < 50 && _engine.Player.Construction.Count < ConstructionCatalog.MaximumQueueLength; i++)
                            if (!_engine.BuildInCity(city.Id, industry.Id).Success) break;
                    }
                    OpenConstructionQueue(); SetSpeed(12);
                }
                await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
                var frames = new List<double>(); var process = new List<double>();
                ulong previous = Time.GetTicksUsec();
                for (int i = 0; i < 480; i++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    ulong now = Time.GetTicksUsec(); frames.Add((now - previous) / 1000d); previous = now;
                    process.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000d);
                    if (_engine.State.PendingEvent != null)
                    {
                        _engine.ResolveEvent(_engine.State.PendingEvent.Options.Last().Id); _shownEvent = ""; CloseModal();
                        if (view is "map-12x" or "construction-500") SetSpeed(12);
                    }
                }
                frames.Sort(); process.Sort();
                GD.Print($"PERFORMANCE_SAMPLE view={view} frames={frames.Count} median_ms={frames[frames.Count / 2]:0.00} p95_ms={frames[(int)(frames.Count * .95)]:0.00} process_p95_ms={process[(int)(process.Count * .95)]:0.00} nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0} static_mib={Performance.GetMonitor(Performance.Monitor.MemoryStatic) / 1048576d:0.0}");
            }
            RequestQuit(0);
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); RequestQuit(1); }
    }
}
