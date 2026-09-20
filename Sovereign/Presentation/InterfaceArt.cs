using Godot;
using System;
using System.Collections.Generic;

namespace Sovereign.Presentation;

public partial class Main
{
    private readonly Dictionary<string, Texture2D> _interfaceTextures = new();
    private Font _numbers = null!;
    private Texture2D UiIcon(string name)
    {
        if (_interfaceTextures.TryGetValue(name, out var cached)) return cached;
        string[] painted = { "overview", "politics", "industry", "markets", "population", "research", "diplomacy", "chronicle", "atlas", "leadership", "construction", "terrain" };
        int index = Array.IndexOf(painted, name);
        Texture2D texture;
        if (index >= 0)
        {
            var sheet = GD.Load<Texture2D>("res://Assets/interface/icon-atlas.png");
            var cell = sheet.GetSize() / new Vector2(4, 3);
            texture = new AtlasTexture { Atlas = sheet, Region = new Rect2(new Vector2(index % 4, index / 4) * cell, cell), FilterClip = true };
        }
        else texture = GD.Load<Texture2D>($"res://Assets/interface/{name}.svg");
        _interfaceTextures[name] = texture; return texture;
    }

    private Texture2D CountryFlagTexture(string id)
    {
        string variant = id == "USA" && _engine.State.Date >= new DateTime(1836, 7, 4) ? "USA-25" : id;
        string key = "flag:" + variant;
        if (_interfaceTextures.TryGetValue(key, out var texture)) return texture;
        string path = $"res://Assets/flags/{variant}.svg";
        texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : UiIcon("qing-banner");
        _interfaceTextures[key] = texture; return texture;
    }

    private string CountryFlagHint(string id) => id switch
    {
        "QNG" => "大清识别旗 · 采用晚清黄龙旗图式；1836年尚非这一制式",
        "USA" => "美国识别旗 · 1836年7月4日前24星，此后使用25星图式；后续星数演变待补充",
        _ => "国家识别旗 · 图式与出处见随附旗帜资料"
    };

    private TextureRect CountryFlag(string id, int width = 82, int height = 50) => new()
    {
        Texture = CountryFlagTexture(id), CustomMinimumSize = new Vector2(width, height),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        TooltipText = CountryFlagHint(id), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
    };

    private void CountryIdentity(Control parent, string id, string title, int size = 25)
    {
        var row = Row(parent, 12); row.AddChild(CountryFlag(id));
        var caption = Para(title, size, Gold); caption.AddThemeFontOverride("font", _serif);
        caption.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; row.AddChild(caption);
    }

    private void ConfigureInterfaceTheme()
    {
        _numbers = new FontVariation { BaseFont = GD.Load<FontFile>("res://Assets/fonts/SourceSerif4.ttf"),
            Fallbacks = new Godot.Collections.Array<Font> { _body } };
        var theme = new Theme { DefaultFont = GD.Load<FontFile>("res://Assets/fonts/SourceHanSerifSC-Regular.otf"), DefaultFontSize = 16 };
        foreach (string type in new[] { "OptionButton", "LineEdit", "PopupMenu" })
        {
            theme.SetFontSize("font_size", type, 15);
            theme.SetColor("font_color", type, Cream);
            theme.SetStylebox("normal", type, CabinetSurface("cabinet-row", 7));
            theme.SetStylebox("hover", type, CabinetSurface("button-hover", 7));
            theme.SetStylebox("focus", type, Style(new Color(0, 0, 0, 0), Gold, 1, 7));
        }
        theme.SetStylebox("panel", "PopupMenu", CabinetSurface("cabinet-body", 9));
        theme.SetStylebox("hover", "PopupMenu", CabinetSurface("button-selected", 5));
        theme.SetColor("font_hover_color", "PopupMenu", Colors.White);
        theme.SetStylebox("panel", "TooltipPanel", Style(new Color("202328"), new Color("b19a69"), 2, 12));
        theme.SetColor("font_color", "TooltipLabel", Cream);
        theme.SetFontSize("font_size", "TooltipLabel", 14);
        theme.SetStylebox("scroll", "VScrollBar", Style(new Color("232329"), new Color("4e4942"), 1, 2));
        theme.SetStylebox("grabber", "VScrollBar", Style(new Color("897350"), new Color("bba474"), 1, 4));
        theme.SetStylebox("grabber_highlight", "VScrollBar", Style(Gold, Gold, 1, 4));
        _ui.Theme = theme;
    }
}
