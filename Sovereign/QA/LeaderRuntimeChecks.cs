using Godot;
using Sovereign.Presentation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sovereign.QA;

/// <summary>Runs inside the native renderer without Main, the world map or the simulation.</summary>
public partial class LeaderRuntimeChecks : Node3D
{
    private static readonly (string Key, string Country, int Age)[] Cases =
    {
        ("william_iv","GBR",70), ("victoria","GBR",18),
        ("frederick_william_iii","PRU",65), ("frederick_william_iv","PRU",45),
        ("tokugawa_ienari","JAP",62), ("tokugawa_ieyoshi","JAP",44),
        ("louis_philippe","FRA",62), ("ferdinand_i","AUS",42), ("nicholas_i","RUS",39),
        ("andrew_jackson","USA",68), ("martin_van_buren","USA",54),
        ("william_henry_harrison","USA",68), ("john_tyler","USA",51), ("james_polk","USA",49),
        ("daoguang","QNG",53), ("mahmud_ii","OTT",50), ("abdulmejid_i","OTT",16),
        ("isabella_ii","SPA",5), ("maria_ii","POR",16), ("leopold_i","BEL",45),
        ("isabella_ii","SPA",13)
    };

    public override async void _Ready()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            string only = args.FirstOrDefault(x => x.StartsWith("--identity="))?.Split('=', 2)[1] ?? "";
            string captureDirectory = args.FirstOrDefault(x => x.StartsWith("--capture-dir="))?.Split('=', 2)[1] ?? "";
            if (captureDirectory.Length > 0)
            {
                var error = DirAccess.MakeDirRecursiveAbsolute(captureDirectory);
                Check(error is Error.Ok or Error.AlreadyExists, "Cannot create capture directory: " + error);
            }
            var view = new LeaderView { CompactPortrait = true, PreferRealisticPortrait = false };
            AddChild(view);
            view.SetProcess(false);
            int cases = 0;
            ulong previousSkeleton = 0;
            foreach (var entry in Cases)
            {
                if (only.Length > 0 && entry.Key != only) continue;
                view.ShowLeader(entry.Key, entry.Country, entry.Age);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Check(view.UsesAuthoredModel, entry.Key + ": procedural fallback was used");
                Check(!view.IsPortraitMode, entry.Key + ": a 2D portrait was used");
                Check(view.HasSkeletalAnimation && view.HasFacialAnimation, entry.Key + ": missing skeletal/facial animation");
                Check(view.HasFullFigure, entry.Key + ": expected a full figure, including child models");
                int builds = view.ModelBuildCount;
                var skeleton = view.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().Single();
                ulong skeletonId = skeleton.GetInstanceId();
                Check(skeletonId != previousSkeleton, entry.Key + ": did not change to the requested figure");
                previousSkeleton = skeletonId;

                view.SetFraming("face", true);
                view.SetViewAngle(.45f);
                view.ShowLeader(entry.Key, entry.Country, entry.Age);
                view.ShowLeader(entry.Key, entry.Country, entry.Age + 1);
                view.HideLeader();
                view.ShowLeader(entry.Key, entry.Country, entry.Age);
                Check(view.ModelBuildCount == builds, entry.Key + ": view/visibility/within-stage age change rebuilt the model");
                Check(skeleton.GetInstanceId() == skeletonId, entry.Key + ": model instance changed on framing");
                Check(view.Framing == "face", entry.Key + ": showing the same model reset framing");
                if (entry.Key == "isabella_ii")
                    Check(view.LoadedModelKey == (entry.Age >= 11 ? "isabella_ii_adolescent" : "isabella_ii"), "Isabella age-stage selection is incorrect");

                var head = skeleton.FindBone("Head");
                Check(head >= 0, entry.Key + ": Head bone missing");
                for (int i = 0; i < 90; i++) view._Process(1.0 / 30.0);
                var idle = skeleton.GetBonePoseRotation(head);
                view.TriggerGesture("talk");
                float maximumTravel = 0;
                for (int i = 0; i < 150; i++)
                {
                    view._Process(1.0 / 30.0);
                    maximumTravel = Math.Max(maximumTravel, idle.AngleTo(skeleton.GetBonePoseRotation(head)));
                }
                Check(float.IsFinite(maximumTravel) && maximumTravel < .28f, entry.Key + ": gesture drift or excessive head movement");
                view.ResetView();

                if (captureDirectory.Length > 0)
                {
                    foreach (string framing in new[] { "full", "face" })
                    {
                        view.SetFraming(framing, true);
                        view.SetViewAngle(-.10f);
                        for (int i = 0; i < 3; i++)
                        {
                            view._Process(1.0 / 30.0);
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        }
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        string file = System.IO.Path.Combine(captureDirectory, view.LoadedModelKey + "-" + framing + ".png");
                        using var capture = GetViewport().GetTexture().GetImage();
                        Check(capture.SavePng(file) == Error.Ok, "Capture failed: " + file);
                    }
                }
                GD.Print($"LEADER_RUNTIME_PASS key={view.LoadedModelKey} builds={builds} head_travel_rad={maximumTravel:F4} skeleton={skeletonId}");
                cases++;
            }
            Check(cases > 0, "No requested identity was found");
            GD.Print($"LEADER_RUNTIME_COMPLETE cases={cases} video_memory_bytes={RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.VideoMemUsed)}");
            GetTree().Quit();
        }
        catch (Exception ex)
        {
            GD.PushError("LEADER_RUNTIME_FAIL " + ex);
            GetTree().Quit(1);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
