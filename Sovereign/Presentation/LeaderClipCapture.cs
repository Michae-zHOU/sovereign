using Godot;
using System;
using System.IO;

namespace Sovereign.Presentation;

public partial class Main
{
    private async void CaptureLeaderClip(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder); Engine.MaxFps = 30;
            StartGame("QNG"); OpenLeader("QNG");
            _leader.SetFraming("full", true);
            await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
            _leader.AnimationCaptureStepSeconds = 1d / 30;
            _toast.Text = ""; _toastTime = 0;
            for (int frame = 0; frame < 180; frame++)
            {
                if (frame == 0) _leader.TriggerGesture("greeting");
                if (frame == 65) _leader.TriggerGesture("talk");
                if (frame == 155) _leader.TriggerGesture("agree");
                // Show actual garment depth from the front through a three-quarter turn.
                _leader.SetViewAngle(-.10f + .72f * (frame / 179f));
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var image = GetViewport().GetTexture().GetImage();
                var result = image.SavePng(Path.Combine(folder, $"frame-{frame:0000}.png")); image.Dispose();
                if (result != Error.Ok) throw new Exception("Frame capture failed: " + result);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            GD.Print("SOVEREIGN_LEADER_CLIP_PASS frames=180 authored=" + _leader.UsesAuthoredModel + " skeletal=" + _leader.HasSkeletalAnimation + " facial=" + _leader.HasFacialAnimation);
            RequestQuit(0);
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); RequestQuit(1); }
    }
}
