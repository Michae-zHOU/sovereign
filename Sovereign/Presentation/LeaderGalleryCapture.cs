using Godot;
using System;
using System.IO;
using System.Linq;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main
{
    // Developer-only visual regression capture. Dates are staged for portraits;
    // this mode does not simulate or save the resulting campaign state.
    private async void CaptureLeaderGallery(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            Engine.MaxFps = 30;
            var leaders = HistoricalLeaders.All.GroupBy(x => x.Id).Select(x => x.First()).ToArray();
            foreach (var definition in leaders)
            {
                StartGame(definition.CountryId);
                _engine.State.Date = definition.StartDate;
                OpenLeader(definition.CountryId);
                foreach (string view in new[] { "full", "face" })
                {
                    _leader.SetFraming(view, true);
                    _leader.SetViewAngle(-.10f);
                    _toast.Text = ""; _toastTime = 0;
                    await ToSignal(GetTree().CreateTimer(.7), SceneTreeTimer.SignalName.Timeout);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var picture = GetViewport().GetTexture().GetImage();
                    if (picture.SavePng(Path.Combine(folder, definition.Id + "-" + view + ".png")) != Error.Ok)
                        throw new Exception("Portrait capture failed: " + definition.Id);
                }
            }
            StartGame("SPA"); _engine.State.Date = new DateTime(1843, 11, 10); OpenLeader("SPA");
            _leader.SetFraming("full", true);
            await ToSignal(GetTree().CreateTimer(.7), SceneTreeTimer.SignalName.Timeout);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using (var picture = GetViewport().GetTexture().GetImage())
                if (picture.SavePng(Path.Combine(folder, "isabella_ii_adolescent-full.png")) != Error.Ok)
                    throw new Exception("Adolescent portrait capture failed");
            GD.Print("SOVEREIGN_LEADER_GALLERY_PASS identities=20 age_variants=21 captures=41");
            RequestQuit(0);
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); RequestQuit(1); }
    }
}
