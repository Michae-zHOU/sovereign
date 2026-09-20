using Godot;
using System;

namespace Sovereign.Presentation;

public partial class Main
{
    // Art stays in texture caches across panel rebuilds. Decorative controls never
    // intercept the mouse; text and actions remain on opaque cabinet surfaces.
    private Texture2D PanelIllustration(string key)
    {
        string cacheKey = "panel-art:" + key;
        if (_interfaceTextures.TryGetValue(cacheKey, out var texture)) return texture;
        string path = $"res://Assets/illustrations/panels/{key}.png";
        texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : UiIcon("overview");
        _interfaceTextures[cacheKey] = texture;
        return texture;
    }

    private void IllustratedHeader(Control parent, string key, string title, string subtitle, int height = 132)
    {
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(0, height),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", Style(new Color("172329"), new Color("8b7854"), 0, 2));
        parent.AddChild(frame);
        var canvas = new Control { CustomMinimumSize = new Vector2(0, height - 4), ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        frame.AddChild(canvas);
        var picture = new TextureRect { Texture = PanelIllustration(key),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.AddChild(picture); picture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        if (key == "politics")
        {
            picture.AnchorLeft = .48f;
            picture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        }
        // The title plaque is deliberately separate from the subjects on the right.
        var plaque = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        plaque.AddThemeStyleboxOverride("panel", Style(new Color(.06f, .10f, .12f, .88f), new Color(.52f, .45f, .31f, .65f), 0, 8));
        canvas.AddChild(plaque); plaque.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        plaque.AnchorRight = key == "politics" ? .48f : .60f;
        plaque.GrowVertical = Control.GrowDirection.Begin;
        if (key == "politics")
        {
            plaque.AnchorTop = plaque.AnchorBottom = .5f;
            plaque.GrowVertical = Control.GrowDirection.Both;
            plaque.AddThemeStyleboxOverride("panel", Style(new Color("172329"), new Color("172329"), 0, 20));
        }
        var copy = VBox(plaque, 1); copy.MouseFilter = Control.MouseFilterEnum.Ignore;
        copy.AddChild(L(title, key == "politics" ? 30 : 22, Gold, true));
        copy.AddChild(Para(subtitle, key == "politics" ? 15 : 12, Cream));
    }

    private Texture2D IllustrationCell(string key, int index, int columns, int rows)
    {
        string cacheKey = $"art-cell:{key}:{index}";
        if (_interfaceTextures.TryGetValue(cacheKey, out var texture)) return texture;
        var atlas = PanelIllustration(key);
        var cell = atlas.GetSize() / new Vector2(columns, rows);
        // Half-pixel inset keeps adjacent painted cells out of filtered edges.
        texture = new AtlasTexture { Atlas = atlas, FilterClip = true,
            Region = new Rect2(new Vector2(index % columns, index / columns) * cell + Vector2.One * .5f, cell - Vector2.One) };
        _interfaceTextures[cacheKey] = texture;
        return texture;
    }

    private Texture2D PopulationPortrait(string profession) => IllustrationCell("population-portraits", profession switch
    {
        "peasants" => 0, "laborers" => 1, "machinists" => 2, "shopkeepers" => 3,
        "capitalists" => 4, "aristocrats" => 5, "unemployed" => 6, _ => 7
    }, 4, 2);

    private TextureRect PoliticalGroupPicture(string id) => new()
    {
        Texture = IllustrationCell("political-groups", id switch
        {
            "landowners" => 0, "industrialists" => 1, "rural_folk" => 2, "intelligentsia" => 3,
            "devout" => 4, "armed_forces" => 5, "petite_bourgeoisie" => 6, "trade_unions" => 7, _ => 0
        }, 4, 2),
        CustomMinimumSize = new Vector2(58, 58), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore,
        SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
    };
}
