using Godot;
using Sovereign.Simulation;

namespace Sovereign.Presentation;

public partial class Main
{
    private SubViewport? _portraitViewport;
    private LeaderView? _portraitPreview;

    // A single cached independent 3D world avoids rebuilding models on every panel refresh.
    // It does not replace the map camera and stops rendering when its panel is not displayed.
    private TextureRect LeaderPortrait(string countryId, int width = 196, int height = 266)
    {
        if (_portraitViewport == null)
        {
            _portraitViewport = new SubViewport { Size = new Vector2I(392, 532), OwnWorld3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Msaa3D = Viewport.Msaa.Msaa2X };
            AddChild(_portraitViewport);
            _portraitPreview = new LeaderView { CompactPortrait = true }; _portraitViewport.AddChild(_portraitPreview);
            _portraitPreview.SetProcessUnhandledInput(false);
        }
        var leader = HistoricalLeaders.Get(countryId, _engine.State.Date);
        _portraitPreview!.SetProcess(true); _portraitPreview.ShowLeader(leader.AppearanceKey, countryId, leader.AgeOn(_engine.State.Date) ?? -1);
        _portraitViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible;
        return new TextureRect { Texture = _portraitViewport.GetTexture(), CustomMinimumSize = new Vector2(width, height),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore, TooltipText = "实时三维人物 · 点击人物查看进入可转动视图" };
    }

    private void SuspendEmbeddedPortrait()
    {
        if (_portraitViewport == null) return;
        _portraitViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        _portraitPreview!.SetProcess(false);
    }
}
